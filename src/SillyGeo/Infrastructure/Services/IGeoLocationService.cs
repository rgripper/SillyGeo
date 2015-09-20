using System.Threading.Tasks;

namespace SillyGeo.Infrastructure.Services
{
    public interface IGeoLocationService
    {
        Task<PopulatedPlace> GetNearestPopulatedPlaceAsync(Coordinates coordinates);
    }
}
