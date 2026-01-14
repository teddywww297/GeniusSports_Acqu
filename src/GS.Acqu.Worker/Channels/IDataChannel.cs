using System.Threading.Channels;

namespace GS.Acqu.Worker.Channels;

/// <summary>
/// 資料 Channel 介面
/// 提供高效能的生產者/消費者模式
/// </summary>
/// <typeparam name="T">資料類型</typeparam>
public interface IDataChannel<T>
{
    /// <summary>
    /// 寫入資料 (非阻塞)
    /// </summary>
    /// <param name="item">資料項目</param>
    /// <returns>是否成功寫入</returns>
    bool TryWrite(T item);

    /// <summary>
    /// 寫入資料 (非同步，可等待)
    /// </summary>
    ValueTask WriteAsync(T item, CancellationToken cancellationToken = default);

    /// <summary>
    /// 讀取單筆資料
    /// </summary>
    ValueTask<T> ReadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 嘗試讀取資料
    /// </summary>
    bool TryRead(out T? item);

    /// <summary>
    /// 等待資料可讀取
    /// </summary>
    ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 讀取所有資料的非同步列舉
    /// </summary>
    IAsyncEnumerable<T> ReadAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Channel 的 Reader (供進階使用)
    /// </summary>
    ChannelReader<T> Reader { get; }

    /// <summary>
    /// 目前 Channel 中的項目數量 (估計值)
    /// </summary>
    int Count { get; }

    /// <summary>
    /// 取得統計資訊
    /// </summary>
    ChannelStats GetStats();
}

/// <summary>
/// Channel 統計資訊
/// </summary>
public class ChannelStats
{
    /// <summary>
    /// 總寫入數量
    /// </summary>
    public long TotalWritten { get; set; }

    /// <summary>
    /// 總讀取數量
    /// </summary>
    public long TotalRead { get; set; }

    /// <summary>
    /// 丟棄數量 (因滿載)
    /// </summary>
    public long TotalDropped { get; set; }

    /// <summary>
    /// 目前佇列深度
    /// </summary>
    public int CurrentDepth { get; set; }

    /// <summary>
    /// Channel 容量
    /// </summary>
    public int Capacity { get; set; }
}

