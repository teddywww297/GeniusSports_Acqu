using GS.Acqu.Domain.Entities;
using GS.Acqu.Domain.Enums;

namespace GS.Acqu.Worker.Clients;

/// <summary>
/// Genius Sports gRPC Client 介面
/// </summary>
public interface IGeniusSportsClient
{
    /// <summary>
    /// 取得球種清單
    /// </summary>
    Task<IEnumerable<(int Id, string Name)>> GetSportsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得聯賽清單
    /// </summary>
    Task<IEnumerable<LeagueInfo>> GetCompetitionsAsync(int sportId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得賽事清單
    /// </summary>
    Task<IEnumerable<MatchInfo>> GetMatchesAsync(int sportId, DateTime startTime, DateTime endTime, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得早盤盤口
    /// </summary>
    Task<IEnumerable<(int MatchId, IEnumerable<MarketInfo> Markets)>> GetPreMatchMarketsAsync(
        int sportId, 
        string startDate, 
        string endDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得走地盤口
    /// </summary>
    Task<IEnumerable<(int MatchId, IEnumerable<MarketInfo> Markets)>> GetInPlayMarketsAsync(
        int sportId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 訂閱賽事變更
    /// </summary>
    IAsyncEnumerable<MatchInfo> WatchMatchesAsync(int sportId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 訂閱早盤盤口變更
    /// </summary>
    IAsyncEnumerable<(int MatchId, IEnumerable<MarketInfo> Markets)> WatchPreMatchMarketsAsync(
        int sportId, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 訂閱走地盤口變更
    /// </summary>
    IAsyncEnumerable<(int MatchId, IEnumerable<MarketInfo> Markets)> WatchInPlayMarketsAsync(
        int sportId, 
        CancellationToken cancellationToken = default);
}


