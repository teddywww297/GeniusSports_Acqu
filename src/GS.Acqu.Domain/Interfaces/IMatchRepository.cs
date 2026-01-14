using GS.Acqu.Domain.Entities;

namespace GS.Acqu.Domain.Interfaces;

/// <summary>
/// 賽事儲存庫介面
/// </summary>
public interface IMatchRepository
{
    /// <summary>
    /// 根據來源賽事編號取得系統賽事編號
    /// </summary>
    Task<int?> GetEventIdAsync(string sourceMatchId);

    /// <summary>
    /// 建立賽事
    /// </summary>
    /// <param name="match">賽事資訊</param>
    /// <param name="catId">分類 ID (來自 tblLeague.CatID)</param>
    /// <param name="leagueId">聯賽 ID</param>
    /// <param name="homeTeamId">主隊 ID</param>
    /// <param name="awayTeamId">客隊 ID</param>
    /// <returns>新建立的賽事編號</returns>
    Task<int> CreateEventAsync(MatchInfo match, int catId, int leagueId, int homeTeamId, int awayTeamId);

    /// <summary>
    /// 更新賽事
    /// </summary>
    Task UpdateEventAsync(int eventId, MatchInfo match);

    /// <summary>
    /// 檢查賽事是否存在
    /// </summary>
    Task<bool> ExistsAsync(string sourceMatchId);

    /// <summary>
    /// 根據聯賽、主客隊、比賽時間查找現有賽事 (用於反向對點)
    /// </summary>
    /// <returns>找到的 EvtID，未找到則返回 null</returns>
    Task<int?> FindExistingEventAsync(int catId, int leagueId, int homeTeamId, int awayTeamId, DateTime scheduleTime);

    /// <summary>
    /// 更新賽事的 ProviderMatchId (反向對點)
    /// </summary>
    Task UpdateProviderMatchIdAsync(int eventId, string providerMatchId);

    /// <summary>
    /// 查詢有 ProviderMatchId 但沒有盤口的賽事
    /// </summary>
    /// <param name="limit">最大筆數</param>
    /// <returns>缺失盤口的賽事清單 (EvtId, ProviderMatchId, CatId)</returns>
    Task<IEnumerable<(int EvtId, string ProviderMatchId, int CatId)>> GetEventsWithoutMarketsAsync(int limit);
}

