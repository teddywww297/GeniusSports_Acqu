using System.Collections.Concurrent;
using GS.Acqu.Domain.Entities;
using GS.Acqu.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace GS.Acqu.Infrastructure.Services;

/// <summary>
/// 賽事資訊快取服務實作
/// 使用 ConcurrentDictionary 在記憶體中快取賽事的聯賽、球隊等基本資訊
/// 解決 WatchMatches 串流不包含名稱資料的問題
/// </summary>
public class MatchInfoCacheService : IMatchInfoCacheService
{
    private readonly ConcurrentDictionary<string, MatchInfoCacheEntry> _cache = new();
    private readonly ILogger<MatchInfoCacheService> _logger;
    
    private long _hitCount;
    private long _missCount;
    private DateTime? _lastUpdated;

    public MatchInfoCacheService(ILogger<MatchInfoCacheService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public void AddOrUpdate(string sourceMatchId, MatchInfo matchInfo)
    {
        if (string.IsNullOrEmpty(sourceMatchId))
            return;

        var entry = new MatchInfoCacheEntry
        {
            SourceMatchId = sourceMatchId,
            SportType = matchInfo.SportType,
            League = matchInfo.League,
            HomeTeam = matchInfo.HomeTeam,
            AwayTeam = matchInfo.AwayTeam,
            ScheduleTime = matchInfo.ScheduleTime,
            CachedAt = DateTime.Now
        };

        _cache.AddOrUpdate(sourceMatchId, entry, (_, _) => entry);
        _lastUpdated = DateTime.Now;
    }

    /// <inheritdoc />
    public bool TryGet(string sourceMatchId, out MatchInfo? matchInfo)
    {
        matchInfo = null;

        if (string.IsNullOrEmpty(sourceMatchId))
        {
            Interlocked.Increment(ref _missCount);
            return false;
        }

        if (_cache.TryGetValue(sourceMatchId, out var entry))
        {
            Interlocked.Increment(ref _hitCount);
            
            // 轉換回 MatchInfo
            matchInfo = new MatchInfo
            {
                SourceMatchId = entry.SourceMatchId,
                SportType = entry.SportType,
                League = entry.League ?? new LeagueInfo(),
                HomeTeam = entry.HomeTeam ?? new TeamInfo(),
                AwayTeam = entry.AwayTeam ?? new TeamInfo(),
                ScheduleTime = entry.ScheduleTime
            };
            return true;
        }

        Interlocked.Increment(ref _missCount);
        return false;
    }

    /// <inheritdoc />
    public MatchInfo? GetMatchInfo(string sourceMatchId)
    {
        if (TryGet(sourceMatchId, out var matchInfo))
        {
            return matchInfo;
        }
        return null;
    }

    /// <inheritdoc />
    public int LoadFromMatches(IEnumerable<MatchInfo> matches)
    {
        var count = 0;
        foreach (var match in matches)
        {
            if (string.IsNullOrEmpty(match.SourceMatchId))
                continue;

            AddOrUpdate(match.SourceMatchId, match);
            count++;
        }

        _logger.LogInformation(
            "已載入 {Count} 筆賽事資訊到快取",
            count);

        return count;
    }

    /// <inheritdoc />
    public MatchInfo EnrichFromCache(MatchInfo feedMatchInfo)
    {
        if (string.IsNullOrEmpty(feedMatchInfo.SourceMatchId))
            return feedMatchInfo;

        if (!_cache.TryGetValue(feedMatchInfo.SourceMatchId, out var entry))
        {
            Interlocked.Increment(ref _missCount);
            _logger.LogDebug(
                "快取未命中: MatchId={MatchId}",
                feedMatchInfo.SourceMatchId);
            return feedMatchInfo;
        }

        Interlocked.Increment(ref _hitCount);

        // 從快取補齊缺失的資料
        return new MatchInfo
        {
            SourceMatchId = feedMatchInfo.SourceMatchId,
            SportType = feedMatchInfo.SportType,
            GameType = feedMatchInfo.GameType,
            Status = feedMatchInfo.Status,
            ScheduleTime = entry.ScheduleTime, // 使用快取的預定時間
            SourceUpdateTime = feedMatchInfo.SourceUpdateTime,
            LiveTime = feedMatchInfo.LiveTime,
            
            // 從快取補齊名稱資訊
            League = entry.League ?? feedMatchInfo.League,
            HomeTeam = entry.HomeTeam ?? feedMatchInfo.HomeTeam,
            AwayTeam = entry.AwayTeam ?? feedMatchInfo.AwayTeam,
            
            // 使用串流的即時比分
            Score = feedMatchInfo.Score
        };
    }

    /// <inheritdoc />
    public int CleanupExpired(TimeSpan olderThan)
    {
        var threshold = DateTime.Now - olderThan;
        var toRemove = _cache
            .Where(kvp => kvp.Value.CachedAt < threshold)
            .Select(kvp => kvp.Key)
            .ToList();

        var removed = 0;
        foreach (var key in toRemove)
        {
            if (_cache.TryRemove(key, out _))
                removed++;
        }

        if (removed > 0)
        {
            _logger.LogInformation(
                "已清除 {Count} 筆過期快取 (超過 {Hours} 小時)",
                removed,
                olderThan.TotalHours);
        }

        return removed;
    }

    /// <inheritdoc />
    public MatchInfoCacheStatistics GetStatistics()
    {
        var countBySport = _cache
            .GroupBy(kvp => kvp.Value.SportType.ToString())
            .ToDictionary(g => g.Key, g => g.Count());

        return new MatchInfoCacheStatistics
        {
            TotalCount = _cache.Count,
            CountBySport = countBySport,
            HitCount = Interlocked.Read(ref _hitCount),
            MissCount = Interlocked.Read(ref _missCount),
            LastUpdated = _lastUpdated
        };
    }

    /// <summary>
    /// 快取項目
    /// </summary>
    private class MatchInfoCacheEntry
    {
        public required string SourceMatchId { get; init; }
        public Domain.Enums.SportType SportType { get; init; }
        public LeagueInfo? League { get; init; }
        public TeamInfo? HomeTeam { get; init; }
        public TeamInfo? AwayTeam { get; init; }
        public DateTime ScheduleTime { get; init; }
        public DateTime CachedAt { get; init; }
    }
}
