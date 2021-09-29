using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Weikio.TypeGenerator;

namespace Weikio.ApiFramework.SDK.DatabasePlugin.CodeGeneration
{
    public class CodeGenerator
    {
        private readonly DatabasePluginSettings _databasePluginSettings;
        private readonly ILogger<CodeGenerator> _logger;

        public CodeGenerator(DatabasePluginSettings databasePluginSettings,
            ILogger<CodeGenerator> logger)
        {
            _databasePluginSettings = databasePluginSettings;
            _logger = logger;
        }

        public static CodeToAssemblyGenerator CodeToAssemblyGenerator { get; set; }

        public Assembly GenerateAssembly(IList<Table> tableSchema, SqlCommands nonQueryCommands, DatabaseOptionsBase databaseOptions)
        {
            var assemblyCode = GenerateCode(tableSchema, nonQueryCommands, databaseOptions);

            try
            {
                CodeToAssemblyGenerator = new CodeToAssemblyGenerator(true, default, _databasePluginSettings.AdditionalReferences);
                CodeToAssemblyGenerator.ReferenceAssembly(typeof(Console).Assembly);
                CodeToAssemblyGenerator.ReferenceAssembly(typeof(System.Data.DataRow).Assembly);
                CodeToAssemblyGenerator.ReferenceAssemblyContainingType<ProducesResponseTypeAttribute>();
                CodeToAssemblyGenerator.ReferenceAssembly(databaseOptions.GetType().Assembly);
                CodeToAssemblyGenerator.ReferenceAssembly(typeof(SqlKata.Column).Assembly);
                CodeToAssemblyGenerator.ReferenceAssembly(typeof(CodeGenerator).Assembly);

                _logger.LogInformation("Generating assembly from code");
                var result = CodeToAssemblyGenerator.GenerateAssembly(assemblyCode);

                return result;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to generate assembly");

                throw;
            }
        }

        public string GenerateCode(IList<Table> tableSchema, SqlCommands nonQueryCommands, DatabaseOptionsBase databaseOptions)
        {
            var source = new StringBuilder();
            source.UsingNamespace("System");
            source.UsingNamespace("System.Collections.Generic");
            source.UsingNamespace("System.Reflection");
            source.UsingNamespace("System.Linq");
            source.UsingNamespace("System.Diagnostics");
            source.UsingNamespace("System.Data");
            source.UsingNamespace("Weikio.ApiFramework.SDK.DatabasePlugin.CodeGeneration");
            source.UsingNamespace("Microsoft.AspNetCore.Http");
            source.UsingNamespace("Microsoft.AspNetCore.Mvc");
            source.UsingNamespace("Microsoft.Extensions.Logging");
            
            if (_databasePluginSettings.AdditionalNamespaces?.Any() == true)
            {
                foreach (var ns in _databasePluginSettings.AdditionalNamespaces)
                {
                    source.UsingNamespace(ns);
                }
            }
            
            source.WriteLine("");

            _logger.LogInformation("Generating code for tables and commands");

            foreach (var table in tableSchema)
            {
                source.WriteNamespaceBlock(table, namespaceBlock =>
                {
                    _logger.LogDebug("Generating code for table {Table}", table.Name);
                    namespaceBlock.WriteDataTypeClass(table);

                    namespaceBlock.WriteApiClass(table, databaseOptions);
                });
            }

            if (nonQueryCommands?.Any() == true)
            {
                foreach (var command in nonQueryCommands)
                {
                    source.WriteNamespaceBlock(command, namespaceBlock =>
                    {
                        _logger.LogDebug("Generating code for non query command {NonQueryCommand}", command.Key);
                    
                        namespaceBlock.WriteNonQueryCommandApiClass(command);
                    });
                }
            }

            _logger.LogInformation("Code generated for tables and commands");

            return source.ToString();
        }
    }
}
