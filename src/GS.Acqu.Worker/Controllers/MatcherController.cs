using GS.Acqu.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace GS.Acqu.Worker.Controllers;

/// <summary>
/// 對點服務 API
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class MatcherController : ControllerBase
{
    private readonly IMatcherService _matcher;
    private readonly ILogger<MatcherController> _logger;

    public MatcherController(
        IMatcherService matcher,
        ILogger<MatcherController> logger)
    {
        _matcher = matcher;
        _logger = logger;
    }

    /// <summary>
    /// 重新載入對點快取
    /// </summary>
    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshCache()
    {
        _logger.LogInformation("收到重新載入對點快取請求");

        await _matcher.RefreshCacheAsync();

        return Ok(new { Message = "對點快取已重新載入" });
    }

    /// <summary>
    /// 取得快取統計資訊
    /// </summary>
    [HttpGet("stats")]
    public IActionResult GetStats()
    {
        var (leagueCount, teamCount) = _matcher.GetCacheStats();

        return Ok(new
        {
            LeagueCount = leagueCount,
            TeamCount = teamCount
        });
    }
}
