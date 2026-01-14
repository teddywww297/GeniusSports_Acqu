using System.ComponentModel;

namespace GS.Acqu.Domain.Enums;

/// <summary>
/// 比賽狀態
/// </summary>
public enum MatchStatus
{
    [Description("未開始")] NotStarted = 0,
    [Description("進行中")] Live = 1,
    [Description("暫停")] Paused = 2,
    [Description("中場休息")] HalfTime = 3,
    [Description("已結束")] Ended = 4,
    [Description("取消")] Cancelled = 5,
    [Description("延期")] Postponed = 6,
    [Description("中斷")] Interrupted = 7
}

