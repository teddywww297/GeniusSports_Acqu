namespace GS.Acqu.Infrastructure.Utilities;

/// <summary>
/// Hash 工具類
/// </summary>
public static class HashHelper
{
    /// <summary>
    /// 將字串轉換為唯一整數 ID (BKDR Hash)
    /// </summary>
    /// <param name="source">來源字串</param>
    /// <returns>唯一 ID (正整數)</returns>
    public static int ToAcquUniqId(string source)
    {
        return Math.Abs(BKDRHash(source));
    }

    /// <summary>
    /// 產生盤口唯一識別碼
    /// </summary>
    /// <param name="evtId">賽事 ID</param>
    /// <param name="marketCode">盤口代碼 (Zf=讓分, Ds=大小, De=獨贏, Sd=單雙)</param>
    /// <param name="rowId">盤口行號</param>
    /// <param name="halfType">全半場 (0=全場, 1=半場)</param>
    /// <param name="wagerTypeId">玩法類型 ID</param>
    /// <param name="pos">位置 (可選)</param>
    public static int GenerateMarketId(int evtId, string marketCode, string rowId, int halfType, int wagerTypeId, int? pos = null)
    {
        var key = pos.HasValue
            ? $"{evtId}_{marketCode}_{rowId}_{pos}_{halfType}_{wagerTypeId}"
            : $"{evtId}_{marketCode}_{rowId}_{halfType}_{wagerTypeId}";
            
        return ToAcquUniqId(key);
    }

    /// <summary>
    /// BKDR Hash 演算法
    /// 特點：速度快、分布均勻、碰撞率低
    /// </summary>
    private static int BKDRHash(string str)
    {
        const int seed = 131; // 質數種子 (31, 131, 1313, 13131...)
        int hash = 0;

        foreach (char c in str)
        {
            hash = (hash * seed) + c;
        }

        return hash;
    }
}

