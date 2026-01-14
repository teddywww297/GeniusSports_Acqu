using System.ComponentModel;

namespace GS.Acqu.Domain.Enums;

/// <summary>
/// 賽事類型
/// </summary>
public enum GameType
{
    [Description("早盤")] Early = 0,
    [Description("今日")] Today = 1,
    [Description("滾球")] Live = 2
}

