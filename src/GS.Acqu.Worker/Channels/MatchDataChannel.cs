using System.Threading.Channels;
using GS.Acqu.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace GS.Acqu.Worker.Channels;

/// <summary>
/// 賽事資料 Channel
/// 使用 BoundedChannel 實現背壓控制
/// </summary>
public class MatchDataChannel : IDataChannel<MatchInfo>
{
    private readonly Channel<MatchInfo> _channel;
    private readonly ILogger<MatchDataChannel> _logger;
    private readonly int _capacity;

    // 統計計數器
    private long _totalWritten;
    private long _totalRead;
    private long _totalDropped;

    public MatchDataChannel(ILogger<MatchDataChannel> logger)
    {
        _logger = logger;
        _capacity = 10000; // 賽事資料量較少，容量 1 萬筆即可

        var options = new BoundedChannelOptions(_capacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        };

        _channel = Channel.CreateBounded<MatchInfo>(options);

        _logger.LogInformation(
            "MatchDataChannel 已建立: Capacity={Capacity}, FullMode=DropOldest",
            _capacity);
    }

    public ChannelReader<MatchInfo> Reader => _channel.Reader;

    public int Count => _channel.Reader.Count;

    public bool TryWrite(MatchInfo item)
    {
        if (_channel.Writer.TryWrite(item))
        {
            Interlocked.Increment(ref _totalWritten);
            return true;
        }

        Interlocked.Increment(ref _totalDropped);
        return false;
    }

    public async ValueTask WriteAsync(MatchInfo item, CancellationToken cancellationToken = default)
    {
        await _channel.Writer.WriteAsync(item, cancellationToken);
        Interlocked.Increment(ref _totalWritten);
    }

    public async ValueTask<MatchInfo> ReadAsync(CancellationToken cancellationToken = default)
    {
        var item = await _channel.Reader.ReadAsync(cancellationToken);
        Interlocked.Increment(ref _totalRead);
        return item;
    }

    public bool TryRead(out MatchInfo? item)
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

    public IAsyncEnumerable<MatchInfo> ReadAllAsync(CancellationToken cancellationToken = default)
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

