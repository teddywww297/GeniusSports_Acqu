using GS.Acqu.Domain.Enums;

namespace GS.Acqu.Domain.Entities;

/// <summary>
/// 賽事資訊
/// </summary>
public class MatchInfo
{
    /// <summary>
    /// 來源賽事編號
    /// </summary>
    public string SourceMatchId { get; set; } = string.Empty;

    /// <summary>
    /// 球種類型
    /// </summary>
    public SportType SportType { get; set; }

    /// <summary>
    /// 賽事類型 (早盤/今日/滾球)
    /// </summary>
    public GameType GameType { get; set; }

    /// <summary>
    /// 比賽狀態
    /// </summary>
    public MatchStatus Status { get; set; }

    /// <summary>
    /// 聯賽資訊
    /// </summary>
    public LeagueInfo League { get; set; } = new();

    /// <summary>
    /// 主隊資訊
    /// </summary>
    public TeamInfo HomeTeam { get; set; } = new();

    /// <summary>
    /// 客隊資訊
    /// </summary>
    public TeamInfo AwayTeam { get; set; } = new();

    /// <summary>
    /// 預定開賽時間
    /// </summary>
    public DateTime ScheduleTime { get; set; }

    /// <summary>
    /// 來源更新時間
    /// </summary>
    public DateTime SourceUpdateTime { get; set; }

    /// <summary>
    /// 即時比賽時間 (如: 45', HT, 90+3')
    /// </summary>
    public string? LiveTime { get; set; }

    /// <summary>
    /// 比分資訊
    /// </summary>
    public ScoreInfo? Score { get; set; }
}

