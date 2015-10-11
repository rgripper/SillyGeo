using System;
using System.Threading.Tasks;

namespace SillyGeo
{
    public interface IStoreService : IDisposable
    {
        Task RemoveAsync();
        Task CreateIfNotExistsAsync();
    }
}