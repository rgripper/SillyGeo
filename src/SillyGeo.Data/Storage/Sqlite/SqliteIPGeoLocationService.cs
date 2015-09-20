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
        private readonly IGeoNamesService _geoNamesService;

        public SqliteIPGeoLocationService(IGeoNamesService geoNamesService)
        {
            _geoNamesService = geoNamesService;
        }

        public async Task<IPRangeInfo> LocateAsync(IPAddress ip)
        {
            return null;
        }

    }
}
