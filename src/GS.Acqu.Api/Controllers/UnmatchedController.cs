using GS.Acqu.Application.DTOs;
using GS.Acqu.Domain.Enums;
using GS.Acqu.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace GS.Acqu.Api.Controllers;

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
            SportType = e.Match.SportType,
            SportTypeName = e.Match.SportType.ToString(),
            LeagueName = e.Match.League.Name,
            HomeTeamName = e.Match.HomeTeam.Name,
            AwayTeamName = e.Match.AwayTeam.Name,
            ScheduleTime = e.Match.ScheduleTime,
            Reason = e.Reason,
            ReasonName = e.Reason.ToString(),
            CreatedAt = e.CreatedAt,
            UpdatedAt = e.UpdatedAt
        });

        return Ok(result);
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
}
