using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Weikio.ApiFramework.SDK.DatabasePlugin.CodeGeneration
{
    public abstract class CommandApiBase<T> : ApiBase<T> where T : DtoBase, new()
    {
        protected override QueryData CreateQuery(string tableName, string select, string filter, string orderby, int? top, int? skip, bool? count, List<string> fields)
        {
            var query = CommandText.Replace("\"", "\"\"");

            var cmdParameters = new Dictionary<string, object>();

            foreach (var commandParameter in CommandParameters)
            {
                cmdParameters.Add(commandParameter.Item1, commandParameter.Item2);
            }

            return new QueryData { Query = query, Parameters = cmdParameters };
        }

        protected CommandApiBase(ILogger<ApiBase<T>> logger) : base(logger)
        {
        }
    }
}
