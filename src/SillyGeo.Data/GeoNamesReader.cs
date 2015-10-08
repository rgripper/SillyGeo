using SillyGeo.Infrastructure;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace SillyGeo.Data
{
    public class GeoNamesReader
    {
        private class AdminAreaInfo
        {
            public int Id { get; set; }

            public string Name { get; set; }

            public string Code { get; set; }

            public string CountryCode { get; set; }
        }

        public IEnumerable<Area> ReadAreas(string localizedNamesPath, 
            string admin1Path, string admin2Path, string citiesPath, string contriesPath)
        {
            var localizedNames = ReadLocalizedNames(localizedNamesPath);

            var countries = ReadCountries(contriesPath, localizedNames);
            var countryIdsByCodes = countries.ToDictionary(x => x.Code, x => x.Id);

            var admin1IdsByCodes = ReadAdminAreaInfosByCodes(admin1Path);
            var admin2IdsByCodes = ReadAdminAreaInfosByCodes(admin2Path);

            var adminAreas = admin1IdsByCodes.Values.Concat(admin2IdsByCodes.Values)
                .Select(x => new AdminArea 
                { 
                    Id = x.Id, 
                    Name = x.Name, 
                    NamesByCultures = GetValue(localizedNames, x.Id) ?? new Dictionary<string, string>(),
                    CountryId = countryIdsByCodes[x.CountryCode]
                });

            var cities = ReadCities(citiesPath, localizedNames, countryIdsByCodes, admin1IdsByCodes, admin2IdsByCodes);

            return countries.Concat<Area>(adminAreas).Concat(cities);
        }

        private IEnumerable<PopulatedPlace> ReadCities(string path, IDictionary<int, Dictionary<string, string>> localizedNamesByIds,
            IDictionary<string, int> countriesByCodes, IDictionary<string, AdminAreaInfo> admin1IdsByCodes, IDictionary<string, AdminAreaInfo> admin2IdsByCodes)
        {
            Func<int, IEnumerable<string>, Dictionary<string, string>> getLocalizedNames = (id, alternateNames) =>
            {
                Dictionary<string, string> localizedNames;
                if (localizedNamesByIds.TryGetValue(id, out localizedNames))
                {
                    return localizedNames;
                }
                else
                {
                    return new Dictionary<string, string>();
                }
            };

            var results =
                from line in File.ReadLines(path)
                let parts = line.Split('\t')
                let id = int.Parse(parts[0])
                let alternateNames = parts[3].Split(',')
                let adm1Code = parts[8] + "." + parts[10]
                let adm2Code = adm1Code + "." + parts[11]
                select new PopulatedPlace
                {
                    Id = id,
                    Name = parts[1],
                    CountryId = countriesByCodes[parts[8]],
                    Coordinates = new Coordinates
                    {
                        Latitude = double.Parse(parts[4], CultureInfo.InvariantCulture),
                        Longitude = double.Parse(parts[5], CultureInfo.InvariantCulture)
                    },
                    NamesByCultures = getLocalizedNames(id, alternateNames),
                    AdminAreaLevel1Id = GetValue(admin1IdsByCodes, adm1Code)?.Id,
                    AdminAreaLevel2Id = GetValue(admin2IdsByCodes, adm2Code)?.Id
                };

            return results.ToList();
        }

        private IDictionary<string, AdminAreaInfo> ReadAdminAreaInfosByCodes(string path)
        {
            var results =
                from line in File.ReadLines(path)
                let parts = line.Split('\t')
                select new AdminAreaInfo { Id = int.Parse(parts[3]), Code = parts[0], Name = parts[1], CountryCode = parts[0].Split('.')[0] };

            return results.ToDictionary(x => x.Code);
        }

        private IEnumerable<Country> ReadCountries(string path, Dictionary<int, Dictionary<string, string>> localizedNamesByIds)
        {
            var results =
                from line in File.ReadLines(path)
                where !line.StartsWith("#")
                let parts = line.Split('\t')
                where !string.IsNullOrEmpty(parts[16]) // no longer exist
                let id = int.Parse(parts[16])
                select new Country
                {
                    Id = id,
                    Name = parts[4],
                    Code = parts[0],
                    NamesByCultures = GetLocalizedNames(localizedNamesByIds, id)
                };

            return results.ToList();
        }

        public void ExtractLocalizedNames(string srcFilePath, string destFilePath, params string[] locales)
        {
            var localeSet = new HashSet<string>(locales, StringComparer.OrdinalIgnoreCase);

            var lines =
                from line in File.ReadLines(srcFilePath)
                let parts = line.Split('\t')
                let lineData = new { id = parts[1], locale = parts[2] }
                where localeSet.Contains(lineData.locale)
                select line;

            File.WriteAllLines(destFilePath, lines);
        }

        private Dictionary<int, Dictionary<string, string>> ReadLocalizedNames(string filePath)
        {
            var lines =
                from line in File.ReadLines(filePath)
                let parts = line.Split('\t')
                let lineData = new { placeId = int.Parse(parts[1]), locale = parts[2], name = parts[3] }
                select lineData;

            lines = lines.ToList();

            return lines
                .GroupBy(x => x.placeId)
                .ToDictionary(
                    byPlaceId => byPlaceId.Key,
                    byPlaceId => byPlaceId
                        .GroupBy(x => x.locale)
                        .ToDictionary(g => g.Key, g => g.First().name)); // можно улучшить, выбирая не первое, а то, что совпадает с локализованными в списке городов
        }

        private static U GetValue<T, U>(IDictionary<T, U> dict, T key) where U : class
        {
            U val;
            dict.TryGetValue(key, out val);
            return val;
        }

        private static Dictionary<string, string> GetLocalizedNames(Dictionary<int, Dictionary<string, string>> localizedNamesByIds, int id)
        {
            Dictionary<string, string> localizedNames;
            if (localizedNamesByIds.TryGetValue(id, out localizedNames))
            {
                return localizedNames;
            }
            else
            {
                return new Dictionary<string, string>();
            }
        }

    }
}
