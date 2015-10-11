using System;
using System.Threading.Tasks;

namespace SillyGeo.Infrastructure.Services
{
    public interface IGeoLocationService : IDisposable
    {
        Task<PopulatedPlace> GetNearestPopulatedPlaceAsync(Coordinates coordinates);
    }
}
