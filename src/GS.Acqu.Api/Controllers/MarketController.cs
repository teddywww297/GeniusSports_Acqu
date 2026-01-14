using GS.Acqu.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace GS.Acqu.Api.Controllers;

/// <summary>
/// 盤口管理 API
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class MarketController : ControllerBase
{
    private readonly IMatchRepository _matchRepo;
    private readonly IMarketRepository _marketRepo;
    private readonly ILogger<MarketController> _logger;

    public MarketController(
        IMatchRepository matchRepo,
        IMarketRepository marketRepo,
        ILogger<MarketController> logger)
    {
        _matchRepo = matchRepo;
        _marketRepo = marketRepo;
        _logger = logger;
    }

    /// <summary>
    /// 查詢缺失盤口的賽事
    /// </summary>
    /// <param name="limit">最大筆數 (預設 50)</param>
    /// <returns>缺失盤口的賽事清單</returns>
    [HttpGet("missing")]
    public async Task<ActionResult<IEnumerable<MissingMarketEventDto>>> GetMissingMarkets([FromQuery] int limit = 50)
    {
        var events = await _matchRepo.GetEventsWithoutMarketsAsync(limit);
        
        var result = events.Select(e => new MissingMarketEventDto
        {
            EvtId = e.EvtId,
            ProviderMatchId = e.ProviderMatchId,
            CatId = e.CatId
        });

        return Ok(result);
    }

    /// <summary>
    /// 根據 ProviderMatchId 補建盤口 (單筆)
    /// </summary>
    /// <param name="providerMatchId">供應商賽事編號</param>
    /// <returns>補建結果</returns>
    [HttpPost("repair/{providerMatchId}")]
    public async Task<ActionResult<RepairResultDto>> RepairMarket(string providerMatchId)
    {
        _logger.LogInformation("收到補建盤口請求: ProviderMatchId={ProviderMatchId}", providerMatchId);

        // 檢查賽事是否存在
        var eventId = await _matchRepo.GetEventIdAsync(providerMatchId);
        if (eventId == null)
        {
            return NotFound(new { Message = $"賽事不存在: {providerMatchId}" });
        }

        return Ok(new RepairResultDto
        {
            ProviderMatchId = providerMatchId,
            EvtId = eventId.Value,
            Message = "賽事已確認存在，請使用 Worker 服務的補建機制"
        });
    }

    /// <summary>
    /// 批次補建缺失盤口
    /// </summary>
    /// <param name="request">補建請求</param>
    /// <returns>補建結果</returns>
    [HttpPost("repair-batch")]
    public async Task<ActionResult<BatchRepairResultDto>> RepairMarketsBatch([FromBody] BatchRepairRequest request)
    {
        if (request.ProviderMatchIds == null || request.ProviderMatchIds.Length == 0)
        {
            return BadRequest(new { Message = "請提供 ProviderMatchIds" });
        }

        _logger.LogInformation(
            "收到批次補建盤口請求: 數量={Count}",
            request.ProviderMatchIds.Length);

        var results = new List<RepairResultDto>();
        var successCount = 0;
        var failedCount = 0;

        foreach (var providerMatchId in request.ProviderMatchIds)
        {
            var eventId = await _matchRepo.GetEventIdAsync(providerMatchId);
            if (eventId == null)
            {
                results.Add(new RepairResultDto
                {
                    ProviderMatchId = providerMatchId,
                    EvtId = 0,
                    Message = "賽事不存在"
                });
                failedCount++;
                continue;
            }

            results.Add(new RepairResultDto
            {
                ProviderMatchId = providerMatchId,
                EvtId = eventId.Value,
                Message = "賽事已確認存在"
            });
            successCount++;
        }

        return Ok(new BatchRepairResultDto
        {
            TotalCount = request.ProviderMatchIds.Length,
            SuccessCount = successCount,
            FailedCount = failedCount,
            Results = results,
            Message = "請使用 Worker 服務的補建機制完成實際盤口補建"
        });
    }
}

#region DTOs

/// <summary>
/// 缺失盤口賽事 DTO
/// </summary>
public class MissingMarketEventDto
{
    /// <summary>
    /// 系統賽事編號
    /// </summary>
    public int EvtId { get; set; }

    /// <summary>
    /// 供應商賽事編號
    /// </summary>
    public string ProviderMatchId { get; set; } = string.Empty;

    /// <summary>
    /// 球種編號
    /// </summary>
    public int CatId { get; set; }
}

/// <summary>
/// 補建結果 DTO
/// </summary>
public class RepairResultDto
{
    /// <summary>
    /// 供應商賽事編號
    /// </summary>
    public string ProviderMatchId { get; set; } = string.Empty;

    /// <summary>
    /// 系統賽事編號
    /// </summary>
    public int EvtId { get; set; }

    /// <summary>
    /// 訊息
    /// </summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// 批次補建請求
/// </summary>
public class BatchRepairRequest
{
    /// <summary>
    /// 供應商賽事編號清單
    /// </summary>
    public string[] ProviderMatchIds { get; set; } = [];
}

/// <summary>
/// 批次補建結果 DTO
/// </summary>
public class BatchRepairResultDto
{
    /// <summary>
    /// 總數
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// 成功數
    /// </summary>
    public int SuccessCount { get; set; }

    /// <summary>
    /// 失敗數
    /// </summary>
    public int FailedCount { get; set; }

    /// <summary>
    /// 詳細結果
    /// </summary>
    public List<RepairResultDto> Results { get; set; } = [];

    /// <summary>
    /// 訊息
    /// </summary>
    public string Message { get; set; } = string.Empty;
}

#endregion
