using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Weikio.ApiFramework.SDK.DatabasePlugin.CodeGeneration
{
    public abstract class DirectQueryApiBase<TConfigurationType> where TConfigurationType : DatabaseOptionsBase
    {
        private readonly ILogger<DirectQueryApiBase<TConfigurationType>> _logger;
        public TConfigurationType Configuration { get; set; }

        public DirectQueryApiBase(ILogger<DirectQueryApiBase<TConfigurationType>> logger)
        {
            _logger = logger;
        }

        [HttpPost]
        public async IAsyncEnumerable<Dictionary<string, object>> Run(string query, Dictionary<string, object> parameters)
        {
            var queryAndParameters = new QueryData() { Parameters = parameters ?? new Dictionary<string, object>(), Query = query, IsCount = false };

            using (var conn = Configuration.CreateConnection())
            {
                await conn.OpenAsync();
                _logger.LogTrace("Connection opened");

                using (var cmd = conn.CreateCommand())
                {
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
                            if (prm.Value is JsonElement jsonElement)
                            {
                                var json = jsonElement.GetRawText();

                                var jValue = (JValue)JToken.Parse(json);
                                dynamic obj = jValue.Value;

                                commandParameter.Value = obj;
                            }
                            else
                            {
                                commandParameter.Value = prm.Value;
                            }
                        }

                        cmd.Parameters.Add(commandParameter);
                    }

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        _logger.LogTrace("Opened reader");

                        while (await reader.ReadAsync())
                        {
                            _logger.LogTrace("Line read, mapping to result item");

                            // Convert current row into a dictionary
                            var dict = new Dictionary<string, object>();

                            for (var lp = 0; lp < reader.FieldCount; lp++)
                            {
                                try
                                {
                                    dict.Add(reader.GetName(lp), reader.GetValue(lp));
                                }
                                catch (Exception e)
                                {
                                    _logger.LogWarning(e, "Failed to convert result row to dictionary item. Row is ignored");
                                }
                            }

                            yield return dict;
                        }
                    }
                }
            }
        }
    }
}
