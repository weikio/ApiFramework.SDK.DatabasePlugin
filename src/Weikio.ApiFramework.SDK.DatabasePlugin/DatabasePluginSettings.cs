using System;
using System.Collections.Generic;
using System.Reflection;
using SqlKata.Compilers;

namespace Weikio.ApiFramework.SDK.DatabasePlugin
{
    public class DatabasePluginSettings
    {
        public Func<string, string> SqlColumnSelectFactory { get; set; }
        public List<string> AdditionalNamespaces { get; set; } = new List<string>();
        public List<Assembly> AdditionalReferences { get; set; } = new List<Assembly>();
        public DatabasePluginSettings(Func<string, string> sqlColumnSelectFactory)
        {
            SqlColumnSelectFactory = sqlColumnSelectFactory;

            if (SqlColumnSelectFactory == null)
            {
                throw new ArgumentNullException(nameof(SqlColumnSelectFactory));
            }
        }
    }
}
