using System.Data;
using Dapper;
using GS.Acqu.Domain.Entities;
using GS.Acqu.Domain.Enums;
using GS.Acqu.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace GS.Acqu.Infrastructure.Repositories;

/// <summary>
/// 盤口儲存庫實作 (用於直接 DB 操作)
/// </summary>
public class MarketRepository : IMarketRepository
{
    private readonly IDbConnection _db;
    private readonly ILogger<MarketRepository> _logger;

    public MarketRepository(IDbConnection db, ILogger<MarketRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<int?> GetMarketIdAsync(int eventId, MarketType marketType, HalfType halfType)
    {
        const string sql = @"
            SELECT AcquUniqID 
            FROM tblGameDataDetail 
            WHERE EvtID = @EventId 
              AND WagerTypeID = @MarketType 
              AND HalfType = @HalfType
              AND Status > -97";

        return await _db.QueryFirstOrDefaultAsync<int?>(sql, new
        {
            EventId = eventId,
            MarketType = (int)marketType,
            HalfType = (int)halfType
        });
    }

    /// <inheritdoc />
    public async Task UpsertMarketAsync(int eventId, MarketInfo market)
    {
        // 此方法保留給非高頻場景使用
        // 高頻場景應使用 MarketOddsHandler + 快取 + 隊列
        const string sql = @"
            MERGE INTO tblGameDataDetail AS target
            USING (SELECT @EventId AS EvtID, @MarketType AS WagerTypeID, @HalfType AS HalfType) AS source
            ON target.EvtID = source.EvtID 
               AND target.WagerTypeID = source.WagerTypeID 
               AND target.HalfType = source.HalfType
            WHEN MATCHED THEN
                UPDATE SET 
                    Line = @Line,
                    Odds1 = @Odds1,
                    Odds2 = @Odds2,
                    Odds3 = @Odds3,
                    IsSuspended = @IsSuspended,
                    SourceUpdateTime = @SourceUpdateTime,
                    UpdateTime = GETDATE()
            WHEN NOT MATCHED THEN
                INSERT (EvtID, WagerTypeID, HalfType, Line, Odds1, Odds2, Odds3, IsSuspended, SourceUpdateTime, CreateTime, UpdateTime)
                VALUES (@EventId, @MarketType, @HalfType, @Line, @Odds1, @Odds2, @Odds3, @IsSuspended, @SourceUpdateTime, GETDATE(), GETDATE());";

        var parameters = new
        {
            EventId = eventId,
            MarketType = (int)market.MarketType,
            HalfType = (int)market.HalfType,
            Line = market.Line,
            Odds1 = market.Odds1,
            Odds2 = market.Odds2,
            Odds3 = market.Odds3,
            IsSuspended = market.IsSuspended,
            SourceUpdateTime = market.SourceUpdateTime
        };

        await _db.ExecuteAsync(sql, parameters);
    }

    /// <inheritdoc />
    public async Task BulkUpsertMarketsAsync(int eventId, IEnumerable<MarketInfo> markets)
    {
        // 此方法保留給非高頻場景使用
        // 高頻場景應使用 MarketOddsHandler + 快取 + 隊列
        foreach (var market in markets)
        {
            await UpsertMarketAsync(eventId, market);
        }
    }

    /// <summary>
    /// 同步新增盤口 (用於新盤口首次寫入)
    /// </summary>
    public async Task<bool> InsertMarketDetailAsync(MarketDetailCache market)
    {
        // 注意：GameID 設為 0，若 DB 有 NOT NULL 約束可能需要調整
        // 根據文件，GameID 來自 tblGameSeq，但目前系統不使用此欄位
        const string sql = @"
            INSERT INTO tblGameDataDetail (
                AcquUniqID, EvtID, GameID,
                HomeHdp, AwayHdp, HdpPos, HomeHdpOdds, AwayHdpOdds,
                OULine, OverOdds, UnderOdds,
                HomeOdds, DrewOdds, AwayOdds,
                HalfType, WagerGrpID, WagerTypeID, Status, OptPer,
                AcqFSite, ZeroH, LastUpdate, LastChange
            )
            VALUES (
                @AcquUniqId, @EvtId, 0,
                ISNULL(@HomeHdp, '-'), ISNULL(@AwayHdp, '-'), @HdpPos, @HomeHdpOdds, @AwayHdpOdds,
                ISNULL(@OULine, '-'), @OverOdds, @UnderOdds,
                @HomeOdds, @DrawOdds, @AwayOdds,
                @HalfType, @WagerGrpId, @WagerTypeId, @Status, @OptPer,
                'GS', 0, GETDATE(), GETDATE()
            )";

        try
        {
            await _db.ExecuteAsync(sql, market);
            _logger.LogDebug("新增盤口: AcquUniqID={AcquUniqId}, EvtID={EvtId}", 
                market.AcquUniqId, market.EvtId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "新增盤口失敗: AcquUniqID={AcquUniqId}", market.AcquUniqId);
            return false;
        }
    }

    /// <summary>
    /// 關閉盤口 (拉盤)
    /// </summary>
    public string GetCloseMarketSql(int acquUniqId)
    {
        return $"UPDATE tblGameDataDetail SET Status=-1, LastUpdate=GETDATE() WHERE AcquUniqID={acquUniqId} AND Status=1;";
    }
}
