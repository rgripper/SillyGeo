using SillyGeo.Infrastructure;
using SillyGeo.Infrastructure.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Runtime;
using System.Threading.Tasks;

namespace SillyGeo.Data.Providers
{
    public class MaxMindCitiesCsv2Provider 
    {
        private readonly IGeoNamesService _geoNamesService;

        public MaxMindCitiesCsv2Provider(IGeoNamesService geoNamesService)
        {
            _geoNamesService = geoNamesService;
        }
        
        public IEnumerable<IPRangeLocation> GetIPRangeLocations(Stream csvStream)
        {
            List<IPRangeLocation> locations = new List<IPRangeLocation>();
            using (var reader = new StreamReader(csvStream))
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    if (line.StartsWith("network_start_ip")) continue; // header
                    var parts = line.Split(',');
                    if (string.IsNullOrEmpty(parts[3])) continue;
                    locations.Add(ParseLocation(parts));
                }
            }
            return locations;
        }

        private IPRangeLocation ParseLocation(string[] parts)
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

            if (!item.IsCountryOnly && item.Coordinates == null)
            {
                throw new Exception("Coordinates are missing");
            }

            return new IPRangeLocation
            {
                Coordinates = item.Coordinates,
                IPRange = new IPRange
                {
                    Start = item.IPAddress.MapToIPv4(),
                    End = GetEndAddress(item.IPAddress, item.NetworkPrefixLength).MapToIPv4()
                },
                AreaId = item.GeonameId ?? item.RegisteredCountryGeonameId
            };
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
