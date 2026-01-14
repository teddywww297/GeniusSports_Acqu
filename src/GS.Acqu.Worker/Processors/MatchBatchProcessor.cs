using GS.Acqu.Application.Interfaces;
using GS.Acqu.Domain.Entities;
using GS.Acqu.Worker.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GS.Acqu.Worker.Processors;

/// <summary>
/// 賽事批次處理器
/// 從 Channel 讀取賽事資料，逐筆處理 (賽事需要完整對點流程)
/// </summary>
public class MatchBatchProcessor : BackgroundService
{
    private readonly IDataChannel<MatchInfo> _channel;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MatchBatchProcessor> _logger;

    // 設定
    private const int BatchTimeoutMs = 100;      // 賽事更新頻率較低，可等久一點
    private const int StatsIntervalSeconds = 60;

    private DateTime _lastStatsTime = DateTime.UtcNow;
    private long _totalProcessed;

    public MatchBatchProcessor(
        IDataChannel<MatchInfo> channel,
        IServiceProvider serviceProvider,
        ILogger<MatchBatchProcessor> logger)
    {
        _channel = channel;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MatchBatchProcessor 啟動");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                timeoutCts.CancelAfter(BatchTimeoutMs);

                try
                {
                    if (await _channel.WaitToReadAsync(timeoutCts.Token))
                    {
                        // 賽事逐筆處理 (需要完整對點流程)
                        while (_channel.TryRead(out var matchInfo))
                        {
                            if (matchInfo != null)
                            {
                                await ProcessMatchAsync(matchInfo, stoppingToken);
                            }
                        }
                    }
                }
                catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
                {
                    // 超時，繼續迴圈
                }

                LogStatsIfNeeded();
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("MatchBatchProcessor 正在停止...");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MatchBatchProcessor 發生錯誤");
            throw;
        }

        // 處理剩餘資料
        await DrainRemainingAsync(stoppingToken);

        _logger.LogInformation(
            "MatchBatchProcessor 已停止: TotalProcessed={TotalProcessed}",
            _totalProcessed);
    }

    /// <summary>
    /// 處理單筆賽事
    /// </summary>
    private async Task ProcessMatchAsync(MatchInfo matchInfo, CancellationToken cancellationToken)
    {
        try
        {
            // 賽事需要 Scoped 服務 (Repository)
            using var scope = _serviceProvider.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<IMatchInfoHandler>();

            var result = await handler.HandleAsync(matchInfo, cancellationToken);

            Interlocked.Increment(ref _totalProcessed);

            _logger.LogDebug(
                "賽事處理完成: MatchId={MatchId}, Status={Status}, Result={Result}",
                matchInfo.SourceMatchId, matchInfo.Status, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "賽事處理失敗: MatchId={MatchId}",
                matchInfo.SourceMatchId);
        }
    }

    /// <summary>
    /// 停止時處理剩餘資料
    /// </summary>
    private async Task DrainRemainingAsync(CancellationToken cancellationToken)
    {
        var count = 0;
        while (_channel.TryRead(out var matchInfo))
        {
            if (matchInfo != null)
            {
                await ProcessMatchAsync(matchInfo, cancellationToken);
                count++;
            }
        }

        if (count > 0)
        {
            _logger.LogInformation("已處理剩餘 {Count} 筆賽事資料", count);
        }
    }

    /// <summary>
    /// 定期輸出統計資訊
    /// </summary>
    private void LogStatsIfNeeded()
    {
        var now = DateTime.UtcNow;
        if ((now - _lastStatsTime).TotalSeconds < StatsIntervalSeconds)
        {
            return;
        }

        _lastStatsTime = now;
        var stats = _channel.GetStats();

        _logger.LogInformation(
            "MatchBatchProcessor 統計: " +
            "TotalProcessed={TotalProcessed}, " +
            "ChannelDepth={Depth}, Written={Written}, Read={Read}, Dropped={Dropped}",
            _totalProcessed,
            stats.CurrentDepth, stats.TotalWritten, stats.TotalRead, stats.TotalDropped);
    }
}

