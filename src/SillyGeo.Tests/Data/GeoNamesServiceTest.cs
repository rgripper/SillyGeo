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
    public abstract class GeoNamesServiceTest
    {
        public abstract IGeoNamesService GeoNamesService { get; }

        [Fact]
        public void MustGetArea()
        {

        }

        [Fact]
        public void MustGetCountry()
        {

        }

        [Fact]
        public void MustGetPopulatedPlaceAddress()
        {

        }

        [Fact]
        public void MustGetNearestPopulatedPlace()
        {

        }
    }
}
