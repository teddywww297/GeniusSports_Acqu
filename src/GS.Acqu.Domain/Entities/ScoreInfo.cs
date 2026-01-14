namespace GS.Acqu.Domain.Entities;

/// <summary>
/// 比分資訊
/// </summary>
public class ScoreInfo
{
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
}

