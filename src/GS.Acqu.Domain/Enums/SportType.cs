using System.ComponentModel;

namespace GS.Acqu.Domain.Enums;

/// <summary>
/// 球種類型 - 對應 DB tblLeague/tblTeam 的 CatID
/// </summary>
public enum SportType
{
    [Description("足球")] Soccer = 1,              // sc
    [Description("籃球")] Basketball = 3,          // bk
    [Description("美棒")] MLB = 4,                 // mlb
    [Description("美式足球")] Football = 5,        // fb
    [Description("台棒")] CPBL = 11,               // cpbl
    [Description("日棒")] NPB = 12,                // npb
    [Description("其它棒球")] OtherBaseball = 13,  // other
    [Description("韓棒")] KBO = 14,                // kbo
    [Description("女籃")] WNBA = 16,
    [Description("世界盃足球")] WorldCup = 31,     // wsc
    [Description("網球")] Tennis = 55,             // tn
    [Description("冰球")] Hockey = 82,             // hc
    [Description("電競")] ESports = 85             // es
}

