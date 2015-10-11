using SillyGeo.Data.Storage.Sqlite;

namespace SillyGeo.Tests.Data
{
    public interface ITestServiceFactory
    {
        IImportService CreateImportService();

        IStoreService CreateStoreService();
    }

    public class SqliteTestServiceFactory : ITestServiceFactory
    {
        public IImportService CreateImportService()
        {
            return new SqliteImportService("Data Source=SillyGeoDb.sqlite;");
        }

        public IStoreService CreateStoreService()
        {
            return new SqliteImportService("Data Source=SillyGeoDb.sqlite;");
        }
    }
}
