using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Weikio.TypeGenerator.Types;

namespace Weikio.ApiFramework.SDK.DatabasePlugin.CodeGeneration
{
    public abstract class ApiBase<T> where T : DtoBase, new()
    {
        protected ILogger<ApiBase<T>> Logger { get; }

        protected ApiBase(ILogger<ApiBase<T>> logger)
        {
            Logger = logger;
        }

        private readonly IConnectionCreator _connectionCreator = Cache.ConnectionCreator;
        protected abstract string TableName { get; }
        protected abstract Dictionary<string, string> ColumnMap { get; }
        protected abstract bool IsSqlCommand { get; }
        protected string CommandText { get; set; }
        protected List<Tuple<string, object>> CommandParameters { get; set; }
        private static ConcurrentDictionary<string, Type> _cachedTypes = new ConcurrentDictionary<string, Type>();

        public DatabaseOptionsBase Configuration { get; set; }
        
        protected async IAsyncEnumerable<object> RunSelect(string select, string filter, string orderby, int? top, int? skip, bool? count)
        {
            var fields = new List<string>();

            using (var conn = _connectionCreator.CreateConnection(Configuration))
            {
                await conn.OpenAsync();

                using (var cmd = conn.CreateCommand())
                {
                    var queryAndParameters = CreateQuery(TableName, select, filter, orderby, top, skip, count, fields);

                    cmd.CommandText = queryAndParameters.Query;

                    foreach (var prm in queryAndParameters.Parameters)
                    {
                        var commandParameter = cmd.CreateParameter();
                        commandParameter.ParameterName = prm.Key;
                        if (prm.Value == null)
                        {
                            commandParameter.Value = DBNull.Value;
                        }
                        else
                        {
                            commandParameter.Value = prm.Value;
                        }

                        cmd.Parameters.Add(commandParameter);
                    }

                    var selectedColumns = ColumnMap;
                    Type generatedType = null;

                    if (fields?.Any() == true)
                    {
                        selectedColumns = ColumnMap.Where(x => fields.Contains(x.Key, StringComparer.OrdinalIgnoreCase)).ToDictionary(p => p.Key, p => p.Value);

                        generatedType = GetFilteredType(selectedColumns);
                    }

                    if (queryAndParameters.IsCount)
                    {
                        var countResult = await cmd.ExecuteScalarAsync();

                        yield return countResult;
                    }
                    else
                    {
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                if (generatedType != null)
                                {
                                    var item = Activator.CreateInstance(generatedType);

                                    foreach (var column in selectedColumns)
                                    {
                                        var dbColumnValue = reader[column.Key] == DBNull.Value ? null : reader[column.Key];

                                        if (Configuration.TrimStrings && dbColumnValue is string dbString)
                                        {
                                            dbColumnValue = dbString.Trim();
                                        }
                                        
                                        generatedType.InvokeMember(column.Value,
                                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.SetProperty,
                                            Type.DefaultBinder, item, new[] { dbColumnValue });
                                    }

                                    yield return item;
                                }
                                else
                                {
                                    var item = new T();

                                    foreach (var column in selectedColumns)
                                    {
                                        var dbColumnValue = reader[column.Key] == DBNull.Value ? null : reader[column.Key];

                                        if (Configuration.TrimStrings && dbColumnValue is string dbString)
                                        {
                                            dbColumnValue = dbString.Trim();
                                        }

                                        item[column.Value] = dbColumnValue;
                                    }

                                    yield return item;
                                }
                            }
                        }
                    }
                }
            }
        }

        private static Type GetFilteredType(Dictionary<string, string> selectedColumns)
        {
            var typeName = typeof(T).Name;
            var columnNames = string.Join("", selectedColumns.Select(x => x.Value));
            var typeId = Math.Abs(columnNames.GetHashCode());

            var key = typeName + typeId;

            return _cachedTypes.GetOrAdd(key, s =>
            {
                var wrapperOptions = new TypeToTypeWrapperOptions
                {
                    IncludedProperties = new List<string>(selectedColumns.Select(x => x.Value)),
                    AssemblyGenerator = CodeGenerator.CodeToAssemblyGenerator,
                    TypeName = key
                };

                var result = new TypeToTypeWrapper().CreateType(typeof(T), wrapperOptions);

                return result;
            });
        }

        protected abstract QueryData CreateQuery(string tableName, string select, string filter, string orderby, int? top, int? skip, bool? count,
            List<string> fields);
    }
}
