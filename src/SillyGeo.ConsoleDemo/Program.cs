using Microsoft.Dnx.Runtime;
using Microsoft.Framework.Configuration;
using Microsoft.Framework.DependencyInjection;
using SillyGeo.Data.Storage.Sqlite;
using SillyGeo.Infrastructure;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

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

        private async Task Main(string[] args)
        {
            try
            {
                var connectionString = _config["Data:DefaultConnection:ConnectionString"];
                var geoNamesService = new SqliteGeoNamesService(connectionString);
                var databaseManager = new DatabaseManager(connectionString);
                await databaseManager.CreateDatabaseIfNotExistsAsync();
                await databaseManager.ClearAsync();
                await databaseManager.AddAreaRangeAsync(new List<Area> { new Area { Id = 1, Name = "Moo", NamesByCultures = null } });
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
