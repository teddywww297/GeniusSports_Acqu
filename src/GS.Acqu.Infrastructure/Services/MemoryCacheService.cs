using GS.Acqu.Domain.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace GS.Acqu.Infrastructure.Services;

/// <summary>
/// 記憶體快取服務實作
/// </summary>
public class MemoryCacheService : ICacheService
{
    private readonly IMemoryCache _cache;

    public MemoryCacheService(IMemoryCache cache)
    {
        _cache = cache;
    }

    public Task<T?> GetAsync<T>(string key) where T : class
    {
        _cache.TryGetValue(key, out T? value);
        return Task.FromResult(value);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null) where T : class
    {
        var options = new MemoryCacheEntryOptions();
        
        if (expiry.HasValue)
        {
            options.AbsoluteExpirationRelativeToNow = expiry.Value;
        }

        _cache.Set(key, value, options);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key)
    {
        _cache.Remove(key);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string key)
    {
        return Task.FromResult(_cache.TryGetValue(key, out _));
    }
}

