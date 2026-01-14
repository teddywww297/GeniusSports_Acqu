using System.ComponentModel;

namespace GS.Acqu.Domain.Enums;

/// <summary>
/// 訊息類型
/// </summary>
public enum MessageType
{
    [Description("賽事資訊")] MatchInfo = 1,
    [Description("盤口賠率")] MarketOdds = 2,
    [Description("比賽結果")] MatchResult = 3,
    [Description("比分更新")] ScoreUpdate = 4
}

