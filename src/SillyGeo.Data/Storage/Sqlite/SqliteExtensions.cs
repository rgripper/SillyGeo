using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace SillyGeo.Data.Storage.Sqlite
{
    internal static class SqliteExtensions
    {
        [DllImport("sqlite3", EntryPoint = "sqlite3_enable_load_extension", CallingConvention = CallingConvention.Cdecl)]
        private static extern int EnableLoadExtension(IntPtr db, int onoff);

        public static int LoadExtension(this SqliteConnection connection, string extensionName)
        {
            EnableLoadExtension(connection.Handle, 1);
            return connection.CreateCommand($"SELECT load_extension('{extensionName}');").ExecuteNonQuery();
        }

        public static int ExecutePragma(this SqliteConnection connection, string key, object value)
        {
            var insertedValue = value is string ? string.Concat('"', value, '"') : value.ToString();
            return connection.CreateCommand($"PRAGMA {key} = {insertedValue};").ExecuteNonQuery();
        }

        public static async Task ExecuteBatchWithoutJournalAsync(this SqliteConnection connection, string query, IEnumerable<object[]> parameterSeries, int batchSize, IProgress<int> progress)
        {
            foreach (var batchedParameterSeries in parameterSeries.Batch(batchSize))
            {
                await connection.ExecuteBatchWithoutJournalAsync(query, batchedParameterSeries).ConfigureAwait(false);
                progress?.Report(batchedParameterSeries.Count());
            }
        }

        private static async Task ExecuteBatchWithoutJournalAsync(this SqliteConnection connection, string query, IEnumerable<object[]> parameterSeries)
        {
            connection.ExecutePragma("journal_mode", "OFF");
            await connection.ExecuteBatchAsync(query, parameterSeries);
            connection.ExecutePragma("journal_mode", "DELETE");
        }
    }
}
