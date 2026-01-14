using GS.Acqu.Domain.Enums;

namespace GS.Acqu.Domain.Entities;

/// <summary>
/// 盤口資訊
/// </summary>
public class MarketInfo
{
    /// <summary>
    /// 來源賽事編號
    /// </summary>
    public string SourceMatchId { get; set; } = string.Empty;

    /// <summary>
    /// 盤口類型
    /// </summary>
    public MarketType MarketType { get; set; }

    /// <summary>
    /// 全半場類型
    /// </summary>
    public HalfType HalfType { get; set; }

    /// <summary>
    /// 盤口線 (讓球值/大小線)
    /// </summary>
    public decimal? Line { get; set; }

    /// <summary>
    /// 賠率1 (主隊/大/勝)
    /// </summary>
    public decimal Odds1 { get; set; }

    /// <summary>
    /// 賠率2 (客隊/小/負)
    /// </summary>
    public decimal Odds2 { get; set; }

    /// <summary>
    /// 賠率3 (和局，獨贏用)
    /// </summary>
    public decimal? Odds3 { get; set; }

    /// <summary>
    /// 是否暫停
    /// </summary>
    public bool IsSuspended { get; set; }

    /// <summary>
    /// 來源更新時間
    /// </summary>
    public DateTime SourceUpdateTime { get; set; }
}

