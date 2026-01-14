namespace GS.Acqu.Infrastructure.Data;

/// <summary>
/// 資料庫設定選項
/// </summary>
public class DatabaseOptions
{
    /// <summary>
    /// 設定區段名稱
    /// </summary>
    public const string SectionName = "Database";

    /// <summary>
    /// 賽事資料連線字串 (BigballGame)
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// 主資料連線字串 (BigballMain) - 隊伍/聯賽等基礎資料
    /// </summary>
    public string MainConnectionString { get; set; } = string.Empty;
}

