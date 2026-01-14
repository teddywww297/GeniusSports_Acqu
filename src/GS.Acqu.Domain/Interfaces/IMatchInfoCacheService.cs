using GS.Acqu.Domain.Entities;

namespace GS.Acqu.Domain.Interfaces;

/// <summary>
/// 賽事資訊快取服務介面
/// 用於快取從 GetMatches API 取得的完整賽事資訊（聯賽、球隊名稱等）
/// 以供 WatchMatches 串流更新時補齊缺失的資料
/// </summary>
public interface IMatchInfoCacheService
{
    /// <summary>
    /// 新增或更新賽事快取
    /// </summary>
    /// <param name="sourceMatchId">來源賽事 ID</param>
    /// <param name="matchInfo">完整賽事資訊</param>
    void AddOrUpdate(string sourceMatchId, MatchInfo matchInfo);

    /// <summary>
    /// 嘗試從快取取得賽事資訊
    /// </summary>
    /// <param name="sourceMatchId">來源賽事 ID</param>
    /// <param name="matchInfo">快取的賽事資訊</param>
    /// <returns>是否存在快取</returns>
    bool TryGet(string sourceMatchId, out MatchInfo? matchInfo);

    /// <summary>
    /// 從快取取得賽事資訊
    /// </summary>
    /// <param name="sourceMatchId">來源賽事 ID</param>
    /// <returns>快取的賽事資訊，不存在則回傳 null</returns>
    MatchInfo? GetMatchInfo(string sourceMatchId);

    /// <summary>
    /// 批次載入賽事資訊到快取
    /// </summary>
    /// <param name="matches">賽事資訊列表</param>
    /// <returns>載入的數量</returns>
    int LoadFromMatches(IEnumerable<MatchInfo> matches);

    /// <summary>
    /// 從快取補齊串流賽事的缺失資料（聯賽、球隊名稱）
    /// </summary>
    /// <param name="feedMatchInfo">串流取得的賽事資訊（可能缺少名稱）</param>
    /// <returns>補齊後的賽事資訊</returns>
    MatchInfo EnrichFromCache(MatchInfo feedMatchInfo);

    /// <summary>
    /// 清除過期的快取（根據賽事狀態或時間）
    /// </summary>
    /// <param name="olderThan">清除多久以前的快取</param>
    /// <returns>清除的數量</returns>
    int CleanupExpired(TimeSpan olderThan);

    /// <summary>
    /// 取得快取統計
    /// </summary>
    MatchInfoCacheStatistics GetStatistics();
}

/// <summary>
/// 快取統計資訊
/// </summary>
public class MatchInfoCacheStatistics
{
    /// <summary>
    /// 總快取數量
    /// </summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// 各球種的快取數量
    /// </summary>
    public Dictionary<string, int> CountBySport { get; init; } = new();

    /// <summary>
    /// 快取命中次數
    /// </summary>
    public long HitCount { get; init; }

    /// <summary>
    /// 快取未命中次數
    /// </summary>
    public long MissCount { get; init; }

    /// <summary>
    /// 命中率
    /// </summary>
    public double HitRate => HitCount + MissCount > 0 
        ? (double)HitCount / (HitCount + MissCount) 
        : 0;

    /// <summary>
    /// 最後更新時間
    /// </summary>
    public DateTime? LastUpdated { get; init; }
}
