namespace GS.Acqu.Domain.Entities;

/// <summary>
/// 盤口詳情快取實體 (對應 tblGameDataDetail)
/// 欄位對應現有 GDetailType TVP
/// </summary>
public class MarketDetailCache
{
    /// <summary>
    /// 唯一識別碼 (BKDR Hash)
    /// </summary>
    public int AcquUniqId { get; set; }

    /// <summary>
    /// 賽事 ID
    /// </summary>
    public int EvtId { get; set; }

    /// <summary>
    /// 遊戲流水號 (來自 tblGameSeq)
    /// </summary>
    public int GameId { get; set; }

    /// <summary>
    /// 主隊讓分
    /// </summary>
    public string? HomeHdp { get; set; }

    /// <summary>
    /// 客隊讓分
    /// </summary>
    public string? AwayHdp { get; set; }

    /// <summary>
    /// 讓分位置 (1=主讓, 2=客讓)
    /// </summary>
    public byte HdpPos { get; set; }

    /// <summary>
    /// 主隊讓分賠率
    /// </summary>
    public decimal HomeHdpOdds { get; set; }

    /// <summary>
    /// 客隊讓分賠率
    /// </summary>
    public decimal AwayHdpOdds { get; set; }

    /// <summary>
    /// 大小線
    /// </summary>
    public string? OULine { get; set; }

    /// <summary>
    /// 大賠率
    /// </summary>
    public decimal OverOdds { get; set; }

    /// <summary>
    /// 小賠率
    /// </summary>
    public decimal UnderOdds { get; set; }

    /// <summary>
    /// 主勝賠率
    /// </summary>
    public decimal HomeOdds { get; set; }

    /// <summary>
    /// 和局賠率
    /// </summary>
    public decimal DrawOdds { get; set; }

    /// <summary>
    /// 客勝賠率
    /// </summary>
    public decimal AwayOdds { get; set; }

    /// <summary>
    /// 全半場 (0=全場, 1=上半, 2=下半)
    /// </summary>
    public byte HalfType { get; set; }

    /// <summary>
    /// 玩法群組 ID
    /// </summary>
    public short WagerGrpId { get; set; }

    /// <summary>
    /// 玩法類型 ID
    /// </summary>
    public short WagerTypeId { get; set; }

    /// <summary>
    /// 狀態 (1=開盤, -1=關盤)
    /// </summary>
    public short Status { get; set; } = 1;

    /// <summary>
    /// 操作人員
    /// </summary>
    public short OptPer { get; set; }

    /// <summary>
    /// 位置 (GDetailType 欄位)
    /// </summary>
    public byte Position { get; set; }

    /// <summary>
    /// 變更標記 (0=無變更, 1=有變更)
    /// </summary>
    public byte Change { get; set; }

    /// <summary>
    /// 計算賠率1
    /// </summary>
    public decimal CalOdds1 { get; set; }

    /// <summary>
    /// 計算賠率2
    /// </summary>
    public decimal CalOdds2 { get; set; }

    /// <summary>
    /// 計算賠率3
    /// </summary>
    public decimal CalOdds3 { get; set; }

    /// <summary>
    /// 抽水
    /// </summary>
    public decimal Margin { get; set; }

    /// <summary>
    /// 抽水代碼
    /// </summary>
    public string? MarginCode { get; set; }

    /// <summary>
    /// 最後更新時間
    /// </summary>
    public DateTime LastUpdate { get; set; }

    /// <summary>
    /// 來源更新時間
    /// </summary>
    public DateTime SourceTime { get; set; }

    /// <summary>
    /// 資料來源站點
    /// </summary>
    public string? AcqFSite { get; set; } = "GS";

    /// <summary>
    /// 零盤標記
    /// </summary>
    public int ZeroH { get; set; }
}
