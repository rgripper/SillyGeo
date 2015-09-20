using SillyGeo.Infrastructure;
using SillyGeo.Infrastructure.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Threading.Tasks;

namespace SillyGeo.Data.Providers
{
    public class MaxMindCitiesCsv2Provider 
    {
        private readonly IGeoNamesService GeoNamesService;

        //private int _recordCount;

        //private int _currentCount = 0;

        private Stream _csvStream;

        public MaxMindCitiesCsv2Provider(Stream csvStream, IGeoNamesService geoNamesService)
        {
            GeoNamesService = geoNamesService;
            _csvStream = csvStream;
        }
        
        public async Task<IEnumerable<IPRangeLocation>> GetIPRangeLocationsAsync()
        {
            List<IPRangeLocation> locations = new List<IPRangeLocation>();
            using (var reader = new StreamReader(_csvStream))
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    var parts = line.Split(',');
                    if (string.IsNullOrEmpty(parts[3])) continue;
                    var location = await ParseLocationAsync(parts).ConfigureAwait(false);
                    locations.Add(location);
                }
            }
            return locations;
        }

        private async Task<IPRangeLocation> ParseLocationAsync(string[] parts)
        {
            var item = new
            {
                IPAddress = IPAddress.Parse(parts[0]),
                NetworkPrefixLength = int.Parse(parts[1]),
                GeonameId = string.IsNullOrEmpty(parts[2]) ? (int?)null : int.Parse(parts[2]),
                IsCountryOnly = string.IsNullOrEmpty(parts[2]) || parts[2] == parts[3],
                RegisteredCountryGeonameId = int.Parse(parts[3]),
                //RepresentedCountryGeonameId = int.Parse(parts[4]),
                Coordinates = string.IsNullOrEmpty(parts[6]) ? null : new Coordinates
                    {
                        Longitude = double.Parse(parts[7], CultureInfo.InvariantCulture),
                        Latitude = double.Parse(parts[6], CultureInfo.InvariantCulture)
                    }
            };

            return new IPRangeLocation
            {
                Coordinates = item.IsCountryOnly 
                    ? null 
                    : await GetPopulatedPlaceCoordinatesAsync(item.GeonameId) ?? item.Coordinates,
                IPRange = new IPRange
                {
                    Start = item.IPAddress.MapToIPv4(),
                    End = GetEndAddress(item.IPAddress, item.NetworkPrefixLength).MapToIPv4()
                },
                AreaId = item.GeonameId ?? item.RegisteredCountryGeonameId
            };
        }

        private async Task<Coordinates> GetPopulatedPlaceCoordinatesAsync(int? geoNameId)
        {
            //_currentCount++;
            //Console.Write("\rProcessed: {0:0.00}%", (_currentCount / (double)_recordCount) * 100);
            if (geoNameId.HasValue)
            {
                var place = (PopulatedPlace)await GeoNamesService.GetAreaAsync(geoNameId.Value).ConfigureAwait(false);
                if (place != null)
                {
                    return place.Coordinates;
                }
            }

            return null;
        }

        private static IPAddress GetEndAddress(IPAddress startAddress, int networkPrefixLength)
        {
            var addressBytes = startAddress.GetAddressBytes();
            var suffixLength = addressBytes.Length * 8 - networkPrefixLength;
            var suffixValue = (new BigInteger(1) << suffixLength) - 1;
            var suffixBytes = suffixValue.ToByteArray();
            for (int i = 0; i < suffixBytes.Length; i++)
            {
                addressBytes[addressBytes.Length - i - 1] |= suffixBytes[i];
            }
            return new IPAddress(addressBytes);
        }

    }
}
