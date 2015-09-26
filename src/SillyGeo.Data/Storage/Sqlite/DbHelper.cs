using Microsoft.Data.Sqlite;
using SillyGeo.Infrastructure;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace SillyGeo.Data.Storage.Sqlite
{
    internal class DbHelper
    {
        private readonly string _connectionString;

        private readonly int _srid;

        [DllImport("sqlite3", EntryPoint = "sqlite3_enable_load_extension", CallingConvention = CallingConvention.Cdecl)]
        private static extern int EnableLoadExtension(IntPtr db, int onoff);

        public DbHelper(string connectionString, int srid = 4326)
        {
            _connectionString = connectionString;
            _srid = srid;
        }

        public string PointParameterCall(int latIndex, int lonIndex)
        {
            return $"MAKEPOINT({{{latIndex}}}, {{{lonIndex}}}, {_srid})";
        }

        private string PointRawParameterCall(string latName, string lonName, int srid)
        {
            return $"MAKEPOINT({latName}, {lonName}, {srid})";
        }

        public async Task<int> ExecuteNonQueryAsync(string query, params object[] parameters)
        {
            using (DbConnection connection = OpenConnection())
            {
                var command = CreateCommand(connection, query, parameters);
                return await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        public async Task ExecuteBatchAsync(IEnumerable<KeyValuePair<string, object[]>> queries)
        {
            using (DbConnection connection = OpenConnection())
            {
                await CreateCommand(connection, "BEGIN TRANSACTION", new object[0]).ExecuteNonQueryAsync().ConfigureAwait(false);
                foreach (var item in queries)
                {
                    var command = CreateCommand(connection, item.Key, item.Value);
                    await command.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
                await CreateCommand(connection, "END TRANSACTION", new object[0]).ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        public async Task<bool> CreateDatabaseIfNotExistsAsync()
        {
            var connStringBuilder = new SqliteConnectionStringBuilder(_connectionString);
            if (!System.IO.File.Exists(connStringBuilder.DataSource))
            {
                System.IO.File.Create(connStringBuilder.DataSource).Dispose();
                using (DbConnection connection = OpenConnection())
                {
                    await CreateCommand(connection, "BEGIN; SELECT InitSpatialMetaData(); COMMIT;").ExecuteNonQueryAsync();
                }
                return true;
            }
            return false;
        }

        private SqliteConnection OpenConnection()
        {
            var connection = new SqliteConnection(_connectionString);
            connection.Open();
            EnableLoadExtension(connection.Handle, 1);
            var loadExtensionCmd = connection.CreateCommand();
            loadExtensionCmd.CommandText = "SELECT load_extension('mod_spatialite.dll');";
            loadExtensionCmd.ExecuteNonQuery();
            return connection;
        }

        public async Task<IEnumerable<T>> ExecuteReaderAsync<T>(Func<DbDataReader, T> reader, string query, params object[] parameters)
        {
            using (DbConnection connection = OpenConnection())
            {
                var command = CreateCommand(connection, query, parameters);

                List<T> results = new List<T>();
                using (var dataReader = await command.ExecuteReaderAsync().ConfigureAwait(false))
                {
                    while (await dataReader.ReadAsync().ConfigureAwait(false))
                    {
                        results.Add(reader(dataReader));
                    }
                }
                return results;
            }
        }

        private DbCommand CreateCommand(DbConnection connection, string query, params object[] parameters)
        {
            var command = connection.CreateCommand();

            List<string> parametersCalls = new List<string>();
            int i = 0;
            foreach (var parameterValue in parameters)
            {
                var coordinates = parameterValue as Coordinates;
                if (coordinates != null)
                {
                    var latName = $"@p{i}_lat";
                    var lonName = $"@p{i}_lon";
                    parametersCalls.Add(PointRawParameterCall(latName, lonName, _srid));
                    command.Parameters.Add(new SqliteParameter(latName, coordinates.Latitude));
                    command.Parameters.Add(new SqliteParameter(lonName, coordinates.Longitude));
                }
                else
                {
                    var parameterName = "@p" + i;
                    parametersCalls.Add(parameterName);
                    command.Parameters.Add(new SqliteParameter(parameterName, parameterValue ?? DBNull.Value));
                }

                i++;
            }

            command.CommandText = string.Format(query, parametersCalls.ToArray());
            return command;
        }
    }

    internal static class DbHelperExtensions
    {
        public static async Task ExecuteBatchAsync(this DbHelper helper, IEnumerable<KeyValuePair<string, object[]>> queries, int count, IProgress<int> progress)
        {
            int i = 0;
            foreach (var item in Batch(queries, count))
            {
                i += item.Count();
                await helper.ExecuteBatchAsync(item).ConfigureAwait(false);
                progress?.Report(i);
            }
        }

        private static IEnumerable<IEnumerable<T>> Batch<T>(IEnumerable<T> items,
                                                   int maxItems)
        {
            return items.Select((item, inx) => new { item, inx })
                        .GroupBy(x => x.inx / maxItems)
                        .Select(g => g.Select(x => x.item));
        }
    }
}
