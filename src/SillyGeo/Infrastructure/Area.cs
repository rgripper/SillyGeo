using System.Collections.Generic;

namespace SillyGeo.Infrastructure
{
    public class Area : ILocalizedEntity
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public Dictionary<string, string> NamesByCultures { get; set; }
    }
}
