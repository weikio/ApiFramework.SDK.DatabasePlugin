using System.Data.Common;

namespace Weikio.ApiFramework.SDK.DatabasePlugin
{
    public interface IConnectionCreator
    {
        DbConnection CreateConnection(DatabaseOptionsBase options);
    }
}
