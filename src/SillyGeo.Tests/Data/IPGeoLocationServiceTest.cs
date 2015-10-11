using SillyGeo.Data;
using SillyGeo.Data.Storage.Sqlite;
using SillyGeo.Infrastructure.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace SillyGeo.Tests.Data
{
    public abstract class IPGeoLocationServiceTest
    {
        public abstract IIPGeoLocationService IPGeoLocationService { get; }

        [Fact]
        public void MustLocate()
        {

        }
    }
}
