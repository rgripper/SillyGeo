using Microsoft.Dnx.Runtime;
using Microsoft.Framework.Configuration;
using Microsoft.Framework.DependencyInjection;
using Newtonsoft.Json;
using SillyGeo.Data;
using SillyGeo.Data.Providers;
using SillyGeo.Data.Storage.Sqlite;
using SillyGeo.Infrastructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Linq;
using System.Diagnostics;

namespace SillyGeo.ConsoleDemo
{
    public class Program
    {
        private readonly IServiceProvider _serviceProvider;

        private readonly IConfiguration _config;

        public Program(IApplicationEnvironment appEnvironment)
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();

            _config = new ConfigurationBuilder(AppContext.BaseDirectory).AddJsonFile("config.json").Build();
        }

        private void ConfigureServices(IServiceCollection services)
        {

        }

        public async Task Main(string[] args)
        {
            try
            {
                var connectionString = _config["Data:DefaultConnection:ConnectionString"];
                var geoNamesService = new SqliteGeoNamesService(connectionString);
                var databaseManager = new DatabaseManager(connectionString);

                var geoLocationService = new SqliteIPGeoLocationService(connectionString, geoNamesService);

                await databaseManager.CreateDatabaseIfNotExistsAsync();
                await databaseManager.ClearAreasAsync();
                await databaseManager.ClearIPRangesAsync();

                var geoNamesReader = new GeoNamesReader();
                //geoNamesReader.ExtractLocalizedNames("Content/GeoNames/alternateNames.txt", "Content/GeoNames/alternateNames.txt_en.txt", "en");
                var areas = geoNamesReader.ReadAreas(
                    localizedNamesPath: "Content/GeoNames/alternateNames.txt_en.txt",
                    admin1Path: "Content/GeoNames/admin1CodesASCII.txt", admin2Path: "Content/GeoNames/admin2Codes.txt",
                    citiesPath: "Content/GeoNames/cities15000.txt", contriesPath: "Content/GeoNames/countryInfo.txt");

                var areaCount = areas.Count();
                await databaseManager.AddAreaRangeAsync(areas, new Progress<int>(x => Console.Write("\r{0}/{1} areas were added", x, areaCount)));
                Console.WriteLine();
                var ipGeobaseRuProvider = new IPGeobaseRuProvider(geoNamesService);

                IEnumerable<IPRangeLocation> locations;

                using (Stream cidrStream = File.OpenRead("Content/ProviderData/IPGeobaseRu/cidr_optim.txt"),
                    citiesStream = File.OpenRead("Content/ProviderData/IPGeobaseRu/cities.txt"))
                {
                    locations = await ipGeobaseRuProvider.GetIPRangeLocationsAsync(cidrStream, citiesStream);
                }

                //var maxMindCitiesCsv2Provider = new MaxMindCitiesCsv2Provider(geoNamesService);

                //using (var stream = File.OpenRead("Content/ProviderData/MaxMindCitiesCsv2/GeoLite2-City-Blocks.csv"))
                //{
                //    locations = maxMindCitiesCsv2Provider.GetIPRangeLocations(stream);
                //}

                var locationCount = locations.Count();

                Console.WriteLine();
                Console.WriteLine("{0} locations were read", locationCount);

                await databaseManager.AddIPRangesLocationRangeAsync(locations, new Progress<int>(x => Console.Write("\r{0}/{1} locations were added", x, locationCount)));
                Console.WriteLine();

                var location = await geoLocationService.LocateAsync(IPAddress.Parse("137.116.229.81"));
                Console.WriteLine(JsonConvert.SerializeObject(location));
            }
            catch (AggregateException aggr)
            {
                foreach (var ex in aggr.InnerExceptions)
                {
                    Console.WriteLine(ex);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            Console.ReadLine();
        }
    }
}
