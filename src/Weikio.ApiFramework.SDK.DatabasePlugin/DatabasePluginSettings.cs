using System;
using System.Collections.Generic;
using System.Reflection;
using SqlKata.Compilers;

namespace Weikio.ApiFramework.SDK.DatabasePlugin
{
    public class DatabasePluginSettings
    {
        public Func<DatabaseOptionsBase, IConnectionCreator> ConnectionCreatorFactory { get; set; }
        public Func<string, string> SqlColumnSelectFactory { get; set; }
        public Compiler Compiler { get; set; }
        public List<string> AdditionalNamespaces { get; set; } = new List<string>();
        public List<Assembly> AdditionalReferences { get; set; } = new List<Assembly>();

        public DatabasePluginSettings(Func<DatabaseOptionsBase, IConnectionCreator> connectionCreatorFactory, Func<string, string> sqlColumnSelectFactory, Compiler compiler)
        {
            ConnectionCreatorFactory = connectionCreatorFactory;
            SqlColumnSelectFactory = sqlColumnSelectFactory;
            Compiler = compiler;

            if (ConnectionCreatorFactory == null)
            {
                throw new ArgumentNullException(nameof(connectionCreatorFactory));
            }

            if (SqlColumnSelectFactory == null)
            {
                throw new ArgumentNullException(nameof(SqlColumnSelectFactory));
            }

            if (Compiler == null)
            {
                throw new ArgumentNullException(nameof(Compiler));
            }
        }
    }
}
