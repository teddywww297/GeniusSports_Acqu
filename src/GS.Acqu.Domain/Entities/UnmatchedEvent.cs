using GS.Acqu.Domain.Enums;

namespace GS.Acqu.Domain.Entities;

/// <summary>
/// 未對點賽事
/// </summary>
public class UnmatchedEvent
{
    /// <summary>
    /// 來源賽事編號 (Key)
    /// </summary>
    public string SourceMatchId { get; set; } = string.Empty;

    /// <summary>
    /// 來源站點標識
    /// </summary>
    public string Source { get; set; } = "GS";

    /// <summary>
    /// 賽事資訊
    /// </summary>
    public MatchInfo Match { get; set; } = new();

    // ===== 對點結果詳情 =====

    /// <summary>
    /// 聯賽對點結果 (null=未對點)
    /// </summary>
    public MatchData? LeagueMatchResult { get; set; }

    /// <summary>
    /// 主隊對點結果 (null=未對點)
    /// </summary>
    public MatchData? HomeTeamMatchResult { get; set; }

    /// <summary>
    /// 客隊對點結果 (null=未對點)
    /// </summary>
    public MatchData? AwayTeamMatchResult { get; set; }

    /// <summary>
    /// 主要失敗原因
    /// </summary>
    public ProcessResult Reason { get; set; }

    // ===== 狀態追蹤 =====

    /// <summary>
    /// 建立時間
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// 最後更新時間
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// 重試次數
    /// </summary>
    public int RetryCount { get; set; } = 0;

    // ===== 輔助屬性 =====

    /// <summary>
    /// 聯賽對點 ID (0=未對點)
    /// </summary>
    public int LeagueId => LeagueMatchResult?.Id ?? 0;

    /// <summary>
    /// 主隊對點 ID (0=未對點)
    /// </summary>
    public int HomeId => HomeTeamMatchResult?.Id ?? 0;

    /// <summary>
    /// 客隊對點 ID (0=未對點)
    /// </summary>
    public int AwayId => AwayTeamMatchResult?.Id ?? 0;

    /// <summary>
    /// 是否為部分對點
    /// </summary>
    public bool IsPartialMatch =>
        LeagueMatchResult != null ||
        HomeTeamMatchResult != null ||
        AwayTeamMatchResult != null;

    /// <summary>
    /// 對點狀態摘要
    /// </summary>
    public string MatchStatus =>
        $"League:{(LeagueMatchResult != null ? "✓" : "✗")} " +
        $"Home:{(HomeTeamMatchResult != null ? "✓" : "✗")} " +
        $"Away:{(AwayTeamMatchResult != null ? "✓" : "✗")}";
}

