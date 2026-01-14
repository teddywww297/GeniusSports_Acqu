using GS.Acqu.Application.DTOs;
using GS.Acqu.Domain.Enums;
using GS.Acqu.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace GS.Acqu.Worker.Controllers;

/// <summary>
/// 未對點賽事 API
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class UnmatchedController : ControllerBase
{
    private readonly IUnmatchedEventStore _unmatchedStore;
    private readonly ILogger<UnmatchedController> _logger;

    public UnmatchedController(
        IUnmatchedEventStore unmatchedStore,
        ILogger<UnmatchedController> logger)
    {
        _unmatchedStore = unmatchedStore;
        _logger = logger;
    }

    /// <summary>
    /// 取得未對點賽事清單
    /// </summary>
    /// <param name="sportType">球種類型 (可選)</param>
    /// <returns>未對點賽事清單</returns>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<UnmatchedEventDto>>> GetAll([FromQuery] SportType? sportType = null)
    {
        var events = await _unmatchedStore.GetAllAsync(sportType);

        var result = events.Select(e => new UnmatchedEventDto
        {
            SourceMatchId = e.SourceMatchId,
            Source = e.Source,
            SportType = e.Match.SportType,
            SportTypeName = e.Match.SportType.ToString(),

            // 聯賽資訊
            LeagueName = e.Match.League.Name,
            LeagueNameCn = e.Match.League.NameCn,
            LeagueNameEn = e.Match.League.NameEn,
            LeagueMatched = e.LeagueMatchResult != null,
            LeagueId = e.LeagueId,

            // 主隊資訊
            HomeTeamName = e.Match.HomeTeam.Name,
            HomeTeamNameCn = e.Match.HomeTeam.NameCn,
            HomeTeamNameEn = e.Match.HomeTeam.NameEn,
            HomeTeamMatched = e.HomeTeamMatchResult != null,
            HomeTeamId = e.HomeId,

            // 客隊資訊
            AwayTeamName = e.Match.AwayTeam.Name,
            AwayTeamNameCn = e.Match.AwayTeam.NameCn,
            AwayTeamNameEn = e.Match.AwayTeam.NameEn,
            AwayTeamMatched = e.AwayTeamMatchResult != null,
            AwayTeamId = e.AwayId,

            // 賽事資訊
            ScheduleTime = e.Match.ScheduleTime,

            // 狀態資訊
            Reason = e.Reason,
            ReasonName = e.Reason.ToString(),
            MatchStatus = e.MatchStatus,
            IsPartialMatch = e.IsPartialMatch,
            RetryCount = e.RetryCount,
            CreatedAt = e.CreatedAt,
            UpdatedAt = e.UpdatedAt
        });

        return Ok(result);
    }

    /// <summary>
    /// 取得統計資訊
    /// </summary>
    [HttpGet("statistics")]
    public async Task<ActionResult<UnmatchedStatistics>> GetStatistics()
    {
        var stats = await _unmatchedStore.GetStatisticsAsync();
        return Ok(stats);
    }

    /// <summary>
    /// 取得未對點賽事數量
    /// </summary>
    /// <param name="sportType">球種類型 (可選)</param>
    /// <returns>數量</returns>
    [HttpGet("count")]
    public async Task<ActionResult<CountResult>> GetCount([FromQuery] SportType? sportType = null)
    {
        var count = await _unmatchedStore.GetCountAsync(sportType);
        return Ok(new CountResult { Count = count });
    }

    /// <summary>
    /// 取得單筆未對點賽事
    /// </summary>
    /// <param name="sourceMatchId">來源賽事編號</param>
    [HttpGet("{sourceMatchId}")]
    public async Task<ActionResult<UnmatchedEventDto>> GetById(string sourceMatchId)
    {
        var e = await _unmatchedStore.GetBySourceMatchIdAsync(sourceMatchId);
        if (e == null)
            return NotFound();

        var result = new UnmatchedEventDto
        {
            SourceMatchId = e.SourceMatchId,
            Source = e.Source,
            SportType = e.Match.SportType,
            SportTypeName = e.Match.SportType.ToString(),

            LeagueName = e.Match.League.Name,
            LeagueNameCn = e.Match.League.NameCn,
            LeagueNameEn = e.Match.League.NameEn,
            LeagueMatched = e.LeagueMatchResult != null,
            LeagueId = e.LeagueId,

            HomeTeamName = e.Match.HomeTeam.Name,
            HomeTeamNameCn = e.Match.HomeTeam.NameCn,
            HomeTeamNameEn = e.Match.HomeTeam.NameEn,
            HomeTeamMatched = e.HomeTeamMatchResult != null,
            HomeTeamId = e.HomeId,

            AwayTeamName = e.Match.AwayTeam.Name,
            AwayTeamNameCn = e.Match.AwayTeam.NameCn,
            AwayTeamNameEn = e.Match.AwayTeam.NameEn,
            AwayTeamMatched = e.AwayTeamMatchResult != null,
            AwayTeamId = e.AwayId,

            ScheduleTime = e.Match.ScheduleTime,

            Reason = e.Reason,
            ReasonName = e.Reason.ToString(),
            MatchStatus = e.MatchStatus,
            IsPartialMatch = e.IsPartialMatch,
            RetryCount = e.RetryCount,
            CreatedAt = e.CreatedAt,
            UpdatedAt = e.UpdatedAt
        };

        return Ok(result);
    }

    /// <summary>
    /// 移除未對點賽事
    /// </summary>
    /// <param name="sourceMatchId">來源賽事編號</param>
    [HttpDelete("{sourceMatchId}")]
    public async Task<IActionResult> Remove(string sourceMatchId)
    {
        await _unmatchedStore.RemoveAsync(sourceMatchId);
        _logger.LogInformation("已移除未對點賽事: {SourceMatchId}", sourceMatchId);
        return NoContent();
    }

    /// <summary>
    /// 清除過期賽事
    /// </summary>
    /// <param name="hours">過期時間 (小時)</param>
    [HttpPost("cleanup")]
    public async Task<IActionResult> Cleanup([FromQuery] int hours = 24)
    {
        await _unmatchedStore.ClearExpiredAsync(TimeSpan.FromHours(hours));
        var remainingCount = await _unmatchedStore.GetCountAsync();
        return Ok(new { Message = $"已清除 {hours} 小時前的過期賽事", RemainingCount = remainingCount });
    }
}
