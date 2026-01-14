using System.ComponentModel;

namespace GS.Acqu.Domain.Enums;

/// <summary>
/// 盤口類型
/// </summary>
public enum MarketType
{
    [Description("讓球")] Handicap = 1,
    [Description("大小")] OverUnder = 2,
    [Description("獨贏")] MoneyLine = 3,
    [Description("波膽")] CorrectScore = 4,
    [Description("半全場")] HalfFullTime = 5,
    [Description("單雙")] OddEven = 6,
    [Description("總得分")] TotalGoals = 7
}

