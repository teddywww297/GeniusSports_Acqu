namespace GS.Acqu.Domain.Entities;

/// <summary>
/// 聯賽資訊
/// </summary>
public class LeagueInfo
{
    /// <summary>
    /// 來源聯賽編號
    /// </summary>
    public string SourceLeagueId { get; set; } = string.Empty;

    /// <summary>
    /// 聯賽名稱
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 英文名稱
    /// </summary>
    public string? NameEn { get; set; }

    /// <summary>
    /// 中文名稱
    /// </summary>
    public string? NameCn { get; set; }
}

