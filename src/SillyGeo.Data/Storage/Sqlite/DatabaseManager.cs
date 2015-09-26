using Newtonsoft.Json;
using SillyGeo.Data.Storage.Sqlite.Models;
using SillyGeo.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SillyGeo.Data.Storage.Sqlite
{
    public class DatabaseManager : IDatabaseManager
    {
        private readonly DbHelper _dbHelper;

        private readonly int _batchLength = 1000;

        public DatabaseManager(string connectionString)
        {
            _dbHelper = new DbHelper(connectionString);
        }

        public async Task AddIPRangesLocationRangeAsync(IEnumerable<IPRangeLocation> ipRangeLocations, IProgress<int> progress)
        {
            var queries = ipRangeLocations.Select(item =>
            {
                var startIP = new FlatIPAddress(item.IPRange.Start);
                var endIP = new FlatIPAddress(item.IPRange.End);
                return new KeyValuePair<string, object[]>(@"
                    INSERT INTO IPRangeInfos (AreaId, StartLow, StartHigh, EndLow, EndHigh) 
                    VALUES ({0}, {1}, {2}, {3}, {4});",
                    new object[] { item.AreaId, startIP.Low, startIP.High, endIP.Low, endIP.High });
            }).ToList();
            await _dbHelper.ExecuteBatchAsync(queries, _batchLength, progress).ConfigureAwait(false);
        }

        public async Task ClearAreasAsync()
        {
            await _dbHelper.ExecuteNonQueryAsync("DELETE FROM Areas;").ConfigureAwait(false);
        }

        public async Task ClearIPRangesAsync()
        {
            await _dbHelper.ExecuteNonQueryAsync("DELETE FROM IPRangeInfos;").ConfigureAwait(false);
        }

        public async Task AddAreaRangeAsync(IEnumerable<Area> areas, IProgress<int> progress)
        {
            if (areas == null)
            {
                throw new ArgumentNullException("areas");
            }

            var entitiesParameters = areas.Select(area =>
            {
                SortedDictionary<string, object> parameters = new SortedDictionary<string, object>();
                parameters["Id"] = area.Id;
                parameters["Name"] = area.Name;
                parameters["NamesByCultures"] = area.NamesByCultures == null ? null : JsonConvert.SerializeObject(area.NamesByCultures);
                var populatedPlace = area as PopulatedPlace;
                if (populatedPlace != null)
                {
                    parameters["AdminAreaLevel1Id"] = populatedPlace.AdminAreaLevel1Id;
                    parameters["AdminAreaLevel2Id"] = populatedPlace.AdminAreaLevel2Id;
                    parameters["CountryId"] = populatedPlace.CountryId;
                    parameters["Coordinates"] = populatedPlace.Coordinates;

                    parameters["Kind"] = AreaKind.PopulatedPlace;
                }
                else
                {
                    var country = area as Country;
                    if (country != null)
                    {
                        parameters["Code"] = country.Code;

                        parameters["Kind"] = AreaKind.Country;
                    }
                    else
                    {
                        parameters["Kind"] = AreaKind.AdminArea;
                    }
                }

                return parameters;
            });

            var queries = entitiesParameters.Select(parameters =>
            {
                var paramNames = string.Join(",", parameters.Select(x => x.Key));
                var paramValues = parameters.Select(x => x.Value).ToArray();
                var paramIndexes = string.Join(",", Enumerable.Range(0, parameters.Count).Select(x => "{" + x + "}"));

                var paramValuesPlaceholders = string.Join(",", Enumerable.Range(0, parameters.Count));
                return new KeyValuePair<string, object[]>($"INSERT INTO Areas ({paramNames}) VALUES ({paramIndexes});", paramValues);
            }).ToList();

            await _dbHelper.ExecuteNonQueryAsync("PRAGMA journal_mode = OFF");
            await _dbHelper.ExecuteBatchAsync(queries, _batchLength, progress).ConfigureAwait(false);
            await _dbHelper.ExecuteNonQueryAsync("PRAGMA journal_mode = NORMAL");
        }

        public async Task CreateDatabaseIfNotExistsAsync()
        {
            if (await _dbHelper.CreateDatabaseIfNotExistsAsync())
            {
                await _dbHelper.ExecuteNonQueryAsync(@"
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

                await _dbHelper.ExecuteNonQueryAsync(@"
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
