using System.Threading.Tasks;

namespace SillyGeo.Infrastructure.Services
{
    public interface IGeoNamesService : IGeoLocationService
    {
        Task<Area> GetAreaAsync(int id);

        Task<Country> GetCountryAsync(string code);

        Task<string> GetPopulatedPlaceAddressAsync(int id, string cultureName);
    }
}
