using System.Data;
using Dapper;
using GS.Acqu.Domain.Entities;
using GS.Acqu.Domain.Enums;
using GS.Acqu.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace GS.Acqu.Infrastructure.Repositories;

/// <summary>
/// 賽事儲存庫實作
/// </summary>
/// <remarks>
/// 欄位對應說明 (程式 → DB):
/// - SourceMatchId → ProviderMatchId
/// - HomeTeamId → HomeID
/// - AwayTeamId → AwayID  
/// - Status → EvtStatus (1,2→1 開盤, 其他→0)
/// - LiveTime → TimeAct
/// - UpdateTime → LastTime
/// - SourceUpdateTime → SourceTime
/// - FH = 中場時記錄 "{HomeScore}:{AwayScore}"
/// </remarks>
public class MatchRepository : IMatchRepository
{
    private readonly IDbConnection _db;
    private readonly ILogger<MatchRepository> _logger;

    public MatchRepository(IDbConnection db, ILogger<MatchRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<int?> GetEventIdAsync(string sourceMatchId)
    {
        // 同時檢查 ProviderMatchId 和 SAcquEvtID，避免重複建立
        const string sql = @"
            SELECT TOP 1 EvtID 
            FROM tblGameData 
            WHERE ProviderMatchId = @SourceMatchId 
               OR SAcquEvtID = @SourceMatchId";

        return await _db.QueryFirstOrDefaultAsync<int?>(sql, new { SourceMatchId = sourceMatchId });
    }

    /// <inheritdoc />
    public async Task<int> CreateEventAsync(MatchInfo match, int catId, int leagueId, int homeTeamId, int awayTeamId)
    {
        // EvtID 不是 IDENTITY，需要手動取得下一個 ID
        // 同時設定所有 NOT NULL 欄位的預設值
        // SAcquEvtID 欄位存放 GS SourceMatchId，有唯一索引避免重複建立
        const string sql = @"
            DECLARE @NewEvtID INT;
            SELECT @NewEvtID = ISNULL(MAX(EvtID), 0) + 1 FROM tblGameData;
            
            INSERT INTO tblGameData (
                EvtID, ProviderMatchId, SAcquEvtID, CatID, LeagueID, HomeID, AwayID,
                ScheduleTime, GameType, EvtStatus, TimeAct,
                HomeScore, AwayScore, FH,
                SourceTime, CreateTime, LastTime,
                MoreFlg, OddsType, EvtType
            )
            VALUES (
                @NewEvtID, @ProviderMatchId, @SAcquEvtID, @CatId, @LeagueId, @HomeId, @AwayId,
                @ScheduleTime, @GameType, @EvtStatus, @TimeAct,
                @HomeScore, @AwayScore, @FH,
                @SourceTime, GETDATE(), GETDATE(),
                0, 0, 0
            );
            
            SELECT @NewEvtID;";

        var homeScore = match.Score?.HomeScore ?? 0;
        var awayScore = match.Score?.AwayScore ?? 0;
        
        var parameters = new
        {
            ProviderMatchId = match.SourceMatchId,
            SAcquEvtID = match.SourceMatchId,  // SAcquEvtID 使用相同的 SourceMatchId
            CatId = catId,  // 使用傳入的 CatId (來自 tblLeague.CatID)，而非 SportType
            LeagueId = leagueId,
            HomeId = homeTeamId,
            AwayId = awayTeamId,
            ScheduleTime = match.ScheduleTime,
            GameType = (int)match.GameType,
            EvtStatus = ConvertToEvtStatus(match.Status),
            TimeAct = match.LiveTime ?? "",
            HomeScore = homeScore,
            AwayScore = awayScore,
            FH = CalculateFH(match.LiveTime, homeScore, awayScore) ?? "",
            SourceTime = match.SourceUpdateTime
        };

        var eventId = await _db.QuerySingleAsync<int>(sql, parameters);
        _logger.LogDebug("建立賽事: EvtID={EventId}, ProviderMatchId={SourceMatchId}", eventId, match.SourceMatchId);
        
        return eventId;
    }

    /// <inheritdoc />
    public async Task UpdateEventAsync(int eventId, MatchInfo match)
    {
        const string sql = @"
            UPDATE tblGameData SET
                GameType = @GameType,
                EvtStatus = @EvtStatus,
                TimeAct = @TimeAct,
                HomeScore = @HomeScore,
                AwayScore = @AwayScore,
                FH = CASE WHEN @FH IS NOT NULL THEN @FH ELSE FH END,
                SourceTime = @SourceTime,
                LastTime = GETDATE()
            WHERE EvtID = @EventId";

        var homeScore = match.Score?.HomeScore ?? 0;
        var awayScore = match.Score?.AwayScore ?? 0;

        var parameters = new
        {
            EventId = eventId,
            GameType = (int)match.GameType,
            EvtStatus = ConvertToEvtStatus(match.Status),
            TimeAct = match.LiveTime,
            HomeScore = homeScore,
            AwayScore = awayScore,
            FH = CalculateFH(match.LiveTime, homeScore, awayScore),
            SourceTime = match.SourceUpdateTime
        };

        await _db.ExecuteAsync(sql, parameters);
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(string sourceMatchId)
    {
        const string sql = @"
            SELECT COUNT(1) 
            FROM tblGameData 
            WHERE ProviderMatchId = @SourceMatchId";

        var count = await _db.QueryFirstAsync<int>(sql, new { SourceMatchId = sourceMatchId });
        return count > 0;
    }

    /// <inheritdoc />
    public async Task<int?> FindExistingEventAsync(int catId, int leagueId, int homeTeamId, int awayTeamId, DateTime scheduleTime)
    {
        // 根據聯賽、主客隊、比賽時間查找現有賽事
        // 早盤：±7天內匹配 (時間差 > 2小時視為早盤)
        // 走地：±2小時
        // 排除已有 ProviderMatchId 的賽事 (支援 varchar 和 int 的 0)
        const string sql = @"
            SELECT TOP 1 EvtID 
            FROM tblGameData 
            WHERE CatID = @CatId
              AND LeagueID = @LeagueId
              AND HomeID = @HomeTeamId
              AND AwayID = @AwayTeamId
              AND (
                  -- 早盤：比賽時間在未來超過2小時，改用±7天匹配
                  (@IsEarly = 1 AND ScheduleTime BETWEEN DATEADD(DAY, -7, @ScheduleTime) AND DATEADD(DAY, 7, @ScheduleTime))
                  OR
                  -- 走地/即將開始：±2小時
                  (@IsEarly = 0 AND ScheduleTime BETWEEN DATEADD(HOUR, -2, @ScheduleTime) AND DATEADD(HOUR, 2, @ScheduleTime))
              )
              AND (ProviderMatchId IS NULL OR CAST(ProviderMatchId AS VARCHAR(50)) = '' OR CAST(ProviderMatchId AS VARCHAR(50)) = '0')
            ORDER BY ABS(DATEDIFF(MINUTE, ScheduleTime, @ScheduleTime))";

        // 判斷是否為早盤：比賽時間在未來超過2小時
        var isEarly = scheduleTime > DateTime.Now.AddHours(2);

        _logger.LogDebug(
            "反向對點查詢: CatID={CatId}, LeagueID={LeagueId}, HomeID={HomeId}, AwayID={AwayId}, ScheduleTime={ScheduleTime}, IsEarly={IsEarly}",
            catId, leagueId, homeTeamId, awayTeamId, scheduleTime, isEarly);

        var result = await _db.QueryFirstOrDefaultAsync<int?>(sql, new
        {
            CatId = catId,
            LeagueId = leagueId,
            HomeTeamId = homeTeamId,
            AwayTeamId = awayTeamId,
            ScheduleTime = scheduleTime,
            IsEarly = isEarly ? 1 : 0
        });

        if (result.HasValue)
        {
            _logger.LogDebug("反向對點找到賽事: EvtID={EvtId}", result.Value);
        }
        else
        {
            _logger.LogDebug("反向對點未找到符合條件的賽事");
        }

        return result;
    }

    /// <inheritdoc />
    public async Task UpdateProviderMatchIdAsync(int eventId, string providerMatchId)
    {
        // 支援 varchar 和 int 的 0
        const string sql = @"
            UPDATE tblGameData SET
                ProviderMatchId = @ProviderMatchId,
                LastTime = GETDATE()
            WHERE EvtID = @EventId
              AND (ProviderMatchId IS NULL OR CAST(ProviderMatchId AS VARCHAR(50)) = '' OR CAST(ProviderMatchId AS VARCHAR(50)) = '0')";

        var affected = await _db.ExecuteAsync(sql, new { EventId = eventId, ProviderMatchId = providerMatchId });
        
        if (affected > 0)
        {
            _logger.LogInformation("反向對點成功: EvtID={EventId}, ProviderMatchId={ProviderMatchId}", eventId, providerMatchId);
        }
        else
        {
            _logger.LogWarning("反向對點更新失敗 (可能已有 ProviderMatchId): EvtID={EventId}", eventId);
        }
    }

    /// <summary>
    /// 將訊號源 Status 轉換為 EvtStatus
    /// </summary>
    /// <remarks>
    /// Status Live=1, Ended=4 → EvtStatus=1 (開盤中)
    /// 其他狀態 → EvtStatus=0 (由控端處理)
    /// </remarks>
    private static int ConvertToEvtStatus(MatchStatus status)
    {
        return status == MatchStatus.Live || status == MatchStatus.Ended ? 1 : 0;
    }

    /// <summary>
    /// 計算半場比分 FH 欄位
    /// </summary>
    /// <remarks>
    /// 當 TimeAct 為中場時 ("中場", "HT", "ht")，記錄當下比分
    /// 格式: "{主隊分}:{客隊分}"
    /// </remarks>
    private static string? CalculateFH(string? timeAct, int homeScore, int awayScore)
    {
        if (string.IsNullOrEmpty(timeAct))
            return null;

        var normalizedTime = timeAct.Trim().ToUpperInvariant();
        if (normalizedTime == "中場" || normalizedTime == "HT")
        {
            return $"{homeScore}:{awayScore}";
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<(int EvtId, string ProviderMatchId, int CatId)>> GetEventsWithoutMarketsAsync(int limit)
    {
        const string sql = @"
            SELECT TOP (@Limit) g.EvtID, g.ProviderMatchId, g.CatID
            FROM tblGameData g WITH(NOLOCK)
            LEFT JOIN tblGameDataDetail d WITH(NOLOCK) ON g.EvtID = d.EvtID
            WHERE g.ProviderMatchId IS NOT NULL 
              AND g.ProviderMatchId <> ''
              AND g.ProviderMatchId <> '0'
              AND g.ScheduleTime > GETDATE()
              AND d.EvtID IS NULL
            ORDER BY g.ScheduleTime";

        var results = await _db.QueryAsync<(int EvtId, string ProviderMatchId, int CatId)>(
            sql, new { Limit = limit });
        return results;
    }
}

