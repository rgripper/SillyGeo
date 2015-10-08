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
    public class DatabaseManager : IDatabaseManager, IDisposable
    {
        private Lazy<SqliteConnection> _connection;

        private readonly string _spatialiteExtensionName = "mod_spatialite.dll";

        private readonly int _batchLength = 10000;

        private readonly Dictionary<AreaKind, string> _insertQueries = new Dictionary<AreaKind, string>
        {
            [AreaKind.PopulatedPlace] = @"
            INSERT INTO Areas (Id, Name, NamesByCultures, Kind, CountryId, AdminAreaLevel1Id, AdminAreaLevel2Id, Coordinates) 
            VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, MAKEPOINT({7}, {8}, 4326))",
            [AreaKind.AdminArea] = @"
            INSERT INTO Areas (Id, Name, NamesByCultures, Kind, CountryId) 
            VALUES ({0}, {1}, {2}, {3}, {4})",
            [AreaKind.Country] = @"
            INSERT INTO Areas (Id, Name, NamesByCultures, Kind, Code) 
            VALUES ({0}, {1}, {2}, {3}, {4})"
        };

        public DatabaseManager(string connectionString)
        {
            _connection = new Lazy<SqliteConnection>(() => Open(connectionString));
        }

        private SqliteConnection Open(string connectionString)
        {
            var connection = new SqliteConnection(connectionString);
            connection.Open();
            connection.EnableExtensions();
            connection.LoadExtension(_spatialiteExtensionName);
            return connection;
        }

        public async Task ClearAreasAsync()
        {
            await _connection.Value.ExecuteNonQueryAsync("DELETE FROM Areas;").ConfigureAwait(false);
        }

        public async Task ClearIPRangesAsync()
        {
            await _connection.Value.ExecuteNonQueryAsync("DELETE FROM IPRangeInfos;").ConfigureAwait(false);
        }

        public async Task AddAreaRangeAsync(IEnumerable<Area> areas, IProgress<int> progress = null)
        {
            if (areas == null)
            {
                throw new ArgumentNullException(nameof(areas));
            }

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
                        populatedPlace.CountryId,
                        populatedPlace.AdminAreaLevel1Id,
                        populatedPlace.AdminAreaLevel2Id,
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
                        var adminArea = area as AdminArea;
                        return new KeyValuePair<AreaKind, object[]>(AreaKind.AdminArea, new object[]
                        {
                            area.Id,
                            area.Name,
                            area.NamesByCultures == null ? null : JsonConvert.SerializeObject(area.NamesByCultures),
                            AreaKind.AdminArea,
                            adminArea.CountryId
                        });
                    }
                }
            });

            foreach (var parameterSeriesByAreaKind in parameterSeries.GroupBy(x => x.Key))
            {
                await _connection.Value.ExecuteBatchWithoutJournalAsync(_insertQueries[parameterSeriesByAreaKind.Key],
                    parameterSeriesByAreaKind.Select(x => x.Value), _batchLength, progress).ConfigureAwait(false);
            }
        }

        public async Task AddIPRangesLocationRangeAsync(IEnumerable<IPRangeLocation> ipRangeLocations, IProgress<int> progress = null)
        {
            if (ipRangeLocations == null)
            {
                throw new ArgumentNullException(nameof(ipRangeLocations));
            }

            var query = @"
                    INSERT INTO IPRangeInfos (AreaId, StartLow, StartHigh, EndLow, EndHigh, Value) 
                    VALUES ({0}, {1}, {2}, {3}, {4}, {5});";
            var parameterSeries = ipRangeLocations.Select(item =>
            {
                var startIP = new FlatIPAddress(item.IPRange.Start);
                var endIP = new FlatIPAddress(item.IPRange.End);
                return new object[] { item.AreaId, startIP.Low, startIP.High, endIP.Low, endIP.High,
                    startIP.ToIPAddress().ToString() + " - " + endIP.ToIPAddress().ToString() };
            });

            await _connection.Value.ExecuteBatchWithoutJournalAsync(query, parameterSeries, _batchLength, progress).ConfigureAwait(false);
        }

        public async Task CreateDatabaseIfNotExistsAsync()
        {
            //using (var tran = _connection.Value.BeginTransaction())
            //{
                if (!_connection.Value.TableExists("Areas"))
                {
                    await CreateDatabaseAsync(_connection.Value);
                }
                //tran.Commit();
            //}
        }

        public async Task DropDatabaseAsync()
        {
            _connection.Value.Close();
            var connectionString = _connection.Value.ConnectionString;
            File.Delete(_connection.Value.DataSource);
            _connection = new Lazy<SqliteConnection>(() => Open(connectionString));
        }

        private async Task CreateDatabaseAsync(SqliteConnection connection)
        {
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
                CREATE INDEX IF NOT EXISTS IDX_Area_Kind ON Areas (Kind);
                SELECT AddGeometryColumn('Areas', 'Coordinates', 4326, 'POINT', 'XY');
                SELECT CreateSpatialIndex('Areas', 'Coordinates');
                ").ConfigureAwait(false);

            await connection.ExecuteNonQueryAsync(@"
                CREATE TABLE IF NOT EXISTS IPRangeInfos (
                    AreaId INTEGER NOT NULL,
                    StartLow INTEGER NOT NULL,
                    StartHigh INTEGER,
                    EndLow INTEGER NOT NULL,
                    EndHigh INTEGER,
                    Value TEXT
                );
                ").ConfigureAwait(false);
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _connection.Value.Close();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~DatabaseManager() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion
    }
}
