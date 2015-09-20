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
    public class SqliteGeoNamesService : IGeoNamesService
    {
        private readonly DbHelper _dbHelper;

        public SqliteGeoNamesService(string connectionString)
        {
            _dbHelper = new DbHelper(connectionString);
        }

        public async Task<Area> GetAreaAsync(int id)
        {
            var results = await _dbHelper.ExecuteReaderAsync(dataReader =>
            {
                var areaKind = (AreaKind)dataReader["Kind"];
                switch (areaKind)
                {
                    case AreaKind.Country:
                        return CreateCountry(dataReader);
                    case AreaKind.AdminArea:
                        return CreateAdminArea(dataReader);
                    case AreaKind.PopulatedPlace:
                        return CreatePopulatedPlace(dataReader);
                    default:
                        throw new NotSupportedException();
                }
            },
            @"SELECT * FROM Areas WHERE Id={0} LIMIT 1;", id).ConfigureAwait(false);

            return results.SingleOrDefault();
        }

        public async Task<string> GetPopulatedPlaceAddressAsync(int id, string cultureName)
        {
            var entityPath = new List<ILocalizedEntity>();
            var place = (PopulatedPlace)(await GetAreaAsync(id));
            entityPath.Add(place);
            if (place.AdminAreaLevel2Id.HasValue)
            {
                entityPath.Add(await GetAreaAsync(place.AdminAreaLevel2Id.Value));
            }
            if (place.AdminAreaLevel1Id.HasValue)
            {
                entityPath.Add(await GetAreaAsync(place.AdminAreaLevel1Id.Value));
            }

            return string.Join(", ", entityPath
                .Select(x => x.GetLocalizedName(cultureName))
                .Where(x => x != null));
        }

        public async Task<Country> GetCountryAsync(string code)
        {
            var results = await _dbHelper.ExecuteReaderAsync(
                CreateCountry,
                @"SELECT * FROM Areas WHERE Code={0} LIMIT 1;",
                code).ConfigureAwait(false);

            return results.SingleOrDefault();
        }

        public async Task<PopulatedPlace> GetNearestPopulatedPlaceAsync(Coordinates coordinates)
        {
            var distance = 0.3;
            var query = await _dbHelper.ExecuteReaderAsync(CreatePopulatedPlace, @"
                SELECT *
                FROM Areas
                WHERE ST_Distance(Coordinates, MakePoint({0}, {1})) < {2}
                LIMIT 1;", coordinates.Latitude, coordinates.Longitude, distance);

            if (coordinates == null)
            {
                throw new ArgumentNullException("coordinates");
            }

            return new PopulatedPlace { Name = "Test" };// TODO
        }

        //private static U GetValue<T, U>(IDictionary<T, U> dict, T key)
        //    where U : class
        //{
        //    U val;
        //    dict.TryGetValue(key, out val);
        //    return val;
        //}

        private void FillArea(DbDataReader dataReader, Area area)
        {
            area.Id = (int)dataReader["Id"];
            area.Name = (string)dataReader["Name"];
            var namesByCultures = (string)dataReader["NamesByCultures"];
            if (namesByCultures != null)
            {
                area.NamesByCultures = JsonConvert.DeserializeObject<Dictionary<string, string>>(namesByCultures);
            }
        }

        private PopulatedPlace CreatePopulatedPlace(DbDataReader dataReader)
        {
            var populatedPlace = new PopulatedPlace
            {
                Coordinates = new Coordinates { Latitude = (double)dataReader["Latitude"], Longitude = (double)dataReader["Longitude"] },
                CountryId = (int)dataReader["CountryId"],
                AdminAreaLevel1Id = (int)dataReader["AdminAreaLevel1Id"],
                AdminAreaLevel2Id = (int)dataReader["AdminAreaLevel2Id"]
            };

            FillArea(dataReader, populatedPlace);
            return populatedPlace;
        }

        private Country CreateCountry(DbDataReader dataReader)
        {
            var country = new Country
            {
                Code = (string)dataReader["Code"]
            };

            FillArea(dataReader, country);
            return country;
        }

        private Area CreateAdminArea(DbDataReader dataReader)
        {
            var area = new Area();
            FillArea(dataReader, area);
            return area;
        }
    }
}
