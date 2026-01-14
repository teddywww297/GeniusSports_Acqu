using System.Collections.Concurrent;
using GS.Acqu.Domain.Entities;
using GS.Acqu.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace GS.Acqu.Infrastructure.Services;

/// <summary>
/// 盤口快取服務實作 (使用 ConcurrentDictionary)
/// </summary>
public class MarketCacheService : IMarketCacheService
{
    private readonly ConcurrentDictionary<int, MarketDetailCache> _cache = new();
    private readonly ILogger<MarketCacheService> _logger;

    public MarketCacheService(ILogger<MarketCacheService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public bool TryGet(int acquUniqId, out MarketDetailCache? market)
    {
        return _cache.TryGetValue(acquUniqId, out market);
    }

    /// <inheritdoc />
    public bool TryAdd(int acquUniqId, MarketDetailCache market)
    {
        return _cache.TryAdd(acquUniqId, market);
    }

    /// <inheritdoc />
    public bool TryUpdate(int acquUniqId, MarketDetailCache newMarket, MarketDetailCache oldMarket)
    {
        return _cache.TryUpdate(acquUniqId, newMarket, oldMarket);
    }

    /// <inheritdoc />
    public void Set(int acquUniqId, MarketDetailCache market)
    {
        _cache[acquUniqId] = market;
    }

    /// <inheritdoc />
    public bool TryRemove(int acquUniqId)
    {
        return _cache.TryRemove(acquUniqId, out _);
    }

    /// <inheritdoc />
    public int Count => _cache.Count;

    /// <inheritdoc />
    public void Clear()
    {
        _cache.Clear();
        _logger.LogInformation("盤口快取已清除");
    }
}

