using Microsoft.Data.Sqlite;
using SillyGeo.Data.Storage.Sqlite.Models;
using SillyGeo.Infrastructure;
using SillyGeo.Infrastructure.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace SillyGeo.Data.Storage.Sqlite
{
    public class SqliteIPGeoLocationService : IIPGeoLocationService
    {
        private readonly string _connectionString;

        public SqliteIPGeoLocationService(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<IEnumerable<IPRangeInfo>> LocateAsync(IPAddress ip)
        {
            var flatIP = new FlatIPAddress(ip);
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                return await connection.ExecuteReaderAsync(dataReader =>
                {
                    return new IPRangeInfo
                    {
                        IPRange = new IPRange
                        {
                            Start = new FlatIPAddress { Low = (long)dataReader["StartLow"], High = (long)dataReader["StartHigh"] }.ToIPAddress(),
                            End = new FlatIPAddress { Low = (long)dataReader["EndLow"], High = (long)dataReader["EndHigh"] }.ToIPAddress(),
                        },
                    };
                }, @"
                    SELECT * FROM IPRangeInfos 
                    WHERE (StartHigh < {1} OR (StartHigh = {1} AND StartLow <= {0})
                    AND (EndHigh > {1} OR (EndHigh = {1} AND EndLow >= {0});",
                flatIP.Low, flatIP.High);
            }
        }

    }
}
