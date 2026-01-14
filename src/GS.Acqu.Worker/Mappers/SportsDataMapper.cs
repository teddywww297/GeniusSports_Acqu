using GS.Acqu.Domain.Enums;
using SportsData.Service;
using DomainMatchInfo = GS.Acqu.Domain.Entities.MatchInfo;
using DomainMarketInfo = GS.Acqu.Domain.Entities.MarketInfo;
using DomainLeagueInfo = GS.Acqu.Domain.Entities.LeagueInfo;
using DomainTeamInfo = GS.Acqu.Domain.Entities.TeamInfo;
using DomainScoreInfo = GS.Acqu.Domain.Entities.ScoreInfo;
using ProtoMatchData = SportsData.Service.MatchData;
using ProtoMatchFeed = SportsData.Service.MatchFeed;
using ProtoMarketData = SportsData.Service.MarketData;
using ProtoMarketTradingStatus = SportsData.Service.MarketTradingStatus;

namespace GS.Acqu.Worker.Mappers;

/// <summary>
/// Genius Sports Proto 資料轉換器
/// </summary>
public static class SportsDataMapper
{
    #region 聯賽轉換

    /// <summary>
    /// CompetitionData → LeagueInfo
    /// </summary>
    public static DomainLeagueInfo ToLeagueInfo(CompetitionData competition, int sportId)
    {
        return new DomainLeagueInfo
        {
            SourceLeagueId = competition.Id,
            Name = competition.Name,
            NameEn = competition.NameLocalized.TryGetValue("en", out var en) ? en : null,
            NameCn = competition.NameLocalized.TryGetValue("zh-CN", out var cn) ? cn : 
                     competition.NameLocalized.TryGetValue("zh-TW", out var tw) ? tw : null
        };
    }

    #endregion

    #region 賽事轉換

    /// <summary>
    /// MatchData → MatchInfo
    /// </summary>
    public static DomainMatchInfo ToMatchInfo(ProtoMatchData match, int sportId)
    {
        var matchInfo = new DomainMatchInfo
        {
            SourceMatchId = match.MatchId.ToString(),
            SportType = ToSportType(sportId),
            GameType = ToGameType(match.Status),
            Status = ToMatchStatus(match.Status),
            ScheduleTime = match.MatchTime?.ToDateTime().ToLocalTime() ?? DateTime.Now,
            SourceUpdateTime = match.Timestamp?.ToDateTime().ToLocalTime() ?? DateTime.Now,
            LiveTime = match.Clock?.ToTimeSpan().ToString(@"mm\:ss"),
            League = new DomainLeagueInfo
            {
                SourceLeagueId = match.CompetitionId,
                Name = match.Competition
            },
            HomeTeam = new DomainTeamInfo
            {
                SourceTeamId = "", // Genius Sports 沒有提供 TeamId
                Name = match.HomeTeam
            },
            AwayTeam = new DomainTeamInfo
            {
                SourceTeamId = "",
                Name = match.AwayTeam
            }
        };

        // 比分
        if (match.LiveScoreCase == ProtoMatchData.LiveScoreOneofCase.TeamScore)
        {
            matchInfo.Score = new DomainScoreInfo
            {
                HomeScore = match.TeamScore.Home,
                AwayScore = match.TeamScore.Away
            };

            // 節數比分
            if (match.DetailsCase == ProtoMatchData.DetailsOneofCase.Football && match.Football.PeriodScores.Count > 0)
            {
                matchInfo.Score.PeriodScores = string.Join(",",
                    match.Football.PeriodScores.Select(p => $"{p.Home}-{p.Away}"));

                if (match.Football.PeriodScores.Count >= 1)
                {
                    var firstHalf = match.Football.PeriodScores[0];
                    matchInfo.Score.HomeHalfScore = firstHalf.Home;
                    matchInfo.Score.AwayHalfScore = firstHalf.Away;
                }
            }
            else if (match.DetailsCase == ProtoMatchData.DetailsOneofCase.Basketball && match.Basketball.PeriodScores.Count > 0)
            {
                matchInfo.Score.PeriodScores = string.Join(",",
                    match.Basketball.PeriodScores.Select(p => $"{p.Home}-{p.Away}"));
            }
        }

        return matchInfo;
    }

    /// <summary>
    /// MatchFeed → MatchInfo (串流更新)
    /// </summary>
    public static DomainMatchInfo ToMatchInfoFromFeed(ProtoMatchFeed feed, int sportId)
    {
        var matchInfo = new DomainMatchInfo
        {
            SourceMatchId = feed.MatchId.ToString(),
            SportType = ToSportType(sportId),
            GameType = ToGameType(feed.Status),
            Status = ToMatchStatus(feed.Status),
            // MatchFeed 不含 MatchTime，使用 SourceUpdateTime 作為預設值
            ScheduleTime = feed.Timestamp?.ToDateTime().ToLocalTime() ?? DateTime.Now,
            SourceUpdateTime = feed.Timestamp?.ToDateTime().ToLocalTime() ?? DateTime.Now,
            LiveTime = feed.Clock?.ToTimeSpan().ToString(@"mm\:ss"),
            // 串流只有狀態更新，不含聯賽/球隊資訊
            League = new DomainLeagueInfo { SourceLeagueId = "", Name = "" },
            HomeTeam = new DomainTeamInfo { SourceTeamId = "", Name = "" },
            AwayTeam = new DomainTeamInfo { SourceTeamId = "", Name = "" }
        };

        if (feed.LiveScoreCase == ProtoMatchFeed.LiveScoreOneofCase.TeamScore)
        {
            matchInfo.Score = new DomainScoreInfo
            {
                HomeScore = feed.TeamScore.Home,
                AwayScore = feed.TeamScore.Away
            };
        }

        return matchInfo;
    }

    #endregion

    #region 盤口轉換

    /// <summary>
    /// MarketData → MarketInfo
    /// </summary>
    public static DomainMarketInfo ToMarketInfo(string sourceMatchId, ProtoMarketData market)
    {
        var marketInfo = new DomainMarketInfo
        {
            SourceMatchId = sourceMatchId,
            MarketType = ToMarketType(market.Type),
            HalfType = ToHalfType(market.Period),
            Line = ParseDecimal(market.Line),
            // 只有 Suspended 狀態才標記為暫停
            // Default (0) = 尚未開盤，不是暫停
            // Closed (3) = 已關盤，語義上與暫停不同
            IsSuspended = market.TradingStatus == (int)ProtoMarketTradingStatus.Suspended,
            SourceUpdateTime = market.Timestamp?.ToDateTime().ToLocalTime() ?? DateTime.Now
        };

        // 解析賠率 (Selections)
        if (market.Selections.Count > 0)
        {
            var selections = market.Selections.OrderBy(s => s.Id).ToList();

            // 根據盤口類型分配賠率
            switch (marketInfo.MarketType)
            {
                case Domain.Enums.MarketType.Handicap:
                    // 讓球: Home / Away
                    if (selections.Count >= 2)
                    {
                        marketInfo.Odds1 = ParseDecimal(selections[0].Odds) ?? 0;
                        marketInfo.Odds2 = ParseDecimal(selections[1].Odds) ?? 0;
                    }
                    break;

                case Domain.Enums.MarketType.OverUnder:
                    // 大小: Over / Under
                    var over = selections.FirstOrDefault(s => s.Code?.ToLower() == "over");
                    var under = selections.FirstOrDefault(s => s.Code?.ToLower() == "under");
                    marketInfo.Odds1 = ParseDecimal(over?.Odds) ?? 0;
                    marketInfo.Odds2 = ParseDecimal(under?.Odds) ?? 0;
                    break;

                case Domain.Enums.MarketType.MoneyLine:
                    // 勝負盤: Home / Away
                    if (selections.Count >= 2)
                    {
                        var home = selections.FirstOrDefault(s => s.Code?.ToLower() == "home" || s.Code?.ToLower() == "1");
                        var away = selections.FirstOrDefault(s => s.Code?.ToLower() == "away" || s.Code?.ToLower() == "2");
                        var draw = selections.FirstOrDefault(s => s.Code?.ToLower() == "draw" || s.Code?.ToLower() == "x");

                        marketInfo.Odds1 = ParseDecimal(home?.Odds ?? selections[0].Odds) ?? 0;
                        marketInfo.Odds2 = ParseDecimal(away?.Odds ?? selections[1].Odds) ?? 0;
                        marketInfo.Odds3 = ParseDecimal(draw?.Odds);
                    }
                    break;

                case Domain.Enums.MarketType.OddEven:
                    // 單雙: Odd / Even
                    var odd = selections.FirstOrDefault(s => s.Code?.ToLower() == "odd");
                    var even = selections.FirstOrDefault(s => s.Code?.ToLower() == "even");
                    marketInfo.Odds1 = ParseDecimal(odd?.Odds) ?? 0;
                    marketInfo.Odds2 = ParseDecimal(even?.Odds) ?? 0;
                    break;

                default:
                    // 其他: 依序填入
                    if (selections.Count >= 1) marketInfo.Odds1 = ParseDecimal(selections[0].Odds) ?? 0;
                    if (selections.Count >= 2) marketInfo.Odds2 = ParseDecimal(selections[1].Odds) ?? 0;
                    if (selections.Count >= 3) marketInfo.Odds3 = ParseDecimal(selections[2].Odds);
                    break;
            }
        }

        return marketInfo;
    }

    #endregion

    #region 列舉轉換

    /// <summary>
    /// Genius Sports API sportId → DB CatID (SportType)
    /// GS API: 1=Soccer, 3=Basketball (不是2!)
    /// DB CatID: 1=Soccer, 3=Basketball, 4=MLB, 5=Football, 55=Tennis, 82=Hockey, 85=ESports
    /// </summary>
    private static SportType ToSportType(int sportId) => sportId switch
    {
        1 => SportType.Soccer,           // GS Soccer → DB Soccer (1)
        2 => SportType.Basketball,       // GS Basketball (備用)
        3 => SportType.Basketball,       // GS Basketball → DB Basketball (3)
        4 => SportType.Hockey,           // GS Ice Hockey → DB Hockey (82)
        5 => SportType.Football,         // GS American Football → DB Football (5)
        6 => SportType.Tennis,           // GS Tennis → DB Tennis (55)
        7 => SportType.ESports,          // GS Esports → DB ESports (85)
        _ => SportType.Soccer            // 預設足球
    };

    /// <summary>
    /// 賽事狀態 ID → GameType
    /// </summary>
    private static GameType ToGameType(int status) => status switch
    {
        // 未開始的賽事
        0 or 1 => GameType.Early,
        // 進行中的賽事
        >= 2 and <= 10 => GameType.Live,
        // 已結束
        _ => GameType.Today
    };

    /// <summary>
    /// 賽事狀態 ID → MatchStatus
    /// </summary>
    private static MatchStatus ToMatchStatus(int status) => status switch
    {
        0 => MatchStatus.NotStarted,
        1 => MatchStatus.NotStarted,
        2 => MatchStatus.Live,
        3 => MatchStatus.Paused,
        4 => MatchStatus.HalfTime,
        5 => MatchStatus.Ended,
        6 => MatchStatus.Cancelled,
        7 => MatchStatus.Postponed,
        8 => MatchStatus.Interrupted,
        _ => MatchStatus.NotStarted
    };

    /// <summary>
    /// 盤口類型 ID → MarketType
    /// </summary>
    private static Domain.Enums.MarketType ToMarketType(int type) => type switch
    {
        10 => Domain.Enums.MarketType.MoneyLine,  // Moneyline
        11 => Domain.Enums.MarketType.MoneyLine,  // MatchResult (1X2)
        20 => Domain.Enums.MarketType.Handicap,   // Handicap
        30 => Domain.Enums.MarketType.OverUnder,  // OverUnder
        40 => Domain.Enums.MarketType.OddEven,    // OddEven
        _ => Domain.Enums.MarketType.Handicap
    };

    /// <summary>
    /// 盤口時段 ID → HalfType
    /// </summary>
    private static HalfType ToHalfType(int period) => period switch
    {
        1 => HalfType.FullTime,
        2 => HalfType.FirstHalf,
        3 => HalfType.SecondHalf,
        _ => HalfType.FullTime
    };

    #endregion

    #region 工具方法

    /// <summary>
    /// 字串轉 Decimal
    /// </summary>
    private static decimal? ParseDecimal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return decimal.TryParse(value, out var result) ? result : null;
    }

    #endregion
}

