using GS.Acqu.Application.Interfaces;
using GS.Acqu.Domain.Entities;
using GS.Acqu.Domain.Enums;
using GS.Acqu.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace GS.Acqu.Application.UseCases;

/// <summary>
/// 盤口賠率處理器實作 (高效能版本)
/// 整合：快取比對 → 變更檢測 → 批次隊列
/// </summary>
public class MarketOddsHandler : IMarketOddsHandler
{
    private readonly IMatchRepository _matchRepo;
    private readonly IMarketRepository _marketRepo;
    private readonly IMarketCacheService _cache;
    private readonly IMarketQueueService _queue;
    private readonly ILogger<MarketOddsHandler> _logger;

    // 更新頻率控制 (秒)
    private const int LiveUpdateInterval = 4;      // 走地盤口
    private const int NormalUpdateInterval = 10;   // 一般盤口
    private const int SpecialUpdateInterval = 100; // 特殊玩法

    public MessageType MessageType => MessageType.MarketOdds;

    public MarketOddsHandler(
        IMatchRepository matchRepo,
        IMarketRepository marketRepo,
        IMarketCacheService cache,
        IMarketQueueService queue,
        ILogger<MarketOddsHandler> logger)
    {
        _matchRepo = matchRepo;
        _marketRepo = marketRepo;
        _cache = cache;
        _queue = queue;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ProcessResult> HandleAsync(
        IEnumerable<MarketInfo> markets,
        CancellationToken cancellationToken = default)
    {
        var marketList = markets.ToList();

        if (marketList.Count == 0)
        {
            return ProcessResult.Skipped;
        }

        // 按賽事分組處理（批次可能包含多場賽事的盤口）
        var marketsByMatch = marketList.GroupBy(m => m.SourceMatchId);
        var successCount = 0;
        var failedCount = 0;
        var notFoundCount = 0;

        foreach (var matchGroup in marketsByMatch)
        {
            var sourceMatchId = matchGroup.Key;
            var matchMarkets = matchGroup.ToList();

            try
            {
                // 1. 查詢對應的系統賽事編號
                var eventId = await _matchRepo.GetEventIdAsync(sourceMatchId);

                if (eventId == null)
                {
                    _logger.LogWarning(
                        "盤口對應賽事不存在: SourceMatchId={SourceMatchId}, 盤口數={MarketCount}",
                        sourceMatchId, matchMarkets.Count);
                    notFoundCount += matchMarkets.Count;
                    continue;
                }

                // 2. 逐筆處理該賽事的盤口
                foreach (var market in matchMarkets)
                {
                    await ProcessSingleMarketAsync(eventId.Value, market, cancellationToken);
                }

                successCount += matchMarkets.Count;
                _logger.LogDebug(
                    "處理盤口完成: EvtID={EventId}, SourceMatchId={SourceMatchId}, 數量={Count}",
                    eventId.Value, sourceMatchId, matchMarkets.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "處理盤口賠率失敗: SourceMatchId={SourceMatchId}", sourceMatchId);
                failedCount += matchMarkets.Count;
            }
        }

        // 回傳整體結果
        if (successCount > 0 && notFoundCount == 0 && failedCount == 0)
            return ProcessResult.Success;
        if (notFoundCount > 0 && successCount == 0)
            return ProcessResult.MatchNotFound;
        if (failedCount > 0 && successCount == 0)
            return ProcessResult.DatabaseError;
        
        // 部分成功
        _logger.LogInformation(
            "批次處理結果: 成功={Success}, 賽事不存在={NotFound}, 失敗={Failed}",
            successCount, notFoundCount, failedCount);
        return ProcessResult.Success;
    }

    /// <summary>
    /// 處理單筆盤口
    /// </summary>
    private async Task ProcessSingleMarketAsync(int eventId, MarketInfo market, CancellationToken cancellationToken)
    {
        // 1. 計算唯一識別碼
        var acquUniqId = GenerateAcquUniqId(eventId, market);

        // 2. 建立新資料物件
        var newData = ConvertToDetailCache(acquUniqId, eventId, market);

        // 3. 查詢快取
        _cache.TryGet(acquUniqId, out var oldData);

        // 4. 決定處理方式
        if (oldData == null)
        {
            // ===== 新盤口：同步 INSERT =====
            await HandleNewMarketAsync(newData);
        }
        else if (IsDataExpired(oldData, newData))
        {
            // ===== 過期資料：跳過 =====
            _logger.LogDebug("過期資料跳過: AcquUniqID={AcquUniqId}, Old={OldTime} > New={NewTime}",
                acquUniqId, oldData.SourceTime, newData.SourceTime);
        }
        else if (HasDataChanged(oldData, newData))
        {
            // ===== 有變更：放入隊列 =====
            HandleChangedMarket(oldData, newData);
        }
        else if (NeedPeriodicUpdate(oldData, market))
        {
            // ===== 無變更但需定期更新 =====
            HandlePeriodicUpdate(oldData, newData);
        }
    }

    /// <summary>
    /// 處理新盤口 (先 INSERT 到 DB，再加入快取)
    /// </summary>
    private async Task HandleNewMarketAsync(MarketDetailCache newData)
    {
        // 先嘗試加入快取，避免重複處理
        if (!_cache.TryAdd(newData.AcquUniqId, newData))
        {
            // 另一個執行緒已新增，跳過
            _logger.LogDebug("盤口已存在，跳過新增: AcquUniqID={AcquUniqId}", newData.AcquUniqId);
            return;
        }

        // INSERT 到資料庫
        newData.Change = 1;
        var success = await _marketRepo.InsertMarketDetailAsync(newData);
        
        if (success)
        {
            _logger.LogDebug("新增盤口: AcquUniqID={AcquUniqId}, EvtID={EvtId}",
                newData.AcquUniqId, newData.EvtId);
        }
        else
        {
            // INSERT 失敗，從快取移除
            _cache.TryRemove(newData.AcquUniqId);
            _logger.LogWarning("新增盤口失敗: AcquUniqID={AcquUniqId}", newData.AcquUniqId);
        }
    }

    /// <summary>
    /// 處理有變更的盤口
    /// </summary>
    private void HandleChangedMarket(MarketDetailCache oldData, MarketDetailCache newData)
    {
        newData.Change = 1; // 標記有變更
        
        // Bug 2 修復: 只有 TryUpdate 成功才入隊，避免過期資料覆蓋
        if (_cache.TryUpdate(newData.AcquUniqId, newData, oldData))
        {
            _queue.EnqueueUpdate(newData);
            _logger.LogDebug("更新盤口: AcquUniqID={AcquUniqId}, Change=1", newData.AcquUniqId);
        }
        else
        {
            // 快取已被其他執行緒更新，跳過本次更新
            _logger.LogDebug("快取已變更，跳過更新: AcquUniqID={AcquUniqId}", newData.AcquUniqId);
        }
    }

    /// <summary>
    /// 處理定期更新 (無變更但需更新時間戳)
    /// </summary>
    private void HandlePeriodicUpdate(MarketDetailCache oldData, MarketDetailCache newData)
    {
        newData.Change = 0; // 標記無變更
        
        // Bug 2 修復: 只有 TryUpdate 成功才入隊
        if (_cache.TryUpdate(newData.AcquUniqId, newData, oldData))
        {
            _queue.EnqueueUpdate(newData);
        }
        // 定期更新失敗不需要日誌，因為已有其他更新
    }

    #region 輔助方法

    /// <summary>
    /// 產生唯一識別碼 (BKDR Hash)
    /// </summary>
    private static int GenerateAcquUniqId(int eventId, MarketInfo market)
    {
        // 組合 Key: {EvtID}_{MarketType}_{HalfType}_{Line}
        var key = $"{eventId}_{(int)market.MarketType}_{(int)market.HalfType}_{market.Line ?? 0}";
        return Math.Abs(BKDRHash(key));
    }

    /// <summary>
    /// BKDR Hash 演算法
    /// </summary>
    private static int BKDRHash(string str)
    {
        const int seed = 131;
        int hash = 0;
        foreach (char c in str)
        {
            hash = (hash * seed) + c;
        }
        return hash;
    }

    /// <summary>
    /// 轉換為快取實體
    /// </summary>
    private static MarketDetailCache ConvertToDetailCache(int acquUniqId, int eventId, MarketInfo market)
    {
        // Bug 3 修復: 正確設定讓分盤的 HomeHdp、AwayHdp 和 HdpPos
        var (homeHdp, awayHdp, hdpPos) = GetHandicapValues(market);

        return new MarketDetailCache
        {
            AcquUniqId = acquUniqId,
            EvtId = eventId,
            HalfType = (byte)market.HalfType,
            WagerTypeId = (short)market.MarketType,
            Status = market.IsSuspended ? (short)-1 : (short)1,
            SourceTime = market.SourceUpdateTime,
            LastUpdate = DateTime.Now,

            // 讓分盤欄位 (已正確設定)
            HomeHdp = homeHdp,
            AwayHdp = awayHdp,
            HdpPos = hdpPos,
            HomeHdpOdds = market.MarketType == MarketType.Handicap ? market.Odds1 : 0,
            AwayHdpOdds = market.MarketType == MarketType.Handicap ? market.Odds2 : 0,

            // 大小盤欄位
            OULine = market.MarketType == MarketType.OverUnder ? market.Line?.ToString() : null,
            OverOdds = market.MarketType == MarketType.OverUnder ? market.Odds1 : 0,
            UnderOdds = market.MarketType == MarketType.OverUnder ? market.Odds2 : 0,

            // 獨贏盤欄位
            HomeOdds = market.MarketType == MarketType.MoneyLine ? market.Odds1 : 0,
            DrawOdds = market.MarketType == MarketType.MoneyLine ? market.Odds3 ?? 0 : 0,
            AwayOdds = market.MarketType == MarketType.MoneyLine ? market.Odds2 : 0,
        };
    }

    /// <summary>
    /// 取得讓分盤的 HomeHdp、AwayHdp 和 HdpPos
    /// </summary>
    /// <remarks>
    /// 讓分邏輯：
    /// - Line > 0 (正值)：主讓客 → HdpPos=1, HomeHdp=Line, AwayHdp="-"
    /// - Line < 0 (負值)：客讓主 → HdpPos=2, HomeHdp="-", AwayHdp=|Line|
    /// - Line = 0 (平手)：無讓分 → HdpPos=0, HomeHdp="0", AwayHdp="0"
    /// </remarks>
    private static (string? homeHdp, string? awayHdp, byte hdpPos) GetHandicapValues(MarketInfo market)
    {
        if (market.MarketType != MarketType.Handicap || market.Line == null)
        {
            return (null, null, 0);
        }

        var line = market.Line.Value;

        if (line > 0)
        {
            // 主讓客 (主隊讓分)
            return (line.ToString(), "-", 1);
        }
        else if (line < 0)
        {
            // 客讓主 (客隊讓分)
            return ("-", Math.Abs(line).ToString(), 2);
        }
        else
        {
            // 平手盤
            return ("0", "0", 0);
        }
    }

    /// <summary>
    /// 檢查資料是否過期 (來源時間較舊)
    /// </summary>
    private static bool IsDataExpired(MarketDetailCache oldData, MarketDetailCache newData)
    {
        return oldData.SourceTime > newData.SourceTime;
    }

    /// <summary>
    /// 檢查資料是否有變更
    /// </summary>
    private static bool HasDataChanged(MarketDetailCache oldData, MarketDetailCache newData)
    {
        // 讓分盤變更檢測
        if (newData.HomeHdpOdds > 0)
        {
            if (oldData.HomeHdp != newData.HomeHdp ||
                oldData.AwayHdp != newData.AwayHdp ||
                oldData.HdpPos != newData.HdpPos ||
                oldData.HomeHdpOdds != newData.HomeHdpOdds ||
                oldData.AwayHdpOdds != newData.AwayHdpOdds)
            {
                return true;
            }
        }

        // 大小盤變更檢測
        if (newData.OverOdds > 0)
        {
            if (oldData.OULine != newData.OULine ||
                oldData.OverOdds != newData.OverOdds ||
                oldData.UnderOdds != newData.UnderOdds)
            {
                return true;
            }
        }

        // 獨贏盤變更檢測
        if (newData.HomeOdds > 0)
        {
            if (oldData.HomeOdds != newData.HomeOdds ||
                oldData.AwayOdds != newData.AwayOdds ||
                oldData.DrawOdds != newData.DrawOdds)
            {
                return true;
            }
        }

        // 狀態變更檢測
        if (oldData.Status != newData.Status)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// 檢查是否需要定期更新 (即使資料無變更)
    /// </summary>
    private static bool NeedPeriodicUpdate(MarketDetailCache oldData, MarketInfo market)
    {
        var secondsSinceLastUpdate = (DateTime.Now - oldData.LastUpdate).TotalSeconds;

        // 走地盤口：4 秒更新一次
        // 這裡需要根據 GameType 判斷，暫時用 WagerTypeId 判斷
        // TODO: 需要傳入 GameType 資訊

        // 一般盤口：10 秒更新一次
        return secondsSinceLastUpdate >= NormalUpdateInterval;
    }

    #endregion
}
