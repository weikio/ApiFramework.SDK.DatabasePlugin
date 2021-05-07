using System;
using System.Collections.Generic;
using System.Linq;
using DynamicODataToSQL;
using Microsoft.Extensions.Logging;

namespace Weikio.ApiFramework.SDK.DatabasePlugin.CodeGeneration
{
    public abstract class TableApiBase<T> : ApiBase<T> where T : DtoBase, new()
    {
        private static ODataToSqlConverter _converter = new ODataToSqlConverter(new EdmModelBuilder(), 
            Cache.DbCompiler);

        protected override QueryData CreateQuery(string tableName, string select, string filter, string orderby, int? top, int? skip, bool? count, List<string> fields)
        {
            Logger.LogDebug("Creating SQL query for table {TableName}, select {Select}, filter {Filter}, orderBy {OrderBy}, top {Top}, skip {Skip}, count {Count}, fields {Fields}",
                tableName, select, filter, orderby, top, skip,count, fields);
            
            var odataQueryParameters = new Dictionary<string, string>();

            if (!string.IsNullOrWhiteSpace(select))
            {
                odataQueryParameters.Add("select", select);
                fields.AddRange(select.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()));
            }
            
            if (!string.IsNullOrWhiteSpace(filter))
            {
                odataQueryParameters.Add("filter", filter);
            }
            
            if (!string.IsNullOrWhiteSpace(orderby))
            {
                odataQueryParameters.Add("orderby", orderby);
            }

            if (top != null)
            {
                odataQueryParameters.Add("top", top.GetValueOrDefault().ToString());
            }

            if (skip != null)
            {
                odataQueryParameters.Add("skip", skip.GetValueOrDefault().ToString());
            }

            var result = _converter.ConvertToSQL(
                tableName,
                odataQueryParameters, count.GetValueOrDefault());

            var sql = result.Item1;

            var sqlParams = result.Item2; 

            Logger.LogDebug("Created query: {Query}", sql);
            
            return new QueryData { Query = sql, Parameters = sqlParams, IsCount = count.GetValueOrDefault()};
        }

        protected TableApiBase(ILogger<ApiBase<T>> logger) : base(logger)
        {
        }
    }
}
