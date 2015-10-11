using SillyGeo.Infrastructure;
using System.Net;
using System.Threading.Tasks;
using Xunit;

namespace SillyGeo.Tests.Data
{
    public class ImportServiceTest
    {
        [TypeActivatorData(typeof(SqliteTestServiceFactory))]
        [Theory]
        public async Task MustAddIPRangeLocations(ITestServiceFactory serviceFactory)
        {
            using (var storeService = serviceFactory.CreateStoreService())
            {
                await storeService.CreateIfNotExistsAsync();
                using (var importService = serviceFactory.CreateImportService())
                {
                    await storeService.CreateIfNotExistsAsync();
                    await importService.AddIPRangeLocationRangeAsync("test provider",
                        new IPRangeLocation[] {
                            new IPRangeLocation
                            {
                                AreaId = 1,
                                Coordinates = new Coordinates { Latitude = 60, Longitude = 40 },
                                IPRange = new IPRange
                                {
                                    Start = IPAddress.Parse("134.108.0.0"),
                                    End = IPAddress.Parse("134.108.255.255")
                                }
                            }
                        });
                }
                await storeService.RemoveAsync();
            }
        }

        [Fact]
        public void MustAddAreas()
        {

        }

        [Fact]
        public void MustDropDatabase()
        {

        }

        [Fact]
        public void MustClearIPRangeLocations()
        {

        }

        [Fact]
        public void MustClearAreas()
        {

        }
    }
}
