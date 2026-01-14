using System.Collections.Concurrent;
using GS.Acqu.Domain.Entities;
using GS.Acqu.Domain.Enums;
using GS.Acqu.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace GS.Acqu.Infrastructure.Services;

/// <summary>
/// 未對點賽事記憶體儲存實作 (方案 B: 共用記憶體)
/// </summary>
public class UnmatchedEventStore : IUnmatchedEventStore
{
    private readonly ConcurrentDictionary<string, UnmatchedEvent> _store = new();
    private readonly ILogger<UnmatchedEventStore> _logger;

    public UnmatchedEventStore(ILogger<UnmatchedEventStore> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task AddAsync(
        MatchInfo match,
        ProcessResult reason,
        MatchData? leagueMatch = null,
        MatchData? homeTeamMatch = null,
        MatchData? awayTeamMatch = null)
    {
        var unmatchedEvent = new UnmatchedEvent
        {
            SourceMatchId = match.SourceMatchId,
            Source = "GS",
            Match = match,
            Reason = reason,
            LeagueMatchResult = leagueMatch,
            HomeTeamMatchResult = homeTeamMatch,
            AwayTeamMatchResult = awayTeamMatch,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        _store.AddOrUpdate(
            match.SourceMatchId,
            unmatchedEvent,
            (_, existing) =>
            {
                // 保留建立時間，更新其他資訊
                existing.Match = match;
                existing.Reason = reason;
                existing.LeagueMatchResult = leagueMatch;
                existing.HomeTeamMatchResult = homeTeamMatch;
                existing.AwayTeamMatchResult = awayTeamMatch;
                existing.UpdatedAt = DateTime.Now;
                existing.RetryCount++;
                return existing;
            });

        _logger.LogDebug(
            "加入未對點賽事: {SourceMatchId}, 原因: {Reason}, 狀態: {Status}",
            match.SourceMatchId, reason, unmatchedEvent.MatchStatus);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IEnumerable<UnmatchedEvent>> GetAllAsync(SportType? sportType = null)
    {
        IEnumerable<UnmatchedEvent> result = _store.Values;

        if (sportType.HasValue)
        {
            result = result.Where(x => x.Match.SportType == sportType.Value);
        }

        return Task.FromResult(result.OrderByDescending(x => x.UpdatedAt).AsEnumerable());
    }

    /// <inheritdoc />
    public Task<UnmatchedEvent?> GetBySourceMatchIdAsync(string sourceMatchId)
    {
        _store.TryGetValue(sourceMatchId, out var unmatchedEvent);
        return Task.FromResult(unmatchedEvent);
    }

    /// <inheritdoc />
    public Task RemoveAsync(string sourceMatchId)
    {
        if (_store.TryRemove(sourceMatchId, out var removed))
        {
            _logger.LogDebug(
                "移除未對點賽事: {SourceMatchId}, 曾重試 {RetryCount} 次",
                sourceMatchId, removed.RetryCount);
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ClearExpiredAsync(TimeSpan expiry)
    {
        var expireTime = DateTime.Now - expiry;
        var expiredKeys = _store
            .Where(x => x.Value.UpdatedAt < expireTime)
            .Select(x => x.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            _store.TryRemove(key, out _);
        }

        if (expiredKeys.Count > 0)
        {
            _logger.LogInformation("清除 {Count} 筆過期未對點賽事", expiredKeys.Count);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<int> GetCountAsync(SportType? sportType = null)
    {
        if (sportType.HasValue)
        {
            return Task.FromResult(_store.Values.Count(x => x.Match.SportType == sportType.Value));
        }

        return Task.FromResult(_store.Count);
    }

    /// <inheritdoc />
    public Task<UnmatchedStatistics> GetStatisticsAsync()
    {
        var values = _store.Values.ToList();

        var stats = new UnmatchedStatistics
        {
            TotalCount = values.Count,
            LeagueNotMatchedCount = values.Count(x => x.Reason == ProcessResult.LeagueNotMatched),
            TeamNotMatchedCount = values.Count(x => x.Reason == ProcessResult.TeamNotMatched),
            PartialMatchCount = values.Count(x => x.IsPartialMatch),
            BySportType = values
                .GroupBy(x => x.Match.SportType)
                .ToDictionary(g => g.Key, g => g.Count()),
            ByReason = values
                .GroupBy(x => x.Reason)
                .ToDictionary(g => g.Key, g => g.Count())
        };

        return Task.FromResult(stats);
    }
}

