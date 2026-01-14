namespace GS.Acqu.Domain.Entities;

/// <summary>
/// 對點結果資料
/// </summary>
public class MatchData
{
    /// <summary>
    /// 系統內部 ID (LeagueID/TeamID)
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 標準化名稱
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 模組 ID（用於賠率調整分類）
    /// </summary>
    public int ModId { get; set; }

    /// <summary>
    /// 分類 ID (對應 tblLeague.CatID，用於反向對點)
    /// </summary>
    public int CatId { get; set; }
}

