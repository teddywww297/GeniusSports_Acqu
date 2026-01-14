namespace GS.Acqu.Domain.Entities;

/// <summary>
/// 隊伍資訊
/// </summary>
public class TeamInfo
{
    /// <summary>
    /// 來源隊伍編號
    /// </summary>
    public string SourceTeamId { get; set; } = string.Empty;

    /// <summary>
    /// 隊伍名稱
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

