using System.Net;
using System.Threading.Tasks;

namespace SillyGeo.Infrastructure.Services
{
    public interface IIPGeoLocationService
    {
        Task<IPRangeInfo> LocateAsync(IPAddress ip);
    }
}
