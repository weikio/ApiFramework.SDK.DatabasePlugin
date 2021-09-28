using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Weikio.ApiFramework.SDK.DatabasePlugin
{
    public class SchemaReader : IDisposable
    {
        private readonly DatabaseOptionsBase _options;
        private readonly Func<string, string> _sqlColumnSelectFactory;
        private readonly ILogger<SchemaReader> _logger;
        private DbConnection _connection;

        public SchemaReader(DatabaseOptionsBase options, DbConnection connection, Func<string, string> sqlColumnSelectFactory,
            ILogger<SchemaReader> logger)
        {
            _options = options;
            _connection = connection;
            _sqlColumnSelectFactory = sqlColumnSelectFactory;
            _logger = logger;
        }

        public void Connect()
        {
            _connection.Open();
        }

        private void RequireConnection()
        {
            if (_connection == null)
            {
                throw new InvalidOperationException("SchemaReader is not connected to a database.");
            }
        }

        public (List<Table> Tables, SqlCommands Commands) GetSchema()
        {
            var tables = new List<Table>();
            SqlCommands commands = null;

            try
            {
                if (_options.ShouldGenerateApisForTables())
                {
                    var dbTables = HandleTables();
                    tables.AddRange(dbTables);

                    _logger.LogInformation("Found {DatabaseTablesCount} tables", dbTables.Count);
                }
                else
                {
                    _logger.LogInformation("Skipping schema handling for tables based on the configuration");
                }

                if (_options.SqlCommands?.Any() == true)
                {
                    var (queryCommands, sqlCommands) = HandleCommands(_options.SqlCommands);

                    _logger.LogInformation("Found {QueryCommandCount} query commands and {CommandCount} non query commands", queryCommands.Count,
                        sqlCommands.Count);

                    tables.AddRange(queryCommands);
                    commands = sqlCommands;
                }
                else
                {
                    _logger.LogInformation("Skipping schema handling for commands based on the configuration");
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to handle schema");

                throw;
            }

            _logger.LogInformation("Found schema with {TableCount} tables and {CommandCount} commands", tables.Count, commands?.Count ?? 0);

            _connection.Dispose();
            
            return (tables, commands);
        }

        public List<Table> HandleTables()
        {
            _logger.LogInformation("Handling tables");

            RequireConnection();

            var tables = GetTables();
            _logger.LogDebug("Found {TableCount} tables in total", tables.Count);
            var result = new List<Table>();

            foreach (var table in tables)
            {
                if (!_options.Includes(table.Name))
                {
                    continue;
                }

                result.Add(table);
            }

            _logger.LogDebug("After filtering handling {TableCount} tables", result.Count);

            foreach (var table in result)
            {
                var columns = GetColumnsForTable(table);
                table.Columns = columns;
            }

            return result;
        }

        public (IList<Table> QueryCommands, SqlCommands NonQueryCommands) HandleCommands(SqlCommands sqlCommands)
        {
            _logger.LogInformation("Handling commands");

            RequireConnection();

            var queryCommands = new List<Table>();
            var nonQueryCommands = new SqlCommands();

            if (sqlCommands?.Any() != true)
            {
                return (queryCommands, nonQueryCommands);
            }

            foreach (var sqlCommand in sqlCommands)
            {
                if (sqlCommand.Value.IsNonQuery())
                {
                    _logger.LogInformation("Handling non query command {CommandName}", sqlCommand.Key);
                    HandleNonQueryCommand(sqlCommand, nonQueryCommands);

                    continue;
                }

                using (var cmd = _connection.CreateCommand())
                {
                    _logger.LogInformation("Handling query command {CommandName}", sqlCommand.Key);

                    var table = ConvertQueryToTable(cmd, sqlCommand);
                    
                    var columns = GetColumnsFromDbCommand(cmd, sqlCommand.Key);
                    table.Columns = columns;

                    queryCommands.Add(table);
                }
            }

            return (queryCommands, nonQueryCommands);
        }

        public virtual IList<Table> GetTables()
        {
            var result = new List<Table>();

            var schemaTables = _connection.GetSchema("Tables");

            foreach (DataRow schemaTable in schemaTables.Rows)
            {
                var tableNameAndSchema = GetTableNameAndSchemaFromSchemaRow(schemaTable);

                if (tableNameAndSchema == default)
                {
                    continue;
                }

                var table = new Table(tableNameAndSchema.Name, tableNameAndSchema.Schema);
                result.Add(table);
            }

            return result;
        }

        public virtual IList<Column> GetColumnsForTable(Table table)
        {
            _logger.LogDebug("Gettings columns for table {Table}", table.Name);

            using (var command = _connection.CreateCommand())
            {
                string query;

                if (table.IsSqlCommand)
                {
                    query = table.SqlCommand.CommandText;
                }
                else
                {
                    query = _sqlColumnSelectFactory.Invoke(table.NameWithQualifier);
                }

                command.CommandText = query;
                command.CommandTimeout = (int) TimeSpan.FromMinutes(5).TotalSeconds;

                var columns = GetColumnsFromDbCommand(command, table.Name);

                return columns;
            }
        }

        protected virtual (string Name, string Schema) GetTableNameAndSchemaFromSchemaRow(DataRow row)
        {
            if (row["TABLE_TYPE"].ToString() != "TABLE" && row["TABLE_TYPE"].ToString() != "BASE TABLE")
            {
                return default;
            }

            var tableQualifier = "";

            if (row.Table.Columns.Contains("TABLE_QUALIFIER"))
            {
                tableQualifier = row["TABLE_QUALIFIER"].ToString();
            }
            else if (row.Table.Columns.Contains("TABLE_SCHEM"))
            {
                tableQualifier = row["TABLE_SCHEM"].ToString();
            }
            else if (row.Table.Columns.Contains("TABLE_SCHEMA"))
            {
                tableQualifier = row["TABLE_SCHEMA"].ToString();
            }

            var tableName = row["TABLE_NAME"].ToString();

            return (tableName, tableQualifier);
        }

        protected virtual void HandleNonQueryCommand(KeyValuePair<string, SqlCommand> sqlCommand, SqlCommands nonQueryCommands)
        {
            if (sqlCommand.Value.IsDelete())
            {
                throw new NotSupportedException($"DELETE commands are not supported. Command name: '{sqlCommand.Key}'.");
            }

            if (sqlCommand.Value.IsUpdate() && !sqlCommand.Value.HasWhereClause())
            {
                throw new InvalidOperationException("");
            }

            nonQueryCommands.Add(sqlCommand.Key, sqlCommand.Value);

            // don't read schema for INSERT and UPDATE commands
        }
        
        protected virtual IList<Column> GetColumnsFromDbCommand(DbCommand dbCommand, string tableName)
        {
            try
            {
                var columns = new List<Column>();

                using (var reader = dbCommand.ExecuteReader())
                {
                    using (var dtSchema = reader.GetSchemaTable())
                    {
                        if (dtSchema != null)
                        {
                            foreach (DataRow schemaColumn in dtSchema.Rows)
                            {
                                Type dataType;

                                // Try to handle scenarios like hierarchyid in SQL Server
                                var columnName = Convert.ToString(schemaColumn["ColumnName"]);

                                if (schemaColumn["DataType"] == DBNull.Value)
                                {
                                    dataType = typeof(string);

                                    _logger.LogWarning(
                                        "Encountered column {ColumnName} in table {TableName} with missing DataType. Falling back to string presentation",
                                        columnName, tableName);
                                }
                                else
                                {
                                    dataType = (Type) schemaColumn["DataType"];
                                }

                                var isNullable = (bool) schemaColumn["AllowDBNull"];

                                columns.Add(new Column(columnName, dataType, isNullable));
                            }
                        }
                    }
                }

                return columns;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to get columns using DbCommand with query {Query}", dbCommand.CommandText);

                throw;
            }
        }

        protected virtual Table ConvertQueryToTable(DbCommand cmd, KeyValuePair<string, SqlCommand> sqlCommand)
        {
            cmd.CommandText = sqlCommand.Value.CommandText;

            if (!string.IsNullOrWhiteSpace(sqlCommand.Value.CommandSchemaText))
            {
                cmd.CommandText = sqlCommand.Value.CommandSchemaText;
            }
            
            cmd.CommandTimeout = (int) TimeSpan.FromMinutes(5).TotalSeconds;

            if (sqlCommand.Value.Parameters != null)
            {
                foreach (var parameter in sqlCommand.Value.Parameters)
                {
                    var parameterType = Type.GetType(parameter.Type);

                    if (parameterType == null)
                    {
                        throw new ArgumentException(
                            $"Command '{sqlCommand.Key}' has an invalid type '{parameter.Type}' defined for parameter '{parameter.Name}'.");
                    }

                    object parameterValue = null;

                    if (parameter.DefaultValue != null)
                    {
                        parameterValue = parameter.DefaultValue;
                    }
                    else
                    {
                        if (parameterType.IsValueType)
                        {
                            parameterValue = Activator.CreateInstance(parameterType);
                        }
                        else
                        {
                            parameterValue = DBNull.Value;
                        }
                    }

                    var cmdParameter = cmd.CreateParameter();
                    cmdParameter.ParameterName = $"@{parameter.Name}";
                    cmdParameter.Value = parameterValue;

                    cmd.Parameters.Add(cmdParameter);
                }
            }

            var table = new Table($"{sqlCommand.Key}", "") { SqlCommand = sqlCommand.Value };

            return table;
        }

        /// <summary>
        /// Maps DB column type to .NET type: Currently not in use
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        private Dictionary<string, Type> GetDataTypes()
        {
            var result = new Dictionary<string, Type>();
            const string colSqlType = "TypeName";
            const string colNetType = "DataType";

            var dataTypesSchema = _connection.GetSchema("DATATYPES");

            var sqlTypeColumnIndex = dataTypesSchema.Columns.IndexOf(colSqlType);

            if (sqlTypeColumnIndex < 0)
            {
                throw new InvalidOperationException("Data type schema is invalid, no SQL type column found.");
            }

            var netTypeColumnIndex = dataTypesSchema.Columns.IndexOf(colNetType);

            if (netTypeColumnIndex < 0)
            {
                throw new InvalidOperationException("Data type schema is invalid, no .NET type column found.");
            }

            for (var i = 0; i < dataTypesSchema.Rows.Count; i++)
            {
                var row = dataTypesSchema.Rows[i];
                var sqlTypeName = row.ItemArray[sqlTypeColumnIndex] as string;
                var netTypeName = row.ItemArray[netTypeColumnIndex] as string;

                if (!string.IsNullOrEmpty(sqlTypeName) && !string.IsNullOrEmpty(netTypeName))
                {
                    var type = Type.GetType(netTypeName);
                    result.Add(sqlTypeName, type);
                }
            }

            return result;
        }

        #region IDisposable Support

        private bool disposedValue; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    if (_connection != null)
                    {
                        _connection.Dispose();
                        _connection = null;
                    }
                }

                disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        void IDisposable.Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }

        #endregion
    }
}
