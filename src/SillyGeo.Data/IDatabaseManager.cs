using System.Collections.Generic;
using System.Threading.Tasks;
using SillyGeo.Infrastructure;
using System;

namespace SillyGeo.Data
{
    public interface IDatabaseManager
    {
        Task AddAreaRangeAsync(IEnumerable<Area> areas, IProgress<int> progress = null);
        Task AddIPRangesLocationRangeAsync(string providerName, IEnumerable<IPRangeLocation> ipRangeLocations, IProgress<int> progress = null);
        Task DropDatabaseAsync();
        Task ClearAreasAsync();
        Task ClearIPRangesAsync();
        Task CreateDatabaseIfNotExistsAsync();
    }
}