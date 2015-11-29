using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SillyGeo.ConsoleDemo
{
    public class ContentFileInfo
    {
        public DateTime ModificationDate { get; set; }

        public string FileName { get; set; }

        public Uri Url { get; set; }

        public long Length { get; set; }
    }

    public class GeoNamesContentHelper
    {
        private readonly Uri DumpPageUrl = new Uri("http://download.geonames.org/export/dump/");

        public async Task<IEnumerable<ContentFileInfo>> GetContentUrlsAsync()
        {
            using (var client = new HttpClient())
            {
                var dumpPage = await client.GetStringAsync(DumpPageUrl);

                var matches = Regex.Matches(dumpPage, @"alt=""\[[^\]]*\]"">\s*<a href=""(?<url>[^""]*)"">[^""]+<\/a>\s*(?<date>\d{4}-\d{2}-\d{2} \d{2}:\d{2})\s*(?<length>\w+(\.\w+)?)\s+[^<]*<");
                var cestZone = TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time");

                return matches.Cast<Match>().Select(x => new ContentFileInfo
                {
                    ModificationDate = TimeZoneInfo.ConvertTime(DateTime.Parse(x.Groups["date"].Value, CultureInfo.InvariantCulture), cestZone),
                    FileName = x.Groups["url"].Value,
                    Url = new Uri(DumpPageUrl, x.Groups["url"].Value),
                    Length = ParseFileLengthInBytes(x.Groups["length"].Value),
                }).ToList();
            }
        }

        private static long ParseFileLengthInBytes(string value)
        {
            var unit = value[value.Length - 1];
            if (char.IsDigit(unit))
            {
                return (long)Math.Ceiling(double.Parse(value, CultureInfo.InvariantCulture));
            }
            else
            {
                double quant = double.Parse(value.Substring(0, value.Length - 1), CultureInfo.InvariantCulture);
                switch (char.ToLowerInvariant(unit))
                {
                    case 'k':
                        return (long)Math.Ceiling(quant * Math.Pow(1024, 1));
                    case 'm':
                        return (long)Math.Ceiling(quant * Math.Pow(1024, 2));
                    case 'g':
                        return (long)Math.Ceiling(quant * Math.Pow(1024, 3));
                    default:
                        throw new NotImplementedException();
                }
            }
        }
    }
}
