using GS.Acqu.Domain.Entities;
using GS.Acqu.Domain.Enums;

namespace GS.Acqu.Domain.Interfaces;

/// <summary>
/// 未對點賽事儲存介面
/// </summary>
public interface IUnmatchedEventStore
{
    /// <summary>
    /// 新增未對點賽事 (含詳細對點資訊)
    /// </summary>
    /// <param name="match">賽事資訊</param>
    /// <param name="reason">失敗原因</param>
    /// <param name="leagueMatch">聯賽對點結果 (null=未對點)</param>
    /// <param name="homeTeamMatch">主隊對點結果 (null=未對點)</param>
    /// <param name="awayTeamMatch">客隊對點結果 (null=未對點)</param>
    Task AddAsync(
        MatchInfo match,
        ProcessResult reason,
        MatchData? leagueMatch = null,
        MatchData? homeTeamMatch = null,
        MatchData? awayTeamMatch = null);

    /// <summary>
    /// 取得所有未對點賽事
    /// </summary>
    Task<IEnumerable<UnmatchedEvent>> GetAllAsync(SportType? sportType = null);

    /// <summary>
    /// 根據來源賽事編號取得未對點賽事
    /// </summary>
    Task<UnmatchedEvent?> GetBySourceMatchIdAsync(string sourceMatchId);

    /// <summary>
    /// 移除未對點賽事
    /// </summary>
    Task RemoveAsync(string sourceMatchId);

    /// <summary>
    /// 清除過期的未對點賽事
    /// </summary>
    Task ClearExpiredAsync(TimeSpan expiry);

    /// <summary>
    /// 取得未對點賽事數量
    /// </summary>
    Task<int> GetCountAsync(SportType? sportType = null);

    /// <summary>
    /// 取得統計資訊
    /// </summary>
    Task<UnmatchedStatistics> GetStatisticsAsync();
}

/// <summary>
/// 未對點統計資訊
/// </summary>
public class UnmatchedStatistics
{
    /// <summary>
    /// 總數量
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// 聯賽未對點數量
    /// </summary>
    public int LeagueNotMatchedCount { get; set; }

    /// <summary>
    /// 球隊未對點數量
    /// </summary>
    public int TeamNotMatchedCount { get; set; }

    /// <summary>
    /// 部分對點數量
    /// </summary>
    public int PartialMatchCount { get; set; }

    /// <summary>
    /// 按球種統計
    /// </summary>
    public Dictionary<SportType, int> BySportType { get; set; } = new();

    /// <summary>
    /// 按原因統計
    /// </summary>
    public Dictionary<ProcessResult, int> ByReason { get; set; } = new();
}

