using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using SillyGeo.Data.Storage.Sqlite.Models;
using SillyGeo.Infrastructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SillyGeo.Data.Storage.Sqlite
{
    public class DatabaseManager : IDatabaseManager
    {
        private readonly string _connectionString;

        private readonly string _spatialiteExtensionName = "mod_spatialite.dll";

        private readonly int _batchLength = 10000;

        private readonly Dictionary<AreaKind, string> _insertQueries = new Dictionary<AreaKind, string>
        {
            [AreaKind.PopulatedPlace] = @"
            INSERT INTO Areas (Id, Name, NamesByCultures, Kind, AdminAreaLevel1Id, AdminAreaLevel2Id, CountryId, Coordinates) 
            VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, MAKEPOINT({7}, {8}, 4326))",
            [AreaKind.AdminArea] = @"
            INSERT INTO Areas (Id, Name, NamesByCultures, Kind) 
            VALUES ({0}, {1}, {2}, {3})",
            [AreaKind.Country] = @"
            INSERT INTO Areas (Id, Name, NamesByCultures, Kind, Code) 
            VALUES ({0}, {1}, {2}, {3}, {4})"
        };

        public DatabaseManager(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task ClearAreasAsync()
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                await connection.ExecuteNonQueryAsync("DELETE FROM Areas;").ConfigureAwait(false);
            }
        }

        public async Task ClearIPRangesAsync()
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                await connection.ExecuteNonQueryAsync("DELETE FROM IPRangeInfos;").ConfigureAwait(false);
            }
        }

        public async Task AddAreaRangeAsync(IEnumerable<Area> areas, IProgress<int> progress)
        {
            if (areas == null)
            {
                throw new ArgumentNullException(nameof(areas));
            }

            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                connection.LoadExtension(_spatialiteExtensionName);

                var parameterSeries = areas.Select(area =>
                {
                    var populatedPlace = area as PopulatedPlace;
                    if (populatedPlace != null)
                    {
                        return new KeyValuePair<AreaKind, object[]>(AreaKind.PopulatedPlace, new object[]
                        {
                        area.Id,
                        area.Name,
                        area.NamesByCultures == null ? null : JsonConvert.SerializeObject(area.NamesByCultures),
                        AreaKind.PopulatedPlace,
                        populatedPlace.AdminAreaLevel1Id,
                        populatedPlace.AdminAreaLevel2Id,
                        populatedPlace.CountryId,
                        populatedPlace.Coordinates.Latitude,
                        populatedPlace.Coordinates.Longitude
                        });
                    }
                    else
                    {
                        var country = area as Country;
                        if (country != null)
                        {
                            return new KeyValuePair<AreaKind, object[]>(AreaKind.Country, new object[]
                            {
                            area.Id,
                            area.Name,
                            area.NamesByCultures == null ? null : JsonConvert.SerializeObject(area.NamesByCultures),
                            AreaKind.Country,
                            country.Code
                            });
                        }
                        else
                        {
                            return new KeyValuePair<AreaKind, object[]>(AreaKind.AdminArea, new object[]
                            {
                            area.Id,
                            area.Name,
                            area.NamesByCultures == null ? null : JsonConvert.SerializeObject(area.NamesByCultures),
                            AreaKind.AdminArea
                            });
                        }
                    }
                });

                foreach (var parameterSeriesByAreaKind in parameterSeries.GroupBy(x => x.Key))
                {
                    await connection.ExecuteBatchWithoutJournalAsync(_insertQueries[parameterSeriesByAreaKind.Key],
                        parameterSeriesByAreaKind.Select(x => x.Value), _batchLength, progress).ConfigureAwait(false);
                }
            }
        }

        public async Task AddIPRangesLocationRangeAsync(IEnumerable<IPRangeLocation> ipRangeLocations, IProgress<int> progress)
        {
            if (ipRangeLocations == null)
            {
                throw new ArgumentNullException(nameof(ipRangeLocations));
            }

            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                var query = @"
                    INSERT INTO IPRangeInfos (AreaId, StartLow, StartHigh, EndLow, EndHigh) 
                    VALUES ({0}, {1}, {2}, {3}, {4});";
                var parameterSeries = ipRangeLocations.Select(item =>
                {
                    var startIP = new FlatIPAddress(item.IPRange.Start);
                    var endIP = new FlatIPAddress(item.IPRange.End);
                    return new object[] { item.AreaId, startIP.Low, startIP.High, endIP.Low, endIP.High };
                });

                await connection.ExecuteBatchWithoutJournalAsync(query, parameterSeries, _batchLength, progress).ConfigureAwait(false);
            }
        }

        public async Task CreateDatabaseIfNotExistsAsync()
        {
            var connectionStringBuilder = new SqliteConnectionStringBuilder(_connectionString);
            var dbExists = File.Exists(connectionStringBuilder.DataSource);
            if(!dbExists)
            {
                await CreateDatabaseAsync();
            }
        }

        public async Task DropDatabaseAsync()
        {
            var connectionStringBuilder = new SqliteConnectionStringBuilder(_connectionString);
            File.Delete(connectionStringBuilder.DataSource);
        }

        private async Task CreateDatabaseAsync()
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                connection.LoadExtension(_spatialiteExtensionName);
                await connection.CreateCommand("BEGIN; SELECT InitSpatialMetaData(); COMMIT;").ExecuteNonQueryAsync();
                await connection.ExecuteNonQueryAsync(@"
                    CREATE TABLE IF NOT EXISTS Areas (
                        Id INTEGER NOT NULL PRIMARY KEY ASC, 
                        Name TEXT NOT NULL, 
                        NamesByCultures TEXT,
                        Kind INTEGER NOT NULL,
                        Code TEXT,
                        AdminAreaLevel1Id INTEGER,
                        AdminAreaLevel2Id INTEGER,
                        CountryId INTEGER
                    );
                    CREATE INDEX IX_Area_Kind ON Areas (Kind);
                    SELECT AddGeometryColumn('Areas', 'Coordinates', 4326, 'POINT', 'XY');
                    SELECT CreateSpatialIndex('Areas', 'Coordinates');
                    ").ConfigureAwait(false);

                await connection.ExecuteNonQueryAsync(@"
                    CREATE TABLE IF NOT EXISTS IPRangeInfos (
                        AreaId INTEGER NOT NULL,
                        StartLow INTEGER NOT NULL,
                        StartHigh INTEGER,
                        EndLow INTEGER NOT NULL,
                        EndHigh INTEGER
                    );
                    ").ConfigureAwait(false);
            }
        }
    }
}
