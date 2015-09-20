using Newtonsoft.Json;
using SillyGeo.Data.Storage.Sqlite.Models;
using SillyGeo.Infrastructure;
using SillyGeo.Infrastructure.Services;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;

namespace SillyGeo.Data.Storage.Sqlite
{
    public class DatabaseManager
    {
        private readonly DbHelper _dbHelper;

        public DatabaseManager(string connectionString)
        {
            _dbHelper = new DbHelper(connectionString);
        }

        public async Task AddIPRangesLocationRangeAsync(IEnumerable<IPRangeLocation> ipRangeLocations)
        {
            foreach (var item in ipRangeLocations)
            {
                var startIP = new FlatIPAddress(item.IPRange.Start);
                var endIP = new FlatIPAddress(item.IPRange.End);
                await _dbHelper.ExecuteNonQueryAsync(@"
                    INSERT INTO Areas (AreaId, StartLow, StartHigh, EndLow, EndHigh) 
                    VALUES ({0}, {1}, {2}, {3}, {4}, {5});",
                    item.AreaId, startIP.Low, startIP.High, endIP.Low, endIP.High).ConfigureAwait(false);
            }
        }

        public async Task ClearAsync()
        {
            await _dbHelper.ExecuteNonQueryAsync("DELETE FROM Areas;").ConfigureAwait(false);
            await _dbHelper.ExecuteNonQueryAsync("DELETE FROM IPRangeInfos;").ConfigureAwait(false);
        }

        public async Task AddAreaRangeAsync(IEnumerable<Area> areas)
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

            foreach (var parameters in entitiesParameters)
            {
                var paramNames = string.Join(",", parameters.Select(x => x.Key));
                var paramValues = parameters.Select(x => x.Value).ToArray();
                var paramIndexes = string.Join(",", Enumerable.Range(0, parameters.Count).Select(x => "{" + x + "}"));

                var paramValuesPlaceholders = string.Join(",", Enumerable.Range(0, parameters.Count));
                await _dbHelper.ExecuteNonQueryAsync($"INSERT INTO Areas ({paramNames}) VALUES ({paramIndexes});", paramValues).ConfigureAwait(false);
            }
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
SELECT AddGeometryColumn('Areas', 'Coordinates', 4326, 'POINT', 'XY');
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
