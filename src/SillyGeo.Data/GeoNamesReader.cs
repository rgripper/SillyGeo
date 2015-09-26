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
        public IEnumerable<Area> ReadAreas(string localizedNamesPath, 
            string admin1Path, string admin2Path, string citiesPath, string contriesPath)
        {
            var localizedNames = ReadLocalizedNames(localizedNamesPath);

            var admin1IdsByCodes = ReadAdminIdsByCodes(admin1Path);
            var admin2IdsByCodes = ReadAdminIdsByCodes(admin2Path);

            var admin1NamesByIds = ReadAdminNamesByIds(admin1Path);
            var admin2NamesByIds = ReadAdminNamesByIds(admin2Path);

            var adminAreas = admin1NamesByIds.Concat(admin2NamesByIds)
                .Select(x => new Area 
                { 
                    Id = x.Key, 
                    Name = x.Value, 
                    NamesByCultures = GetValue(localizedNames, x.Key) ?? new Dictionary<string, string>() 
                })
                .ToList();

            var countries = ReadCountries(contriesPath, localizedNames);
            var countriesByCodes = countries.ToDictionary(x => x.Code, x => x.Id);

            var cities = ReadCities(citiesPath, localizedNames, countriesByCodes, admin1IdsByCodes, admin2IdsByCodes);

            return countries.Concat(adminAreas).Concat(cities);
        }

        private IEnumerable<PopulatedPlace> ReadCities(string path, Dictionary<int, Dictionary<string, string>> localizedNamesByIds,
            Dictionary<string, int> countriesByCodes, Dictionary<string, int> admin1IdsByCodes, Dictionary<string, int> admin2IdsByCodes)
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
                    AdminAreaLevel1Id = GetIdByCode(admin1IdsByCodes, adm1Code),
                    AdminAreaLevel2Id = GetIdByCode(admin2IdsByCodes, adm2Code)
                };

            return results.ToList();
        }

        private Dictionary<string, int> ReadAdminIdsByCodes(string path)
        {
            return File.ReadLines(path).Select(x => x.Split('\t')).ToDictionary(x => x[0], x => int.Parse(x[3]));
        }

        private Dictionary<int, string> ReadAdminNamesByIds(string path)
        {
            return File.ReadLines(path).Select(x => x.Split('\t')).ToDictionary(x => int.Parse(x[3]), x => x[1]);
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

        private static int? GetIdByCode(IDictionary<string, int> dict, string key)
        {
            int val;
            if (dict.TryGetValue(key, out val))
                return val;
            return null;
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
