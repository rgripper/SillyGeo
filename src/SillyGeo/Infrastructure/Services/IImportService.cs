using System.Collections.Generic;
using System.Threading.Tasks;
using SillyGeo.Infrastructure;
using System;

namespace SillyGeo
{
    public interface IImportService : IDisposable
    {
        Task AddAreaRangeAsync(IEnumerable<Area> areas, IProgress<int> progress = null);
        Task AddIPRangeLocationRangeAsync(string providerName, IEnumerable<IPRangeLocation> ipRangeLocations, IProgress<int> progress = null);
        Task ClearAreasAsync();
        Task ClearIPRangeLocationsAsync();
    }
}