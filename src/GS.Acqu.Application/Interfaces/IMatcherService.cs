using GS.Acqu.Domain.Entities;
using GS.Acqu.Domain.Enums;

namespace GS.Acqu.Application.Interfaces;

/// <summary>
/// 對點服務介面
/// </summary>
public interface IMatcherService
{
    /// <summary>
    /// 對點聯賽
    /// </summary>
    /// <param name="sportType">球種類型</param>
    /// <param name="league">聯賽資訊</param>
    /// <returns>對點成功回傳 MatchData，否則回傳 null</returns>
    Task<MatchData?> MatchLeagueAsync(SportType sportType, LeagueInfo league);

    /// <summary>
    /// 對點隊伍
    /// </summary>
    /// <param name="sportType">球種類型</param>
    /// <param name="team">隊伍資訊</param>
    /// <returns>對點成功回傳 MatchData，否則回傳 null</returns>
    Task<MatchData?> MatchTeamAsync(SportType sportType, TeamInfo team);

    /// <summary>
    /// 重新載入對點快取
    /// </summary>
    Task RefreshCacheAsync();

    /// <summary>
    /// 自動新增聯賽（對點失敗時使用）
    /// </summary>
    /// <param name="sportType">球種類型</param>
    /// <param name="league">聯賽資訊</param>
    /// <returns>新增成功回傳 MatchData，否則回傳 null</returns>
    Task<MatchData?> AutoInsertLeagueAsync(SportType sportType, LeagueInfo league);

    /// <summary>
    /// 自動新增隊伍（對點失敗時使用）
    /// </summary>
    /// <param name="sportType">球種類型</param>
    /// <param name="team">隊伍資訊</param>
    /// <returns>新增成功回傳 MatchData，否則回傳 null</returns>
    Task<MatchData?> AutoInsertTeamAsync(SportType sportType, TeamInfo team);

    /// <summary>
    /// 快取統計資訊
    /// </summary>
    (int LeagueCount, int TeamCount) GetCacheStats();
}
