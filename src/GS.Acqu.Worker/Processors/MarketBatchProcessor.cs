using GS.Acqu.Application.Interfaces;
using GS.Acqu.Domain.Entities;
using GS.Acqu.Worker.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GS.Acqu.Worker.Processors;

/// <summary>
/// 盤口批次處理器
/// 從 Channel 讀取資料，累積批次後交給 Handler 處理
/// </summary>
public class MarketBatchProcessor : BackgroundService
{
    private readonly IDataChannel<MarketInfo> _channel;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MarketBatchProcessor> _logger;

    // 批次設定
    private const int BatchSize = 100;           // 每批最多 100 筆
    private const int BatchTimeoutMs = 50;       // 最多等待 50ms
    private const int StatsIntervalSeconds = 60; // 每 60 秒輸出統計

    private DateTime _lastStatsTime = DateTime.UtcNow;
    private long _totalProcessed;
    private long _totalBatches;

    public MarketBatchProcessor(
        IDataChannel<MarketInfo> channel,
        IServiceProvider serviceProvider,
        ILogger<MarketBatchProcessor> logger)
    {
        _channel = channel;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "MarketBatchProcessor 啟動: BatchSize={BatchSize}, TimeoutMs={TimeoutMs}",
            BatchSize, BatchTimeoutMs);

        var batch = new List<MarketInfo>(BatchSize);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                batch.Clear();

                // 等待資料 (使用超時避免空轉)
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                timeoutCts.CancelAfter(BatchTimeoutMs);

                try
                {
                    // 等待至少有一筆資料
                    if (await _channel.WaitToReadAsync(timeoutCts.Token))
                    {
                        // 收集批次資料
                        CollectBatch(batch);
                    }
                }
                catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
                {
                    // 超時，檢查是否有累積的資料
                    CollectBatch(batch);
                }

                // 處理批次
                if (batch.Count > 0)
                {
                    await ProcessBatchAsync(batch, stoppingToken);
                }

                // 定期輸出統計
                LogStatsIfNeeded();
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("MarketBatchProcessor 正在停止...");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MarketBatchProcessor 發生錯誤");
            throw;
        }

        // 處理剩餘資料
        await DrainRemainingAsync(stoppingToken);

        _logger.LogInformation(
            "MarketBatchProcessor 已停止: TotalProcessed={TotalProcessed}, TotalBatches={TotalBatches}",
            _totalProcessed, _totalBatches);
    }

    /// <summary>
    /// 從 Channel 收集批次資料
    /// </summary>
    private void CollectBatch(List<MarketInfo> batch)
    {
        while (batch.Count < BatchSize && _channel.TryRead(out var item))
        {
            if (item != null)
            {
                batch.Add(item);
            }
        }
    }

    /// <summary>
    /// 處理批次資料
    /// </summary>
    private async Task ProcessBatchAsync(List<MarketInfo> batch, CancellationToken cancellationToken)
    {
        try
        {
            // 使用 Scope 取得 Handler (因為 Handler 是 Scoped)
            using var scope = _serviceProvider.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<IMarketOddsHandler>();

            var result = await handler.HandleAsync(batch, cancellationToken);

            Interlocked.Add(ref _totalProcessed, batch.Count);
            Interlocked.Increment(ref _totalBatches);

            _logger.LogDebug(
                "批次處理完成: Count={Count}, Result={Result}",
                batch.Count, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批次處理失敗: Count={Count}", batch.Count);
        }
    }

    /// <summary>
    /// 停止時處理剩餘資料
    /// </summary>
    private async Task DrainRemainingAsync(CancellationToken cancellationToken)
    {
        var remaining = new List<MarketInfo>();

        while (_channel.TryRead(out var item))
        {
            if (item != null)
            {
                remaining.Add(item);
            }

            // 每批處理
            if (remaining.Count >= BatchSize)
            {
                await ProcessBatchAsync(remaining, cancellationToken);
                remaining.Clear();
            }
        }

        // 處理最後一批
        if (remaining.Count > 0)
        {
            await ProcessBatchAsync(remaining, cancellationToken);
        }

        _logger.LogInformation("已處理剩餘 {Count} 筆資料", remaining.Count);
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
            "MarketBatchProcessor 統計: " +
            "TotalProcessed={TotalProcessed}, TotalBatches={TotalBatches}, " +
            "ChannelDepth={Depth}, Written={Written}, Read={Read}, Dropped={Dropped}",
            _totalProcessed, _totalBatches,
            stats.CurrentDepth, stats.TotalWritten, stats.TotalRead, stats.TotalDropped);
    }
}
