using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using SqlKata.Compilers;
using Weikio.ApiFramework.SDK.DatabasePlugin.CodeGeneration;

namespace Weikio.ApiFramework.SDK.DatabasePlugin
{
    public class DatabaseApiFactoryBase
    {
        private readonly ILogger<DatabaseApiFactoryBase> _logger;
        private readonly ILoggerFactory _loggerFactory;

        protected DatabaseApiFactoryBase(ILogger<DatabaseApiFactoryBase> logger, ILoggerFactory loggerFactory)
        {
            _logger = logger;
            _loggerFactory = loggerFactory;
        }

        protected List<Type> Generate(DatabaseOptionsBase configuration,Func<DatabaseOptionsBase, 
            IConnectionCreator> connectionCreatorFactory, Func<string, string> sqlColumnSelectFactory,
            Compiler compiler)
        {
            try
            {
                using var re = new SchemaReader(configuration, connectionCreatorFactory.Invoke(configuration), sqlColumnSelectFactory,
                    _loggerFactory.CreateLogger<SchemaReader>());

                re.Connect();
                var schema = re.GetSchema();
                
                var generator = new CodeGenerator(connectionCreatorFactory.Invoke(configuration), compiler, _loggerFactory.CreateLogger<CodeGenerator>());
                var assembly = generator.GenerateAssembly(schema.Tables, schema.Commands, configuration);

                var result = assembly.GetExportedTypes()
                    .Where(x => x.Name.EndsWith("Api"))
                    .ToList();
                
                _logger.LogDebug("Generated {ApiCount} APIs", result.Count);

                return result;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to generate API");

                throw;
            }
        }
    }
}
