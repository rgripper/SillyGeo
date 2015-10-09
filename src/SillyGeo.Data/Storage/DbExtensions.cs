using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SillyGeo.Data.Storage
{
    internal static class DbExtensions
    {
        public static DbCommand CreateCommand(this DbConnection connection, string query, bool prepare = false)
        {
            var command = connection.CreateCommand();
            command.CommandText = Regex.Replace(query, @"{(\d+)}", "@p$1");
            if (prepare)
            {
                command.Prepare();
            }
            return command;
        }

        public static async Task ExecuteBatchAsync(this DbConnection connection, string query, IEnumerable<object[]> parameterSeries)
        {
            using (var tran = connection.BeginTransaction())
            {
                var command = connection.CreateCommand(query, prepare: true);
                foreach (var parameters in parameterSeries)
                {
                    command.ResetParameters(parameters);
                    await command.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
                tran.Commit();
            }
        }

        public static async Task<int> ExecuteNonQueryAsync(this DbConnection connection, string query, params object[] parameters)
        {
            var command = connection.CreateCommand(query);
            command.ResetParameters(parameters);
            return await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        public static async Task<IEnumerable<T>> ExecuteReaderAsync<T>(this DbConnection connection, Func<DbDataReader, T> reader, string query, params object[] parameters)
        {
            var command = connection.CreateCommand(query);
            command.ResetParameters(parameters);

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

        public static async Task<object> ExecuteScalarAsync(this DbConnection connection, string query, params object[] parameters)
        {
            var command = connection.CreateCommand(query);
            command.ResetParameters(parameters);
            return await command.ExecuteScalarAsync();
        }

        private static void ResetParameters(this DbCommand command, object[] parameters)
        {
            command.Parameters.Clear();
            for (int i = 0; i < parameters.Length; i++)
            {
                command.Parameters.Add(new SqliteParameter("@p" + i, parameters[i] ?? DBNull.Value));
            }
        }
    }

    internal static class EnumerableExtensions 
    {
        public static IEnumerable<IEnumerable<T>> Batch<T>(this IEnumerable<T> items,
                               int batchSize)
        {
            return items.Select((item, inx) => new { item, inx })
                        .GroupBy(x => x.inx / batchSize)
                        .Select(g => g.Select(x => x.item));
        }
    }

}
