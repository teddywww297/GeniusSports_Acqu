using GS.Acqu.Domain.Entities;
using GS.Acqu.Domain.Interfaces;
using GS.Acqu.Worker.Channels;
using GS.Acqu.Worker.Clients;
using GS.Acqu.Worker.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GS.Acqu.Worker.Workers;

/// <summary>
/// Genius Sports 資料訂閱背景服務
/// 負責訂閱賽事與盤口的即時變更，並寫入 Channel
/// 
/// 架構: gRPC Stream → Channel → BatchProcessor → Handler
/// </summary>
public class GeniusSportsSubscriber : BackgroundService
{
    private readonly IGeniusSportsClient _client;
    private readonly IMatchesApiClient _matchesApiClient;
    private readonly IDataChannel<MatchInfo> _matchChannel;
    private readonly IDataChannel<MarketInfo> _marketChannel;
    private readonly IMatchInfoCacheService _matchInfoCache;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly GeniusSportsOptions _options;
    private readonly ILogger<GeniusSportsSubscriber> _logger;
    
    /// <summary>
    /// 追蹤已送去對點的賽事 (避免重複對點)
    /// </summary>
    private readonly HashSet<string> _matchesSentForMatching = new();
    private readonly object _matchingLock = new();

    public GeniusSportsSubscriber(
        IGeniusSportsClient client,
        IMatchesApiClient matchesApiClient,
        IDataChannel<MatchInfo> matchChannel,
        IDataChannel<MarketInfo> marketChannel,
        IMatchInfoCacheService matchInfoCache,
        IServiceScopeFactory scopeFactory,
        IOptions<GeniusSportsOptions> options,
        ILogger<GeniusSportsSubscriber> logger)
    {
        _client = client;
        _matchesApiClient = matchesApiClient;
        _matchChannel = matchChannel;
        _marketChannel = marketChannel;
        _matchInfoCache = matchInfoCache;
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Genius Sports 訂閱服務啟動中...");

        // 等待其他服務啟動完成
        await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);

        // 先建立賽事快取，確保串流資料有名稱可用
        await InitializeMatchCacheAsync(stoppingToken);

        // 初始載入早盤資料（GetPreMatchMarkets 一次性拉取）
        await InitializePreMatchMarketsAsync(stoppingToken);

        // 啟動時補建一次缺失盤口
        await RepairMissingMarketsAsync(stoppingToken);

        // 啟動定時補建任務 (立即開始計時)
        _ = RunPeriodicRepairAsync(stoppingToken);

        // 為每個球種啟動訂閱
        var tasks = _options.SportIds.SelectMany(sportId => new[]
        {
            SubscribeMatchesAsync(sportId, stoppingToken),
            SubscribePreMatchMarketsAsync(sportId, stoppingToken),
            SubscribeInPlayMarketsAsync(sportId, stoppingToken)
        }).ToList();

        _logger.LogInformation(
            "已啟動 {Count} 個訂閱任務, 球種: [{SportIds}]",
            tasks.Count,
            string.Join(", ", _options.SportIds));

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Genius Sports 訂閱服務已停止");
        }
    }

    /// <summary>
    /// 初始化賽事快取 - 從 REST API 取得完整賽事資訊（聯賽、球隊名稱）
    /// 並送到 MatchChannel 進行對點
    /// </summary>
    private async Task InitializeMatchCacheAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("開始初始化賽事快取 (使用 REST API)...");

        var startDate = DateTime.Today.AddDays(-1);  // 過去一天
        var endDate = DateTime.Today.AddDays(7);     // 未來七天
        var totalLoaded = 0;
        var totalMatched = 0;

        foreach (var sportId in _options.SportIds)
        {
            try
            {
                var matches = await _matchesApiClient.GetMatchesAsync(sportId, startDate, endDate, stoppingToken);
                var matchList = matches.ToList();
                
                // 載入到快取
                var loaded = _matchInfoCache.LoadFromMatches(matchList);
                totalLoaded += loaded;

                // 送到 MatchChannel 進行對點
                var matchedCount = 0;
                foreach (var match in matchList)
                {
                    if (_matchChannel.TryWrite(match))
                    {
                        matchedCount++;
                        
                        // 記錄已送去對點，避免後續重複
                        lock (_matchingLock)
                        {
                            _matchesSentForMatching.Add(match.SourceMatchId);
                        }
                    }
                }
                totalMatched += matchedCount;

                _logger.LogInformation(
                    "SportId={SportId} 已載入 {Loaded} 筆賽事到快取, 送出 {Matched} 筆進行對點",
                    sportId,
                    loaded,
                    matchedCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "載入 SportId={SportId} 賽事快取失敗", sportId);
            }
        }

        var stats = _matchInfoCache.GetStatistics();
        _logger.LogInformation(
            "賽事快取初始化完成: 總共 {TotalCount} 筆, 對點 {TotalMatched} 筆, 各球種: {CountBySport}",
            stats.TotalCount,
            totalMatched,
            string.Join(", ", stats.CountBySport.Select(kvp => $"{kvp.Key}={kvp.Value}")));
        
        // 等待對點處理完成
        _logger.LogInformation("等待對點處理完成...");
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
    }

    /// <summary>
    /// 初始化早盤盤口 - 從 GetPreMatchMarkets 取得所有現有早盤資料
    /// </summary>
    /// <remarks>
    /// 流程：
    /// 1. 取得早盤盤口資料（包含 MatchId）
    /// 2. 對每個 MatchId，從快取取得完整的賽事資訊 (MatchInfo)
    /// 3. 如果快取沒有，嘗試從 GetMatches API 補取
    /// 4. 先寫入 MatchChannel 進行對點（建立/更新 ProviderMatchId）
    /// 5. 等待對點完成後，再寫入 MarketChannel 處理盤口賠率
    /// </remarks>
    private async Task InitializePreMatchMarketsAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("開始初始載入早盤資料 (使用 GetPreMatchMarkets)...");

        var startDate = DateTime.Today.ToString("yyyy-MM-dd");
        var endDate = DateTime.Today.AddDays(7).ToString("yyyy-MM-dd");
        var totalMatches = 0;
        var totalMarkets = 0;

        foreach (var sportId in _options.SportIds)
        {
            try
            {
                var marketsData = await _client.GetPreMatchMarketsAsync(
                    sportId, startDate, endDate, stoppingToken);

                var marketsList = marketsData.ToList();
                var processedMatchIds = new HashSet<int>();
                var missingMatchIds = new List<int>();

                // 第一步：對每個 MatchId 進行賽事對點
                foreach (var (matchId, _) in marketsList)
                {
                    if (processedMatchIds.Contains(matchId))
                        continue;

                    processedMatchIds.Add(matchId);

                    // 從快取取得完整的賽事資訊
                    var matchInfo = _matchInfoCache.GetMatchInfo(matchId.ToString());
                    if (matchInfo != null)
                    {
                        // 寫入 MatchChannel 進行對點
                        if (_matchChannel.TryWrite(matchInfo))
                        {
                            totalMatches++;
                        }
                    }
                    else
                    {
                        missingMatchIds.Add(matchId);
                        _logger.LogDebug(
                            "早盤賽事快取未找到: MatchId={MatchId}",
                            matchId);
                    }
                }

                // 補取快取中缺失的賽事資訊
                if (missingMatchIds.Count > 0)
                {
                    _logger.LogInformation(
                        "SportId={SportId} 有 {Count} 筆早盤賽事需補取資訊: [{MatchIds}]",
                        sportId, missingMatchIds.Count, 
                        string.Join(", ", missingMatchIds.Take(10)));

                    // 重新從 GetMatches 取得更完整的賽事資料
                    try
                    {
                        var allMatches = await _matchesApiClient.GetMatchesAsync(
                            sportId, 
                            DateTime.Today, 
                            DateTime.Today.AddDays(14),  // 擴大範圍
                            stoppingToken);

                        var matchDict = allMatches.ToDictionary(m => m.SourceMatchId, m => m);

                        foreach (var matchId in missingMatchIds)
                        {
                            var matchIdStr = matchId.ToString();
                            if (matchDict.TryGetValue(matchIdStr, out var matchInfo))
                            {
                                // 加入快取
                                _matchInfoCache.AddOrUpdate(matchIdStr, matchInfo);
                                
                                // 寫入 MatchChannel 進行對點
                                if (_matchChannel.TryWrite(matchInfo))
                                {
                                    totalMatches++;
                                    _logger.LogDebug("補取賽事成功: MatchId={MatchId}", matchId);
                                }
                            }
                            else
                            {
                                _logger.LogWarning(
                                    "補取賽事失敗，API 未回傳: MatchId={MatchId}",
                                    matchId);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "補取缺失賽事資訊失敗: SportId={SportId}", sportId);
                    }
                }

                _logger.LogInformation(
                    "SportId={SportId} 已送出 {Count} 筆賽事進行對點",
                    sportId, totalMatches);

                // 等待對點處理完成（給 MatchInfoHandler 一些時間）
                await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);

                // 第二步：處理盤口資料
                var marketCount = 0;
                foreach (var (matchId, markets) in marketsList)
                {
                    foreach (var market in markets)
                    {
                        if (_marketChannel.TryWrite(market))
                        {
                            marketCount++;
                        }
                    }
                }

                totalMarkets += marketCount;
                _logger.LogInformation(
                    "SportId={SportId} 已載入 {Count} 筆早盤盤口",
                    sportId, marketCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "載入 SportId={SportId} 早盤資料失敗", sportId);
            }
        }

        _logger.LogInformation(
            "早盤初始載入完成: 賽事 {MatchCount} 筆, 盤口 {MarketCount} 筆",
            totalMatches, totalMarkets);
    }

    /// <summary>
    /// 訂閱賽事變更 → 寫入 MatchChannel
    /// </summary>
    private async Task SubscribeMatchesAsync(int sportId, CancellationToken stoppingToken)
    {
        _logger.LogInformation("開始訂閱賽事變更: SportId={SportId}", sportId);

        try
        {
            await foreach (var matchInfo in _client.WatchMatchesAsync(sportId, stoppingToken))
            {
                // 直接寫入 Channel，由 MatchBatchProcessor 處理
                if (!_matchChannel.TryWrite(matchInfo))
                {
                    _logger.LogWarning(
                        "MatchChannel 寫入失敗: MatchId={MatchId}",
                        matchInfo.SourceMatchId);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 正常取消
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "賽事訂閱異常終止: SportId={SportId}", sportId);
        }
    }

    /// <summary>
    /// 訂閱早盤盤口變更 → 寫入 MarketChannel
    /// </summary>
    private async Task SubscribePreMatchMarketsAsync(int sportId, CancellationToken stoppingToken)
    {
        _logger.LogInformation("開始訂閱早盤盤口變更: SportId={SportId}", sportId);

        try
        {
            await foreach (var (matchId, markets) in _client.WatchPreMatchMarketsAsync(sportId, stoppingToken))
            {
                var matchIdStr = matchId.ToString();
                
                // 檢查是否需要先對點該賽事
                bool needsMatching;
                lock (_matchingLock)
                {
                    needsMatching = !_matchesSentForMatching.Contains(matchIdStr);
                    if (needsMatching)
                    {
                        _matchesSentForMatching.Add(matchIdStr);
                    }
                }

                if (needsMatching)
                {
                    // 從快取取得賽事資訊並送去對點
                    var matchInfo = _matchInfoCache.GetMatchInfo(matchIdStr);
                    if (matchInfo != null)
                    {
                        if (_matchChannel.TryWrite(matchInfo))
                        {
                            _logger.LogDebug("早盤盤口觸發賽事對點: MatchId={MatchId}", matchId);
                        }
                        
                        // 等待對點完成後再寫入盤口
                        await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "早盤盤口對應賽事不在快取中: MatchId={MatchId}, SportId={SportId}",
                            matchId, sportId);
                    }
                }

                // 將每個 Market 寫入 Channel
                foreach (var market in markets)
                {
                    if (!_marketChannel.TryWrite(market))
                    {
                        _logger.LogWarning(
                            "MarketChannel 寫入失敗 (早盤): MatchId={MatchId}",
                            matchId);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 正常取消
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "早盤盤口訂閱異常終止: SportId={SportId}", sportId);
        }
    }

    /// <summary>
    /// 訂閱走地盤口變更 → 寫入 MarketChannel
    /// </summary>
    private async Task SubscribeInPlayMarketsAsync(int sportId, CancellationToken stoppingToken)
    {
        _logger.LogInformation("開始訂閱走地盤口變更: SportId={SportId}", sportId);

        try
        {
            await foreach (var (matchId, markets) in _client.WatchInPlayMarketsAsync(sportId, stoppingToken))
            {
                // 將每個 Market 寫入 Channel
                foreach (var market in markets)
                {
                    if (!_marketChannel.TryWrite(market))
                    {
                        _logger.LogWarning(
                            "MarketChannel 寫入失敗 (走地): MatchId={MatchId}",
                            matchId);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 正常取消
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "走地盤口訂閱異常終止: SportId={SportId}", sportId);
        }
    }

    #region 補建缺失盤口

    /// <summary>
    /// 定時執行補建缺失盤口 (首次啟動後立即開始計時)
    /// </summary>
    private async Task RunPeriodicRepairAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(_options.RepairIntervalMinutes);
        
        _logger.LogInformation(
            "定時補建任務已啟動，間隔 {Interval} 分鐘",
            _options.RepairIntervalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, stoppingToken);
                await RepairMissingMarketsAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "定時補建任務執行失敗");
            }
        }
    }

    /// <summary>
    /// 補建缺失盤口 - 查詢有 ProviderMatchId 但沒有盤口的賽事，重新拉取盤口
    /// </summary>
    private async Task RepairMissingMarketsAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("開始檢查缺失盤口賽事...");

        try
        {
            // 使用 scope 取得 scoped 服務
            using var scope = _scopeFactory.CreateScope();
            var matchRepo = scope.ServiceProvider.GetRequiredService<IMatchRepository>();

            // 查詢缺失盤口的賽事
            var events = await matchRepo.GetEventsWithoutMarketsAsync(_options.RepairBatchSize);
            var eventList = events.ToList();

            if (eventList.Count == 0)
            {
                _logger.LogInformation("沒有缺失盤口的賽事");
                return;
            }

            // 計算剩餘數量提示
            var remainingHint = eventList.Count >= _options.RepairBatchSize
                ? "可能還有更多"
                : "0";

            _logger.LogInformation(
                "發現 {Count} 筆缺失盤口賽事，剩餘補建約 {Remaining} 筆",
                eventList.Count, remainingHint);

            // 依 CatID (SportId) 分組
            var bySport = eventList.GroupBy(e => e.CatId);
            var totalMarkets = 0;
            var repairedEvents = 0;

            foreach (var group in bySport)
            {
                var sportId = group.Key;
                var providerMatchIds = group.Select(e => e.ProviderMatchId).ToHashSet();

                try
                {
                    // 取得該球種的早盤資料
                    var startDate = DateTime.Today.ToString("yyyy-MM-dd");
                    var endDate = DateTime.Today.AddDays(7).ToString("yyyy-MM-dd");

                    var marketsData = await _client.GetPreMatchMarketsAsync(
                        sportId, startDate, endDate, stoppingToken);

                    // 篩選目標賽事的盤口
                    foreach (var (matchId, markets) in marketsData)
                    {
                        var matchIdStr = matchId.ToString();
                        if (!providerMatchIds.Contains(matchIdStr))
                            continue;

                        var marketCount = 0;
                        foreach (var market in markets)
                        {
                            if (_marketChannel.TryWrite(market))
                            {
                                marketCount++;
                            }
                        }

                        if (marketCount > 0)
                        {
                            repairedEvents++;
                            totalMarkets += marketCount;
                            _logger.LogDebug(
                                "補建賽事盤口: ProviderMatchId={MatchId}, 盤口數={Count}",
                                matchIdStr, marketCount);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "補建 SportId={SportId} 盤口失敗", sportId);
                }
            }

            _logger.LogInformation(
                "補建完成: 賽事 {EventCount} 筆, 盤口 {MarketCount} 筆",
                repairedEvents, totalMarkets);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "補建缺失盤口失敗");
        }
    }

    #endregion
}
