using GS.Acqu.Domain.Entities;

namespace GS.Acqu.Domain.Interfaces;

/// <summary>
/// 盤口快取服務介面
/// </summary>
public interface IMarketCacheService
{
    /// <summary>
    /// 嘗試取得快取的盤口
    /// </summary>
    bool TryGet(int acquUniqId, out MarketDetailCache? market);

    /// <summary>
    /// 新增盤口到快取
    /// </summary>
    bool TryAdd(int acquUniqId, MarketDetailCache market);

    /// <summary>
    /// 更新快取中的盤口
    /// </summary>
    bool TryUpdate(int acquUniqId, MarketDetailCache newMarket, MarketDetailCache oldMarket);

    /// <summary>
    /// 直接設定快取
    /// </summary>
    void Set(int acquUniqId, MarketDetailCache market);

    /// <summary>
    /// 移除快取
    /// </summary>
    bool TryRemove(int acquUniqId);

    /// <summary>
    /// 快取數量
    /// </summary>
    int Count { get; }

    /// <summary>
    /// 清除所有快取
    /// </summary>
    void Clear();
}

