using System.Data;
using Dapper;
using GS.Acqu.Domain.Entities;
using GS.Acqu.Domain.Enums;
using GS.Acqu.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace GS.Acqu.Infrastructure.Repositories;

/// <summary>
/// 結果儲存庫實作
/// </summary>
public class ResultRepository : IResultRepository
{
    private readonly IDbConnection _db;
    private readonly ILogger<ResultRepository> _logger;

    public ResultRepository(IDbConnection db, ILogger<ResultRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task UpdateResultAsync(int eventId, MatchResult result)
    {
        const string sql = @"
            UPDATE tblGameData SET
                Status = @Status,
                HomeScore = @HomeScore,
                AwayScore = @AwayScore,
                HomeHalfScore = @HomeHalfScore,
                AwayHalfScore = @AwayHalfScore,
                PeriodScores = @PeriodScores,
                EndTime = @EndTime,
                UpdateTime = GETDATE()
            WHERE EvtID = @EventId";

        var parameters = new
        {
            EventId = eventId,
            Status = (int)result.FinalStatus,
            HomeScore = result.HomeScore,
            AwayScore = result.AwayScore,
            HomeHalfScore = result.HomeHalfScore,
            AwayHalfScore = result.AwayHalfScore,
            PeriodScores = result.PeriodScores,
            EndTime = result.EndTime
        };

        await _db.ExecuteAsync(sql, parameters);
        _logger.LogInformation(
            "更新比賽結果: EvtID={EventId}, Score={HomeScore}:{AwayScore}, Status={Status}",
            eventId, result.HomeScore, result.AwayScore, result.FinalStatus);
    }
}

