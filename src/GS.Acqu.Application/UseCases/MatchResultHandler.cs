using GS.Acqu.Application.Interfaces;
using GS.Acqu.Domain.Entities;
using GS.Acqu.Domain.Enums;
using GS.Acqu.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace GS.Acqu.Application.UseCases;

/// <summary>
/// 比賽結果處理器實作
/// </summary>
public class MatchResultHandler : IMatchResultHandler
{
    private readonly IMatchRepository _matchRepo;
    private readonly IResultRepository _resultRepo;
    private readonly ILogger<MatchResultHandler> _logger;

    public MessageType MessageType => MessageType.MatchResult;

    public MatchResultHandler(
        IMatchRepository matchRepo,
        IResultRepository resultRepo,
        ILogger<MatchResultHandler> logger)
    {
        _matchRepo = matchRepo;
        _resultRepo = resultRepo;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ProcessResult> HandleAsync(MatchResult result, CancellationToken cancellationToken = default)
    {
        try
        {
            // 查詢對應的系統賽事編號
            var eventId = await _matchRepo.GetEventIdAsync(result.SourceMatchId);

            if (eventId == null)
            {
                _logger.LogWarning(
                    "結果對應賽事不存在: SourceMatchId={SourceMatchId}",
                    result.SourceMatchId);
                
                return ProcessResult.MatchNotFound;
            }

            // 更新比賽結果
            await _resultRepo.UpdateResultAsync(eventId.Value, result);

            _logger.LogInformation(
                "更新比賽結果: EvtID={EventId}, 比分={HomeScore}:{AwayScore}, 狀態={Status}",
                eventId.Value, result.HomeScore, result.AwayScore, result.FinalStatus);

            return ProcessResult.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "處理比賽結果失敗: SourceMatchId={SourceMatchId}", result.SourceMatchId);
            return ProcessResult.DatabaseError;
        }
    }
}

