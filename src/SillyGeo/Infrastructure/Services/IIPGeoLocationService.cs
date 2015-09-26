using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace SillyGeo.Infrastructure.Services
{
    public interface IIPGeoLocationService
    {
        Task<IEnumerable<IPRangeInfo>> LocateAsync(IPAddress ip);
    }
}
