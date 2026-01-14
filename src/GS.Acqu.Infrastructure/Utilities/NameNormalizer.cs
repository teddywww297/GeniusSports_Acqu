using System.Text.RegularExpressions;

namespace GS.Acqu.Infrastructure.Utilities;

/// <summary>
/// 名稱正規化工具類
/// </summary>
public static partial class NameNormalizer
{
    /// <summary>
    /// 正規化名稱（用於對點比對）
    /// </summary>
    /// <remarks>
    /// 處理流程：
    /// 1. 移除控制字符（Unicode Cc 類別）
    /// 2. 移除所有空白字符
    /// 3. 轉換為小寫
    /// </remarks>
    public static string Normalize(string? name)
    {
        if (string.IsNullOrEmpty(name))
            return string.Empty;

        try
        {
            // 移除控制字符和空白
            var cleaned = CleanInputRegex().Replace(name, "");
            return cleaned.ToLowerInvariant();
        }
        catch (RegexMatchTimeoutException)
        {
            // 正則超時時使用簡單處理
            return name.ToLowerInvariant().Replace(" ", "");
        }
    }

    /// <summary>
    /// 選擇最佳名稱（優先順序：主名稱 > 中文名 > 英文名）
    /// </summary>
    public static string SelectBestName(string? name, string? nameCn, string? nameEn)
    {
        if (!string.IsNullOrWhiteSpace(name))
            return name;
        if (!string.IsNullOrWhiteSpace(nameCn))
            return nameCn;
        if (!string.IsNullOrWhiteSpace(nameEn))
            return nameEn;
        return string.Empty;
    }

    /// <summary>
    /// 正則表達式：匹配控制字符和空白
    /// </summary>
    [GeneratedRegex(@"[\p{C}\s]", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex CleanInputRegex();
}

