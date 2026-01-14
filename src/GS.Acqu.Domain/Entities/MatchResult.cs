using GS.Acqu.Domain.Enums;

namespace GS.Acqu.Domain.Entities;

/// <summary>
/// 比賽結果
/// </summary>
public class MatchResult
{
    /// <summary>
    /// 來源賽事編號
    /// </summary>
    public string SourceMatchId { get; set; } = string.Empty;

    /// <summary>
    /// 最終狀態
    /// </summary>
    public MatchStatus FinalStatus { get; set; }

    /// <summary>
    /// 主隊得分
    /// </summary>
    public int HomeScore { get; set; }

    /// <summary>
    /// 客隊得分
    /// </summary>
    public int AwayScore { get; set; }

    /// <summary>
    /// 主隊半場得分
    /// </summary>
    public int? HomeHalfScore { get; set; }

    /// <summary>
    /// 客隊半場得分
    /// </summary>
    public int? AwayHalfScore { get; set; }

    /// <summary>
    /// 各節比分 (JSON 格式)
    /// </summary>
    public string? PeriodScores { get; set; }

    /// <summary>
    /// 結束時間
    /// </summary>
    public DateTime EndTime { get; set; }
}

