using GS.Acqu.Domain.Entities;
using GS.Acqu.Domain.Enums;

namespace GS.Acqu.Domain.Interfaces;

/// <summary>
/// 盤口儲存庫介面
/// </summary>
public interface IMarketRepository
{
    /// <summary>
    /// 取得盤口編號
    /// </summary>
    Task<int?> GetMarketIdAsync(int eventId, MarketType marketType, HalfType halfType);

    /// <summary>
    /// 新增或更新盤口
    /// </summary>
    Task UpsertMarketAsync(int eventId, MarketInfo market);

    /// <summary>
    /// 批次新增或更新盤口
    /// </summary>
    Task BulkUpsertMarketsAsync(int eventId, IEnumerable<MarketInfo> markets);

    /// <summary>
    /// 新增盤口明細 (用於新盤口首次寫入)
    /// </summary>
    Task<bool> InsertMarketDetailAsync(MarketDetailCache market);
}

