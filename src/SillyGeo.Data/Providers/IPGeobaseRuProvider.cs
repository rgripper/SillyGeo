using SillyGeo.Infrastructure;
using SillyGeo.Infrastructure.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace SillyGeo.Data.Providers
{
    public class IPGeobaseRuProvider
    {
        private class CityLocation
        {
            public Coordinates Coordinates { get; set; }

            public int Id { get; set; }
        }

        private readonly IGeoNamesService _geoNamesService;

        public IPGeobaseRuProvider(IGeoNamesService geoNamesService)
        {
            _geoNamesService = geoNamesService;
        }

        public Task<IEnumerable<IPRangeLocation>> GetIPRangeLocationsAsync(Stream rangesStream, Stream citiesStream)
        {
            var citiesLocationsDict = ParseCitiesLocations(citiesStream);
            return ParseIPRangesAsync(rangesStream, citiesLocationsDict);
        }

        private static Dictionary<int, CityLocation> ParseCitiesLocations(Stream stream)
        {
            var dictionary = new Dictionary<int, CityLocation>();
            using (var reader = new StreamReader(stream))
            {
                while (!reader.EndOfStream)
                {
                    var pair = ParseCityLocation(reader.ReadLine());
                    dictionary.Add(pair.Id, pair);
                }
            }
            return dictionary;
        }

        private static CityLocation ParseCityLocation(string line)
        {
            var parts = line.Split('\t');
            return new CityLocation
            {
                Coordinates = new Coordinates
                {
                    Longitude = double.Parse(parts[5], CultureInfo.InvariantCulture),
                    Latitude = double.Parse(parts[4], CultureInfo.InvariantCulture),
                },
                Id = int.Parse(parts[0], CultureInfo.InvariantCulture)
            };
        }

        private async Task<IEnumerable<IPRangeLocation>> ParseIPRangesAsync(Stream stream, Dictionary<int, CityLocation> citiesLocationsDict)
        {
            List<IPRangeLocation> locations = new List<IPRangeLocation>();
            using (var reader = new StreamReader(stream))
            {
                while (!reader.EndOfStream)
                {
                    var range = await ParseIPRangeAsync(reader.ReadLine(), citiesLocationsDict);
                    locations.Add(range);
                }
            }
            return locations;
        }

        private async Task<IPRangeLocation> ParseIPRangeAsync(string line, Dictionary<int, CityLocation> citiesLocationsDict)
        {
            var parts = line.Split('\t');
            var ipRange = parts[2].Split('-');

            int? cityLocationCode = parts[4] == "-" ? (int?)null : int.Parse(parts[4]);
            var cityLocation = cityLocationCode.HasValue ? citiesLocationsDict[cityLocationCode.Value] : null;

            int? areaId = null;

            if (cityLocation != null)
            {
                var populatedPlace = await _geoNamesService.GetNearestPopulatedPlaceAsync(cityLocation.Coordinates);
                areaId = populatedPlace?.Id;
            }
            else
            {
                string countryCode = parts[3];
                var country = await _geoNamesService.GetCountryAsync(countryCode);
                areaId = country?.Id;
            }

            if (areaId == null)
            {
                return null; // do not allow ipranges with undetermined location
            }

            return new IPRangeLocation
            {
                IPRange = new IPRange
                {
                    Start = IPAddress.Parse(ipRange[0].Trim()),
                    End = IPAddress.Parse(ipRange[1].Trim())
                },
                AreaId = areaId.Value,
                Coordinates = cityLocation?.Coordinates
            };
        }
    }
}
