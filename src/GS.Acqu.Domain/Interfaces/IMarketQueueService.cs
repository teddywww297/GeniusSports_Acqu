using GS.Acqu.Domain.Entities;

namespace GS.Acqu.Domain.Interfaces;

/// <summary>
/// 盤口批次隊列服務介面
/// </summary>
public interface IMarketQueueService
{
    /// <summary>
    /// 將盤口放入更新隊列
    /// </summary>
    void EnqueueUpdate(MarketDetailCache market);

    /// <summary>
    /// 將 SQL 放入執行隊列
    /// </summary>
    void EnqueueSql(string sql);

    /// <summary>
    /// 更新隊列數量
    /// </summary>
    int UpdateQueueCount { get; }

    /// <summary>
    /// SQL 隊列數量
    /// </summary>
    int SqlQueueCount { get; }

    /// <summary>
    /// 啟動背景處理
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 停止背景處理
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken);
}

