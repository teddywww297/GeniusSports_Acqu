using System.ComponentModel;

namespace GS.Acqu.Domain.Enums;

/// <summary>
/// 全半場類型
/// </summary>
public enum HalfType
{
    [Description("全場")] FullTime = 0,
    [Description("上半場")] FirstHalf = 1,
    [Description("下半場")] SecondHalf = 2
}

