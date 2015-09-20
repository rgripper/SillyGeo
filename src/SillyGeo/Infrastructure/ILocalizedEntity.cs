using System.Collections.Generic;

namespace SillyGeo.Infrastructure
{
    public interface ILocalizedEntity
    {
        string Name { get; set; }

        Dictionary<string, string> NamesByCultures { get; set; }
    }
}
