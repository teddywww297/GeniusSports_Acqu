using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Grpc.Core;
using Grpc.Net.Client;
using GS.Acqu.Domain.Entities;
using GS.Acqu.Domain.Enums;
using GS.Acqu.Domain.Interfaces;
using GS.Acqu.Worker.Mappers;
using GS.Acqu.Worker.Options;
using SportsData.Service;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GS.Acqu.Worker.Clients;

/// <summary>
/// Genius Sports gRPC Client 實作
/// </summary>
public class GeniusSportsClient : IGeniusSportsClient, IDisposable
{
    private readonly GeniusSportsOptions _options;
    private readonly ILogger<GeniusSportsClient> _logger;
    private readonly IMatchInfoCacheService _matchInfoCache;
    private readonly GrpcChannel _channel;
    private readonly Service.ServiceClient _client;

    public GeniusSportsClient(
        IOptions<GeniusSportsOptions> options,
        ILogger<GeniusSportsClient> logger,
        IMatchInfoCacheService matchInfoCache)
    {
        _options = options.Value;
        _logger = logger;
        _matchInfoCache = matchInfoCache;

        // 建立 gRPC Channel
        _channel = GrpcChannel.ForAddress(_options.Endpoint, new GrpcChannelOptions
        {
            HttpHandler = new SocketsHttpHandler
            {
                PooledConnectionIdleTimeout = Timeout.InfiniteTimeSpan,
                KeepAlivePingDelay = TimeSpan.FromSeconds(60),
                KeepAlivePingTimeout = TimeSpan.FromSeconds(30),
                EnableMultipleHttp2Connections = true
            }
        });

        _client = new Service.ServiceClient(_channel);

        _logger.LogInformation("Genius Sports gRPC Client 已建立: {Endpoint}", _options.Endpoint);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<(int Id, string Name)>> GetSportsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new GetSportsRequest
            {
                Lang = _options.Language
            };

            var response = await _client.GetSportsAsync(request, cancellationToken: cancellationToken);

            return response.Sports.Select(s => (s.Id, s.Name));
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "取得球種清單失敗");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<LeagueInfo>> GetCompetitionsAsync(int sportId, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new CompetitionsRequest
            {
                SportId = sportId,
                Lang = _options.Language
            };

            var response = await _client.GetCompetitionsAsync(request, cancellationToken: cancellationToken);

            return response.Items.Select(c => SportsDataMapper.ToLeagueInfo(c, sportId));
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "取得聯賽清單失敗: SportId={SportId}", sportId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<MatchInfo>> GetMatchesAsync(
        int sportId, 
        DateTime startTime, 
        DateTime endTime, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new MatchesRequest
            {
                SportId = sportId,
                StartTime = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(startTime.ToUniversalTime()),
                EndTime = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(endTime.ToUniversalTime()),
                Lang = _options.Language
            };

            var response = await _client.GetMatchesAsync(request, cancellationToken: cancellationToken);

            return response.Items.Select(m => SportsDataMapper.ToMatchInfo(m, sportId));
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "取得賽事清單失敗: SportId={SportId}", sportId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<(int MatchId, IEnumerable<MarketInfo> Markets)>> GetPreMatchMarketsAsync(
        int sportId, 
        string startDate, 
        string endDate,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new PreMatchMarketsRequest
            {
                SportId = sportId,
                StartDate = startDate,
                EndDate = endDate,
                MarketPeriod = MarketPeriod.FullTime,  // 1: 全場
                MarketTypes = { 
                    SportsData.Service.MarketType.Moneyline,   // 10: 勝負盤
                    SportsData.Service.MarketType.Handicap,    // 20: 讓分盤
                    SportsData.Service.MarketType.OverUnder,   // 30: 大小盤
                    SportsData.Service.MarketType.OddEven      // 40: 單雙盤
                },
                TradingStatuses = {
                    MarketTradingStatus.Open,      // 1: 開盤
                    MarketTradingStatus.Suspended, // 2: 暫停
                    MarketTradingStatus.Closed     // 3: 關盤
                }
            };

            _logger.LogDebug("呼叫 GetPreMatchMarkets gRPC: SportId={SportId}, StartDate={StartDate}, EndDate={EndDate}, MarketTypes=[Moneyline,MatchResult,Handicap,OverUnder,OddEven]",
                sportId, startDate, endDate);

            var response = await _client.GetPreMatchMarketsAsync(request, cancellationToken: cancellationToken);

            var itemCount = response.Items.Count;
            var totalMarkets = response.Items.Sum(m => m.Markets.Count);
            
            _logger.LogInformation("GetPreMatchMarkets 回傳: SportId={SportId}, 賽事數={MatchCount}, 盤口數={MarketCount}",
                sportId, itemCount, totalMarkets);

            // 重要：使用 ToList() 立即具體化，避免延遲執行時 protobuf 物件已被 GC
            return response.Items.Select(m => (
                m.MatchId,
                (IEnumerable<MarketInfo>)m.Markets
                    .Select(market => SportsDataMapper.ToMarketInfo(m.MatchId.ToString(), market))
                    .ToList()
            )).ToList();
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "取得早盤盤口失敗: SportId={SportId}", sportId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<(int MatchId, IEnumerable<MarketInfo> Markets)>> GetInPlayMarketsAsync(
        int sportId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new InPlayMarketsRequest
            {
                SportId = sportId
            };

            var response = await _client.GetInPlayMarketsAsync(request, cancellationToken: cancellationToken);

            // 重要：使用 ToList() 立即具體化，避免延遲執行時 protobuf 物件已被 GC
            return response.Items.Select(m => (
                m.MatchId,
                (IEnumerable<MarketInfo>)m.Markets
                    .Select(market => SportsDataMapper.ToMarketInfo(m.MatchId.ToString(), market))
                    .ToList()
            )).ToList();
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "取得走地盤口失敗: SportId={SportId}", sportId);
            throw;
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<MatchInfo> WatchMatchesAsync(
        int sportId, 
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var channel = Channel.CreateUnbounded<MatchInfo>();

        // 背景任務：從 gRPC 串流讀取資料
        // 使用 ContinueWith 觀察例外，避免 fire-and-forget 導致例外被忽略
        var producerTask = Task.Run(async () =>
        {
            await SubscribeMatchesInternalAsync(sportId, channel.Writer, cancellationToken);
        }, cancellationToken);

        // 觀察背景任務的例外
        _ = producerTask.ContinueWith(t =>
        {
            if (t.IsFaulted && t.Exception != null)
            {
                _logger.LogError(t.Exception, "賽事串流訂閱背景任務發生例外: SportId={SportId}", sportId);
            }
        }, TaskContinuationOptions.OnlyOnFaulted);

        // 從 Channel 讀取資料
        await foreach (var item in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return item;
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<(int MatchId, IEnumerable<MarketInfo> Markets)> WatchPreMatchMarketsAsync(
        int sportId, 
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var channel = Channel.CreateUnbounded<(int MatchId, IEnumerable<MarketInfo> Markets)>();

        // 背景任務：從 gRPC 串流讀取資料
        // 使用 ContinueWith 觀察例外，避免 fire-and-forget 導致例外被忽略
        var producerTask = Task.Run(async () =>
        {
            await SubscribePreMatchMarketsInternalAsync(sportId, channel.Writer, cancellationToken);
        }, cancellationToken);

        // 觀察背景任務的例外
        _ = producerTask.ContinueWith(t =>
        {
            if (t.IsFaulted && t.Exception != null)
            {
                _logger.LogError(t.Exception, "早盤盤口串流訂閱背景任務發生例外: SportId={SportId}", sportId);
            }
        }, TaskContinuationOptions.OnlyOnFaulted);

        // 從 Channel 讀取資料
        await foreach (var item in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return item;
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<(int MatchId, IEnumerable<MarketInfo> Markets)> WatchInPlayMarketsAsync(
        int sportId, 
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var channel = Channel.CreateUnbounded<(int MatchId, IEnumerable<MarketInfo> Markets)>();

        // 背景任務：從 gRPC 串流讀取資料
        // 使用 ContinueWith 觀察例外，避免 fire-and-forget 導致例外被忽略
        var producerTask = Task.Run(async () =>
        {
            await SubscribeInPlayMarketsInternalAsync(sportId, channel.Writer, cancellationToken);
        }, cancellationToken);

        // 觀察背景任務的例外
        _ = producerTask.ContinueWith(t =>
        {
            if (t.IsFaulted && t.Exception != null)
            {
                _logger.LogError(t.Exception, "走地盤口串流訂閱背景任務發生例外: SportId={SportId}", sportId);
            }
        }, TaskContinuationOptions.OnlyOnFaulted);

        // 從 Channel 讀取資料
        await foreach (var item in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return item;
        }
    }

    #region 內部串流訂閱方法

    private async Task SubscribeMatchesInternalAsync(
        int sportId,
        ChannelWriter<MatchInfo> writer,
        CancellationToken cancellationToken)
    {
        Exception? completionException = null;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var request = new WatchMatchRequest { SportId = sportId };
                    using var stream = _client.WatchMatches(request, cancellationToken: cancellationToken);

                    _logger.LogInformation("開始訂閱賽事變更: SportId={SportId}", sportId);

                    await foreach (var feed in stream.ResponseStream.ReadAllAsync(cancellationToken))
                    {
                        // 先轉換串流資料
                        var matchInfo = SportsDataMapper.ToMatchInfoFromFeed(feed, sportId);
                        
                        // 從快取補齊聯賽/球隊名稱
                        var enrichedMatchInfo = _matchInfoCache.EnrichFromCache(matchInfo);
                        
                        await writer.WriteAsync(enrichedMatchInfo, cancellationToken);
                    }
                }
                catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
                {
                    _logger.LogInformation("賽事訂閱已取消: SportId={SportId}", sportId);
                    break;
                }
                catch (RpcException ex)
                {
                    _logger.LogError(ex, "賽事訂閱中斷: SportId={SportId}, 將在 {Seconds} 秒後重連",
                        sportId, _options.ReconnectIntervalSeconds);
                    await Task.Delay(TimeSpan.FromSeconds(_options.ReconnectIntervalSeconds), cancellationToken);
                }
                catch (Google.Protobuf.InvalidProtocolBufferException ex)
                {
                    // Protocol Buffer 解析錯誤，通常是連線中斷導致訊息不完整
                    _logger.LogWarning(ex, "賽事訂閱 ProtoBuf 解析錯誤: SportId={SportId}, 將在 {Seconds} 秒後重連",
                        sportId, _options.ReconnectIntervalSeconds);
                    await Task.Delay(TimeSpan.FromSeconds(_options.ReconnectIntervalSeconds), cancellationToken);
                }
                catch (IOException ex)
                {
                    // 網路 IO 錯誤
                    _logger.LogWarning(ex, "賽事訂閱網路錯誤: SportId={SportId}, 將在 {Seconds} 秒後重連",
                        sportId, _options.ReconnectIntervalSeconds);
                    await Task.Delay(TimeSpan.FromSeconds(_options.ReconnectIntervalSeconds), cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 正常取消，不視為錯誤
        }
        catch (Exception ex)
        {
            // 捕獲非預期的例外，傳遞給消費者
            completionException = ex;
            _logger.LogError(ex, "賽事訂閱發生非預期例外: SportId={SportId}", sportId);
        }
        finally
        {
            // 將例外傳遞給 Channel，讓消費者知道串流失敗的原因
            writer.Complete(completionException);
        }
    }

    private async Task SubscribePreMatchMarketsInternalAsync(
        int sportId,
        ChannelWriter<(int MatchId, IEnumerable<MarketInfo> Markets)> writer,
        CancellationToken cancellationToken)
    {
        Exception? completionException = null;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var request = new WatchMarketsRequest { SportId = sportId };
                    using var stream = _client.WatchPreMatchMarkets(request, cancellationToken: cancellationToken);

                    _logger.LogInformation("開始訂閱早盤盤口變更: SportId={SportId}", sportId);

                    await foreach (var feed in stream.ResponseStream.ReadAllAsync(cancellationToken))
                    {
                        // 重要：使用 ToList() 立即具體化，避免延遲執行時 feed 已被 gRPC 回收
                        var markets = feed.Markets
                            .Select(m => SportsDataMapper.ToMarketInfo(feed.MatchId.ToString(), m))
                            .ToList();
                        
                        await writer.WriteAsync((feed.MatchId, markets), cancellationToken);
                    }
                }
                catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
                {
                    _logger.LogInformation("早盤盤口訂閱已取消: SportId={SportId}", sportId);
                    break;
                }
                catch (RpcException ex)
                {
                    _logger.LogError(ex, "早盤盤口訂閱中斷: SportId={SportId}, 將在 {Seconds} 秒後重連",
                        sportId, _options.ReconnectIntervalSeconds);
                    await Task.Delay(TimeSpan.FromSeconds(_options.ReconnectIntervalSeconds), cancellationToken);
                }
                catch (Google.Protobuf.InvalidProtocolBufferException ex)
                {
                    _logger.LogWarning(ex, "早盤盤口訂閱 ProtoBuf 解析錯誤: SportId={SportId}, 將在 {Seconds} 秒後重連",
                        sportId, _options.ReconnectIntervalSeconds);
                    await Task.Delay(TimeSpan.FromSeconds(_options.ReconnectIntervalSeconds), cancellationToken);
                }
                catch (IOException ex)
                {
                    _logger.LogWarning(ex, "早盤盤口訂閱網路錯誤: SportId={SportId}, 將在 {Seconds} 秒後重連",
                        sportId, _options.ReconnectIntervalSeconds);
                    await Task.Delay(TimeSpan.FromSeconds(_options.ReconnectIntervalSeconds), cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 正常取消，不視為錯誤
        }
        catch (Exception ex)
        {
            // 捕獲非預期的例外，傳遞給消費者
            completionException = ex;
            _logger.LogError(ex, "早盤盤口訂閱發生非預期例外: SportId={SportId}", sportId);
        }
        finally
        {
            // 將例外傳遞給 Channel，讓消費者知道串流失敗的原因
            writer.Complete(completionException);
        }
    }

    private async Task SubscribeInPlayMarketsInternalAsync(
        int sportId,
        ChannelWriter<(int MatchId, IEnumerable<MarketInfo> Markets)> writer,
        CancellationToken cancellationToken)
    {
        Exception? completionException = null;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var request = new WatchMarketsRequest { SportId = sportId };
                    using var stream = _client.WatchInPlayMarkets(request, cancellationToken: cancellationToken);

                    _logger.LogInformation("開始訂閱走地盤口變更: SportId={SportId}", sportId);

                    await foreach (var feed in stream.ResponseStream.ReadAllAsync(cancellationToken))
                    {
                        // 重要：使用 ToList() 立即具體化，避免延遲執行時 feed 已被 gRPC 回收
                        var markets = feed.Markets
                            .Select(m => SportsDataMapper.ToMarketInfo(feed.MatchId.ToString(), m))
                            .ToList();
                        
                        await writer.WriteAsync((feed.MatchId, markets), cancellationToken);
                    }
                }
                catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
                {
                    _logger.LogInformation("走地盤口訂閱已取消: SportId={SportId}", sportId);
                    break;
                }
                catch (RpcException ex)
                {
                    _logger.LogError(ex, "走地盤口訂閱中斷: SportId={SportId}, 將在 {Seconds} 秒後重連",
                        sportId, _options.ReconnectIntervalSeconds);
                    await Task.Delay(TimeSpan.FromSeconds(_options.ReconnectIntervalSeconds), cancellationToken);
                }
                catch (Google.Protobuf.InvalidProtocolBufferException ex)
                {
                    _logger.LogWarning(ex, "走地盤口訂閱 ProtoBuf 解析錯誤: SportId={SportId}, 將在 {Seconds} 秒後重連",
                        sportId, _options.ReconnectIntervalSeconds);
                    await Task.Delay(TimeSpan.FromSeconds(_options.ReconnectIntervalSeconds), cancellationToken);
                }
                catch (IOException ex)
                {
                    _logger.LogWarning(ex, "走地盤口訂閱網路錯誤: SportId={SportId}, 將在 {Seconds} 秒後重連",
                        sportId, _options.ReconnectIntervalSeconds);
                    await Task.Delay(TimeSpan.FromSeconds(_options.ReconnectIntervalSeconds), cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 正常取消，不視為錯誤
        }
        catch (Exception ex)
        {
            // 捕獲非預期的例外，傳遞給消費者
            completionException = ex;
            _logger.LogError(ex, "走地盤口訂閱發生非預期例外: SportId={SportId}", sportId);
        }
        finally
        {
            // 將例外傳遞給 Channel，讓消費者知道串流失敗的原因
            writer.Complete(completionException);
        }
    }

    #endregion

    public void Dispose()
    {
        _channel.Dispose();
        _logger.LogInformation("Genius Sports gRPC Client 已釋放");
    }
}
