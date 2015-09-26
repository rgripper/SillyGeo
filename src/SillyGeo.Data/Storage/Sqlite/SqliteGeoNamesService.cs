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

        private IDictionary<string, Country> _countries = null; // TODO: improve

        public async Task<Country> GetCountryAsync(string code)
        {
            if (_countries == null)
            {
                _countries = (await _dbHelper.ExecuteReaderAsync(
                    CreateCountry,
                    @"SELECT * FROM Areas WHERE Kind={0} LIMIT 1;",
                    AreaKind.Country).ConfigureAwait(false)).ToDictionary(x => x.Code);
            }

            Country country = null;
            _countries.TryGetValue(code, out country);
            return country;
        }

        public async Task<PopulatedPlace> GetNearestPopulatedPlaceAsync(Coordinates coordinates)
        {
            var distance = 300;
            var results = await _dbHelper.ExecuteReaderAsync(CreatePopulatedPlace, $@"
                SELECT *, X(Coordinates) AS Latitude, Y(Coordinates) AS Longitude
                FROM Areas
                WHERE Kind={{0}} AND ST_Distance(Coordinates, {_dbHelper.PointParameterCall(1 , 2)}) < {{3}}
                LIMIT 1;", AreaKind.PopulatedPlace, coordinates.Latitude, coordinates.Longitude, distance);

            return results.SingleOrDefault();// TODO
        }

        private void FillArea(DbDataReader dataReader, Area area)
        {
            area.Id = Convert.ToInt32(dataReader["Id"]);
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
                CountryId = Convert.ToInt32(dataReader["CountryId"]),
                AdminAreaLevel1Id = ConvertToNullableInt32(dataReader["AdminAreaLevel1Id"]),
                AdminAreaLevel2Id = ConvertToNullableInt32(dataReader["AdminAreaLevel2Id"])
            };

            FillArea(dataReader, populatedPlace);
            return populatedPlace;
        }

        private int? ConvertToNullableInt32(object value)
        {
            if (value == DBNull.Value)
            {
                return null;
            }

            return Convert.ToInt32(value);
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
