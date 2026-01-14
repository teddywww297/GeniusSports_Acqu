using System.ComponentModel;

namespace GS.Acqu.Domain.Enums;

/// <summary>
/// 處理結果
/// </summary>
public enum ProcessResult
{
    [Description("成功")] Success = 0,
    [Description("賽事不存在")] MatchNotFound = 1,
    [Description("聯賽未對點")] LeagueNotMatched = 2,
    [Description("隊伍未對點")] TeamNotMatched = 3,
    [Description("資料無效")] InvalidData = 4,
    [Description("資料庫錯誤")] DatabaseError = 5,
    [Description("已跳過")] Skipped = 6
}

