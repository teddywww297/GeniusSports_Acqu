namespace GS.Acqu.Worker.Options;

/// <summary>
/// Genius Sports gRPC 連線設定
/// </summary>
public class GeniusSportsOptions
{
    public const string SectionName = "GeniusSports";

    /// <summary>
    /// gRPC 服務端點
    /// </summary>
    public string Endpoint { get; set; } = "https://localhost:5001";

    /// <summary>
    /// 訂閱的球種 ID 清單
    /// </summary>
    public int[] SportIds { get; set; } = [1]; // 預設足球

    /// <summary>
    /// 重連間隔 (秒)
    /// </summary>
    public int ReconnectIntervalSeconds { get; set; } = 5;

    /// <summary>
    /// 是否啟用 SSL
    /// </summary>
    public bool UseSsl { get; set; } = true;

    /// <summary>
    /// 語系 (用於名稱)
    /// </summary>
    public string Language { get; set; } = "zh-TW";

    /// <summary>
    /// 補建檢查間隔 (分鐘)
    /// </summary>
    public int RepairIntervalMinutes { get; set; } = 10;

    /// <summary>
    /// 每次補建最大數量
    /// </summary>
    public int RepairBatchSize { get; set; } = 50;

    /// <summary>
    /// 是否在對點完成後自動建立賽事 (預設關閉)
    /// </summary>
    public bool AutoCreateEvent { get; set; } = false;
}


