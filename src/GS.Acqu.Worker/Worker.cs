using GS.Acqu.Application.Interfaces;
using GS.Acqu.Domain.Interfaces;

namespace GS.Acqu.Worker;

/// <summary>
/// GS ACQU 背景服務 (定時任務)
/// </summary>
public class Worker : BackgroundService
{
    private readonly IMatcherService _matcher;
    private readonly IUnmatchedEventStore _unmatchedStore;
    private readonly ILogger<Worker> _logger;
    
    private readonly TimeSpan _refreshInterval = TimeSpan.FromSeconds(60);
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromHours(1);
    private readonly TimeSpan _unmatchedExpiry = TimeSpan.FromHours(24);

    public Worker(
        IMatcherService matcher,
        IUnmatchedEventStore unmatchedStore,
        ILogger<Worker> logger)
    {
        _matcher = matcher;
        _unmatchedStore = unmatchedStore;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("GS ACQU Worker 背景任務啟動");

        var lastRefresh = DateTime.Now;
        var lastCleanup = DateTime.Now;

        // 背景任務迴圈
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

            // 定時重新載入對點快取
            if (DateTime.Now - lastRefresh >= _refreshInterval)
            {
                try
                {
                    await _matcher.RefreshCacheAsync();
                    lastRefresh = DateTime.Now;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "重新載入對點快取失敗");
                }
            }

            // 定時清理過期的未對點賽事
            if (DateTime.Now - lastCleanup >= _cleanupInterval)
            {
                try
                {
                    await _unmatchedStore.ClearExpiredAsync(_unmatchedExpiry);
                    var count = await _unmatchedStore.GetCountAsync();
                    _logger.LogInformation("清理過期未對點賽事完成，剩餘 {Count} 筆", count);
                    lastCleanup = DateTime.Now;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "清理過期未對點賽事失敗");
                }
            }
        }

        _logger.LogInformation("GS ACQU Worker 背景任務已停止");
    }
}
