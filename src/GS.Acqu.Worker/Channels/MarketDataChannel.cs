using System.Threading.Channels;
using GS.Acqu.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace GS.Acqu.Worker.Channels;

/// <summary>
/// 盤口資料 Channel
/// 使用 BoundedChannel 實現背壓控制
/// </summary>
public class MarketDataChannel : IDataChannel<MarketInfo>
{
    private readonly Channel<MarketInfo> _channel;
    private readonly ILogger<MarketDataChannel> _logger;
    private readonly int _capacity;

    // 統計計數器
    private long _totalWritten;
    private long _totalRead;
    private long _totalDropped;

    public MarketDataChannel(ILogger<MarketDataChannel> logger)
    {
        _logger = logger;
        _capacity = 50000; // 預設容量 5 萬筆

        var options = new BoundedChannelOptions(_capacity)
        {
            // 滿載時丟棄最舊的資料
            FullMode = BoundedChannelFullMode.DropOldest,
            // 單一消費者 (保證順序)
            SingleReader = true,
            // 多個生產者 (多個串流寫入)
            SingleWriter = false,
            // 允許同步繼續
            AllowSynchronousContinuations = false
        };

        _channel = Channel.CreateBounded<MarketInfo>(options);

        _logger.LogInformation(
            "MarketDataChannel 已建立: Capacity={Capacity}, FullMode=DropOldest",
            _capacity);
    }

    public ChannelReader<MarketInfo> Reader => _channel.Reader;

    public int Count => _channel.Reader.Count;

    public bool TryWrite(MarketInfo item)
    {
        if (_channel.Writer.TryWrite(item))
        {
            Interlocked.Increment(ref _totalWritten);
            return true;
        }

        // 如果 TryWrite 失敗 (理論上 DropOldest 模式不會發生)
        Interlocked.Increment(ref _totalDropped);
        return false;
    }

    public async ValueTask WriteAsync(MarketInfo item, CancellationToken cancellationToken = default)
    {
        await _channel.Writer.WriteAsync(item, cancellationToken);
        Interlocked.Increment(ref _totalWritten);
    }

    public async ValueTask<MarketInfo> ReadAsync(CancellationToken cancellationToken = default)
    {
        var item = await _channel.Reader.ReadAsync(cancellationToken);
        Interlocked.Increment(ref _totalRead);
        return item;
    }

    public bool TryRead(out MarketInfo? item)
    {
        if (_channel.Reader.TryRead(out item))
        {
            Interlocked.Increment(ref _totalRead);
            return true;
        }
        return false;
    }

    public ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken = default)
    {
        return _channel.Reader.WaitToReadAsync(cancellationToken);
    }

    public IAsyncEnumerable<MarketInfo> ReadAllAsync(CancellationToken cancellationToken = default)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }

    public ChannelStats GetStats()
    {
        return new ChannelStats
        {
            TotalWritten = Interlocked.Read(ref _totalWritten),
            TotalRead = Interlocked.Read(ref _totalRead),
            TotalDropped = Interlocked.Read(ref _totalDropped),
            CurrentDepth = _channel.Reader.Count,
            Capacity = _capacity
        };
    }
}

