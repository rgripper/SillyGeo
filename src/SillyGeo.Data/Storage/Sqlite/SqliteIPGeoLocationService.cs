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

        public Task<IEnumerable<IPRangeInfo>> LocateAsync(IPAddress ip)
        {
            var isIPv6 = ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6;
            return isIPv6 ? LocateIPv6Async(ip) : LocateIPv4Async(ip);
        }
         
        private async Task<IEnumerable<IPRangeInfo>> LocateIPv4Async(IPAddress ip)
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
                            Start = new FlatIPAddress { Low = (long)dataReader["StartLow"], High = null }.ToIPAddress(),
                            End = new FlatIPAddress { Low = (long)dataReader["EndLow"], High = null }.ToIPAddress(),
                        },
                        AreaId = Convert.ToInt32(dataReader["AreaId"]),
                        ProviderId = Convert.ToInt32(dataReader["ProviderId"]),
                    };
                }, @"
                    SELECT * FROM IPRangeInfos 
                    WHERE StartLow <= {0} AND EndLow >= {0};",
                flatIP.Low);
            }
        }

        private async Task<IEnumerable<IPRangeInfo>> LocateIPv6Async(IPAddress ip)
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
                        AreaId = Convert.ToInt32(dataReader["AreaId"]),
                        ProviderId = Convert.ToInt32(dataReader["ProviderId"]),
                    };
                }, @"
                    SELECT * FROM IPRangeInfos 
                    WHERE (StartHigh < {1} OR (StartHigh = {1} AND StartLow <= {0}))
                    AND (EndHigh > {1} OR (EndHigh = {1} AND EndLow >= {0}));",
                flatIP.Low, flatIP.High);
            }
        }

        public void Dispose()
        {
        }
    }
}
