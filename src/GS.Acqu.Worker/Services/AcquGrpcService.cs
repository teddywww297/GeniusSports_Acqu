using Grpc.Core;
using GS.Acqu.Application.Interfaces;
using GS.Acqu.Domain.Enums;
using GS.Acqu.Worker.Protos;
using DomainMatchStatus = GS.Acqu.Domain.Enums.MatchStatus;
using DomainSportType = GS.Acqu.Domain.Enums.SportType;
using DomainGameType = GS.Acqu.Domain.Enums.GameType;
using DomainMarketType = GS.Acqu.Domain.Enums.MarketType;
using DomainHalfType = GS.Acqu.Domain.Enums.HalfType;
using DomainMatchInfo = GS.Acqu.Domain.Entities.MatchInfo;
using DomainMatchResult = GS.Acqu.Domain.Entities.MatchResult;
using DomainLeagueInfo = GS.Acqu.Domain.Entities.LeagueInfo;
using DomainTeamInfo = GS.Acqu.Domain.Entities.TeamInfo;
using DomainScoreInfo = GS.Acqu.Domain.Entities.ScoreInfo;

namespace GS.Acqu.Worker.Services;

/// <summary>
/// gRPC 資料採集服務實作
/// </summary>
public class AcquGrpcService : AcquService.AcquServiceBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AcquGrpcService> _logger;

    public AcquGrpcService(
        IServiceProvider serviceProvider,
        ILogger<AcquGrpcService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// 處理賽事資訊推送
    /// </summary>
    public override async Task<ProcessResponse> PushMatchInfo(
        MatchInfoRequest request,
        ServerCallContext context)
    {
        _logger.LogDebug("收到賽事資訊: SourceMatchId={SourceMatchId}", request.SourceMatchId);

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<IMatchInfoHandler>();

            var matchInfo = MapToMatchInfo(request);
            var result = await handler.HandleAsync(matchInfo, context.CancellationToken);

            return CreateResponse(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "處理賽事資訊失敗: SourceMatchId={SourceMatchId}", request.SourceMatchId);
            return new ProcessResponse
            {
                Success = false,
                ResultCode = ProcessResultCode.DatabaseError,
                Message = ex.Message
            };
        }
    }

    /// <summary>
    /// 處理盤口賠率推送
    /// </summary>
    public override async Task<ProcessResponse> PushMarketOdds(
        MarketOddsRequest request,
        ServerCallContext context)
    {
        _logger.LogDebug("收到盤口賠率: SourceMatchId={SourceMatchId}, Count={Count}",
            request.SourceMatchId, request.Markets.Count);

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<IMarketOddsHandler>();

            var markets = request.Markets.Select(m => MapToMarketInfo(request.SourceMatchId, m)).ToList();
            var result = await handler.HandleAsync(markets, context.CancellationToken);

            return CreateResponse(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "處理盤口賠率失敗: SourceMatchId={SourceMatchId}", request.SourceMatchId);
            return new ProcessResponse
            {
                Success = false,
                ResultCode = ProcessResultCode.DatabaseError,
                Message = ex.Message
            };
        }
    }

    /// <summary>
    /// 處理比賽結果推送
    /// </summary>
    public override async Task<ProcessResponse> PushMatchResult(
        MatchResultRequest request,
        ServerCallContext context)
    {
        _logger.LogDebug("收到比賽結果: SourceMatchId={SourceMatchId}", request.SourceMatchId);

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<IMatchResultHandler>();

            var matchResult = MapToMatchResult(request);
            var result = await handler.HandleAsync(matchResult, context.CancellationToken);

            return CreateResponse(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "處理比賽結果失敗: SourceMatchId={SourceMatchId}", request.SourceMatchId);
            return new ProcessResponse
            {
                Success = false,
                ResultCode = ProcessResultCode.DatabaseError,
                Message = ex.Message
            };
        }
    }

    /// <summary>
    /// 串流處理賽事資訊 (高效能模式)
    /// </summary>
    public override async Task StreamMatchInfo(
        IAsyncStreamReader<MatchInfoRequest> requestStream,
        IServerStreamWriter<ProcessResponse> responseStream,
        ServerCallContext context)
    {
        _logger.LogInformation("開始串流接收賽事資訊");
        var count = 0;

        try
        {
            await foreach (var request in requestStream.ReadAllAsync(context.CancellationToken))
            {
                using var scope = _serviceProvider.CreateScope();
                var handler = scope.ServiceProvider.GetRequiredService<IMatchInfoHandler>();

                var matchInfo = MapToMatchInfo(request);
                var result = await handler.HandleAsync(matchInfo, context.CancellationToken);

                await responseStream.WriteAsync(CreateResponse(result));
                count++;
            }

            _logger.LogInformation("串流賽事資訊處理完成: 共 {Count} 筆", count);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("串流賽事資訊被取消: 已處理 {Count} 筆", count);
        }
    }

    /// <summary>
    /// 串流處理盤口賠率 (高效能模式)
    /// </summary>
    public override async Task StreamMarketOdds(
        IAsyncStreamReader<MarketOddsRequest> requestStream,
        IServerStreamWriter<ProcessResponse> responseStream,
        ServerCallContext context)
    {
        _logger.LogInformation("開始串流接收盤口賠率");
        var count = 0;

        try
        {
            await foreach (var request in requestStream.ReadAllAsync(context.CancellationToken))
            {
                using var scope = _serviceProvider.CreateScope();
                var handler = scope.ServiceProvider.GetRequiredService<IMarketOddsHandler>();

                var markets = request.Markets.Select(m => MapToMarketInfo(request.SourceMatchId, m)).ToList();
                var result = await handler.HandleAsync(markets, context.CancellationToken);

                await responseStream.WriteAsync(CreateResponse(result));
                count++;
            }

            _logger.LogInformation("串流盤口賠率處理完成: 共 {Count} 筆", count);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("串流盤口賠率被取消: 已處理 {Count} 筆", count);
        }
    }

    #region 型別轉換方法

    private static DomainMatchInfo MapToMatchInfo(MatchInfoRequest request)
    {
        var matchInfo = new DomainMatchInfo
        {
            SourceMatchId = request.SourceMatchId,
            SportType = MapSportType(request.SportType),
            GameType = MapGameType(request.GameType),
            Status = MapMatchStatus(request.Status),
            ScheduleTime = DateTimeOffset.FromUnixTimeMilliseconds(request.ScheduleTime).LocalDateTime,
            SourceUpdateTime = DateTimeOffset.FromUnixTimeMilliseconds(request.SourceUpdateTime).LocalDateTime,
            LiveTime = request.HasLiveTime ? request.LiveTime : null,
            League = new DomainLeagueInfo
            {
                SourceLeagueId = request.League.SourceLeagueId,
                Name = request.League.Name,
                NameEn = request.League.HasNameEn ? request.League.NameEn : null,
                NameCn = request.League.HasNameCn ? request.League.NameCn : null
            },
            HomeTeam = new DomainTeamInfo
            {
                SourceTeamId = request.HomeTeam.SourceTeamId,
                Name = request.HomeTeam.Name,
                NameEn = request.HomeTeam.HasNameEn ? request.HomeTeam.NameEn : null,
                NameCn = request.HomeTeam.HasNameCn ? request.HomeTeam.NameCn : null
            },
            AwayTeam = new DomainTeamInfo
            {
                SourceTeamId = request.AwayTeam.SourceTeamId,
                Name = request.AwayTeam.Name,
                NameEn = request.AwayTeam.HasNameEn ? request.AwayTeam.NameEn : null,
                NameCn = request.AwayTeam.HasNameCn ? request.AwayTeam.NameCn : null
            }
        };

        if (request.Score != null)
        {
            matchInfo.Score = new DomainScoreInfo
            {
                HomeScore = request.Score.HomeScore,
                AwayScore = request.Score.AwayScore,
                HomeHalfScore = request.Score.HasHomeHalfScore ? request.Score.HomeHalfScore : null,
                AwayHalfScore = request.Score.HasAwayHalfScore ? request.Score.AwayHalfScore : null,
                PeriodScores = request.Score.HasPeriodScores ? request.Score.PeriodScores : null
            };
        }

        return matchInfo;
    }

    private static GS.Acqu.Domain.Entities.MarketInfo MapToMarketInfo(string sourceMatchId, Protos.MarketInfo market)
    {
        return new GS.Acqu.Domain.Entities.MarketInfo
        {
            SourceMatchId = sourceMatchId,
            MarketType = MapMarketType(market.MarketType),
            HalfType = MapHalfType(market.HalfType),
            Line = market.HasLine ? (decimal)market.Line : null,
            Odds1 = (decimal)market.Odds1,
            Odds2 = (decimal)market.Odds2,
            Odds3 = market.HasOdds3 ? (decimal)market.Odds3 : null,
            IsSuspended = market.IsSuspended,
            SourceUpdateTime = DateTimeOffset.FromUnixTimeMilliseconds(market.SourceUpdateTime).LocalDateTime
        };
    }

    private static DomainMatchResult MapToMatchResult(MatchResultRequest request)
    {
        return new DomainMatchResult
        {
            SourceMatchId = request.SourceMatchId,
            FinalStatus = MapMatchStatus(request.FinalStatus),
            HomeScore = request.HomeScore,
            AwayScore = request.AwayScore,
            HomeHalfScore = request.HasHomeHalfScore ? request.HomeHalfScore : null,
            AwayHalfScore = request.HasAwayHalfScore ? request.AwayHalfScore : null,
            PeriodScores = request.HasPeriodScores ? request.PeriodScores : null,
            EndTime = DateTimeOffset.FromUnixTimeMilliseconds(request.EndTime).LocalDateTime
        };
    }

    private static ProcessResponse CreateResponse(ProcessResult result)
    {
        return new ProcessResponse
        {
            Success = result == ProcessResult.Success || result == ProcessResult.Skipped,
            ResultCode = MapProcessResultCode(result),
            Message = result.ToString()
        };
    }

    private static DomainSportType MapSportType(Protos.SportType sportType) => sportType switch
    {
        Protos.SportType.Soccer => DomainSportType.Soccer,
        Protos.SportType.Basketball => DomainSportType.Basketball,
        Protos.SportType.Baseball => DomainSportType.MLB,
        Protos.SportType.IceHockey => DomainSportType.Hockey,
        Protos.SportType.AmericanFootball => DomainSportType.Football,
        Protos.SportType.Tennis => DomainSportType.Tennis,
        Protos.SportType.Esports => DomainSportType.ESports,
        _ => DomainSportType.Soccer
    };

    private static DomainGameType MapGameType(Protos.GameType gameType) => gameType switch
    {
        Protos.GameType.Early => DomainGameType.Early,
        Protos.GameType.Today => DomainGameType.Today,
        Protos.GameType.Live => DomainGameType.Live,
        _ => DomainGameType.Early
    };

    private static DomainMatchStatus MapMatchStatus(Protos.MatchStatus status) => status switch
    {
        Protos.MatchStatus.NotStarted => DomainMatchStatus.NotStarted,
        Protos.MatchStatus.InProgress => DomainMatchStatus.Live,
        Protos.MatchStatus.Paused => DomainMatchStatus.Paused,
        Protos.MatchStatus.HalfTime => DomainMatchStatus.HalfTime,
        Protos.MatchStatus.Ended => DomainMatchStatus.Ended,
        Protos.MatchStatus.Cancelled => DomainMatchStatus.Cancelled,
        Protos.MatchStatus.Postponed => DomainMatchStatus.Postponed,
        Protos.MatchStatus.Interrupted => DomainMatchStatus.Interrupted,
        _ => DomainMatchStatus.NotStarted
    };

    private static DomainMarketType MapMarketType(Protos.MarketType marketType) => marketType switch
    {
        Protos.MarketType.Handicap => DomainMarketType.Handicap,
        Protos.MarketType.OverUnder => DomainMarketType.OverUnder,
        Protos.MarketType.MoneyLine => DomainMarketType.MoneyLine,
        Protos.MarketType.CorrectScore => DomainMarketType.CorrectScore,
        Protos.MarketType.HalfFullTime => DomainMarketType.HalfFullTime,
        Protos.MarketType.OddEven => DomainMarketType.OddEven,
        Protos.MarketType.TotalGoals => DomainMarketType.TotalGoals,
        _ => DomainMarketType.Handicap
    };

    private static DomainHalfType MapHalfType(Protos.HalfType halfType) => halfType switch
    {
        Protos.HalfType.FullTime => DomainHalfType.FullTime,
        Protos.HalfType.FirstHalf => DomainHalfType.FirstHalf,
        Protos.HalfType.SecondHalf => DomainHalfType.SecondHalf,
        _ => DomainHalfType.FullTime
    };

    private static ProcessResultCode MapProcessResultCode(ProcessResult result) => result switch
    {
        ProcessResult.Success => ProcessResultCode.Success,
        ProcessResult.MatchNotFound => ProcessResultCode.MatchNotFound,
        ProcessResult.LeagueNotMatched => ProcessResultCode.LeagueNotMatched,
        ProcessResult.TeamNotMatched => ProcessResultCode.TeamNotMatched,
        ProcessResult.InvalidData => ProcessResultCode.InvalidData,
        ProcessResult.DatabaseError => ProcessResultCode.DatabaseError,
        ProcessResult.Skipped => ProcessResultCode.Skipped,
        _ => ProcessResultCode.DatabaseError
    };

    #endregion
}

