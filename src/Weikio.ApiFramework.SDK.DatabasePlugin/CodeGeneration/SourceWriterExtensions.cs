using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using SqlKata.Compilers;
using Weikio.TypeGenerator.Types;

namespace Weikio.ApiFramework.SDK.DatabasePlugin.CodeGeneration
{
    public static class SourceWriterExtensions
    {
        public static void WriteNamespaceBlock(this StringBuilder writer, Table table,
            Action<StringBuilder> contentProvider)
        {
            writer.Namespace(typeof(DatabaseApiFactoryBase).Namespace + ".Generated" + table.Name);

            contentProvider.Invoke(writer);

            writer.FinishBlock(); // Finish the namespace
        }

        public static void WriteNamespaceBlock(this StringBuilder writer, KeyValuePair<string, SqlCommand> command,
            Action<StringBuilder> contentProvider)
        {
            writer.Namespace(typeof(DatabaseApiFactoryBase).Namespace + ".Generated" + command.Key);

            contentProvider.Invoke(writer);

            writer.FinishBlock(); // Finish the namespace
        }

        public static void WriteDataTypeClass(this StringBuilder writer, Table table)
        {
            writer.WriteLine($"public class {GetDataTypeName(table)} : Weikio.ApiFramework.SDK.DatabasePlugin.CodeGeneration.DtoBase");
            writer.StartBlock();

            foreach (var column in table.Columns)
            {
                var typeName = TypeToTypeWrapper.GetFriendlyName(column.Type, column.Type.Name);
                writer.WriteLine($"public {typeName} {GetPropertyName(column.Name)} {{ get;set; }}");
            }

            writer.WriteLine("");

            writer.FinishBlock(); // Finish the class
        }

        public static void WriteApiClass(this StringBuilder writer, Table table, DatabaseOptionsBase options)
        {
            var apiClassName = GetApiClassName(table);

            if (table.SqlCommand != null)
            {
                writer.WriteLine($"public class {apiClassName} : CommandApiBase<{GetDataTypeName(table)}, {options.GetType().FullName}>");
                writer.WriteLine("{");

                writer.WriteLine($"public {apiClassName}(Microsoft.Extensions.Logging.ILogger<{apiClassName}> logger) : base(logger)");
                writer.StartBlock(); // Constructor
                writer.WriteLine($"CommandText = \"{table.SqlCommand.CommandText}\";");
                writer.WriteLine("CommandParameters = new List<Tuple<string, object>>();");
                writer.WriteLine("}");

                writer.WriteSqlCommandMethod(table.Name, table.SqlCommand);
            }
            else
            {
                writer.WriteLine($"public class {apiClassName} : TableApiBase<{GetDataTypeName(table)}, {options.GetType().FullName}>");
                writer.WriteLine("{");
                            
                writer.WriteLine($"public {apiClassName}(Microsoft.Extensions.Logging.ILogger<{apiClassName}> logger) : base(logger)");
                writer.StartBlock(); // Constructor
                writer.FinishBlock(); // Constructor
            }

            var columnMap = new Dictionary<string, string>();

            foreach (var column in table.Columns)
            {
                columnMap.Add(column.Name, GetPropertyName(column.Name));
            }

            writer.WriteLine("private Dictionary<string, string> _columnMap = new Dictionary<string, string>");
            writer.WriteLine("{");

            foreach (var columnPair in columnMap)
            {
                writer.Write($"    {{\"{columnPair.Key}\", \"{columnPair.Value}\"}},");
            }

            writer.WriteLine("};");

            writer.WriteLine("");

            writer.WriteLine($"protected override string TableName => \"{table.NameWithQualifier}\";");
            writer.WriteLine("protected override Dictionary<string, string> ColumnMap => _columnMap;");
            writer.WriteLine($"protected override bool IsSqlCommand => {(table.IsSqlCommand ? "true" : "false")};");

            if (table.SqlCommand == null)
            {
                writer.WriteLine($"[ProducesResponseType(200, Type = typeof(List<{GetDataTypeName(table)}>))]");

                writer.WriteLine(
                    "public async IAsyncEnumerable<object> Select(string select, string filter, string orderby, int? top, int? skip, bool? count)");
                writer.WriteLine("{");
                writer.WriteLine("await foreach (var item in RunSelect(select, filter, orderby, top, skip, count))");
                writer.WriteLine("{");
                writer.WriteLine("yield return item;");
                writer.WriteLine("}"); // Finish the await foreach

                writer.WriteLine("}"); // Finish the Select method
            }

            writer.WriteLine("}"); // Finish the class
        }

        public static void WriteNonQueryCommandApiClass(this StringBuilder writer, KeyValuePair<string, SqlCommand> command)
        {
            writer.WriteLine($"public class {GetApiClassName(command)} : CommandApiBase<{GetDataTypeName(command.Key, command.Value)}>");
            writer.WriteLine("{");

            writer.WriteLine($"public {GetApiClassName(command)}() {{");
            writer.WriteLine($"CommandText = \"{command.Value.CommandText}\";");
            writer.WriteLine("CommandParameters = new List<Tuple<string, object>>();");
            writer.WriteLine("}");

            writer.WriteSqlCommandMethod(command.Key, command.Value);

            writer.FinishBlock(); // Finish the class
        }

        private static string GetApiClassName(KeyValuePair<string, SqlCommand> command)
        {
            return $"{command.Key}Api";
        }

        private static string GetDataTypeName(string commandName, SqlCommand sqlCommand = null)
        {
            if (!string.IsNullOrEmpty(sqlCommand?.DataTypeName))
            {
                return sqlCommand.DataTypeName;
            }

            return commandName + "Item";
        }

        private static void WriteSqlCommandMethod(this StringBuilder writer, string tableName, SqlCommand sqlCommand)
        {
            var sqlMethod = sqlCommand.CommandText.Trim()
                .Split(new[] { ' ' }, 2)
                .First().ToLower();
            sqlMethod = sqlMethod.Substring(0, 1).ToUpper() + sqlMethod.Substring(1);

            var methodParameters = new List<string>();

            if (sqlCommand.Parameters != null)
            {
                foreach (var sqlCommandParameter in sqlCommand.Parameters)
                {
                    var methodParam = "";

                    if (sqlCommandParameter.Optional)
                    {
                        var paramType = Type.GetType(sqlCommandParameter.Type);

                        if (paramType.IsValueType)
                        {
                            methodParam += $"{sqlCommandParameter.Type}? {sqlCommandParameter.Name} = null";
                        }
                        else
                        {
                            methodParam += $"{sqlCommandParameter.Type} {sqlCommandParameter.Name} = null";
                        }
                    }
                    else
                    {
                        methodParam += $"{sqlCommandParameter.Type} {sqlCommandParameter.Name}";
                    }

                    methodParameters.Add(methodParam);
                }
            }

            var dataTypeName = GetDataTypeName(tableName, sqlCommand);

            writer.WriteLine($"[ProducesResponseType(200, Type = typeof(List<{dataTypeName}>))]");
            writer.WriteLine($"public async IAsyncEnumerable<object> {sqlMethod}({string.Join(", ", methodParameters)})");
            writer.StartBlock();

            writer.WriteLine("");

            if (sqlCommand.Parameters?.Any() == true)
            {
                foreach (var sqlCommandParameter in sqlCommand.Parameters)
                {
                    writer.WriteLine($"CommandParameters.Add(new Tuple<string, object>(\"{sqlCommandParameter.Name}\", {sqlCommandParameter.Name}));");
                }
            }

            writer.WriteLine("await foreach (var item in RunSelect(null, null, null, null, null, null))");
            writer.WriteLine("{");
            writer.WriteLine("yield return item;");
            writer.WriteLine("}"); // Finish the Select method

            writer.FinishBlock(); // Finish the method
        }

        private static string GetApiClassName(Table table)
        {
            return $"{table.Name}Api";
        }

        private static string GetDataTypeName(Table table)
        {
            if (!string.IsNullOrEmpty(table.SqlCommand?.DataTypeName))
            {
                return table.SqlCommand.DataTypeName;
            }

            return table.Name + "Item";
        }

        private static string GetPropertyName(string originalName)
        {
            var isValid = IsValid(originalName);

            if (isValid)
            {
                return originalName;
            }

            var result = originalName;

            if (result.Contains(" "))
            {
                result = result.Replace(" ", "").Trim();
            }

            if (IsValid(originalName))
            {
                return result;
            }

            return $"@{result}";
        }

        private static bool IsValid(string originalName)
        {
            var keywordKind = SyntaxFacts.GetKeywordKind(originalName);
            var isValid = SyntaxFacts.IsValidIdentifier(originalName) && SyntaxFacts.IsReservedKeyword(keywordKind) == false;

            return isValid;
        }
    }
}
