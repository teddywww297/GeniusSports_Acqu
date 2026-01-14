using GS.Acqu.Application.Interfaces;
using GS.Acqu.Domain.Entities;
using GS.Acqu.Domain.Enums;
using GS.Acqu.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace GS.Acqu.Application.UseCases;

/// <summary>
/// 賽事資訊處理器實作
/// </summary>
public class MatchInfoHandler : IMatchInfoHandler
{
    private readonly IMatcherService _matcher;
    private readonly IMatchRepository _matchRepo;
    private readonly IUnmatchedEventStore _unmatchedStore;
    private readonly ILogger<MatchInfoHandler> _logger;

    // 是否啟用自動新增（可從配置讀取）
    private readonly bool _autoInsertEnabled = false;
    
    // 是否自動建立賽事（從配置讀取，預設關閉）
    private readonly bool _autoCreateEventEnabled;

    public MessageType MessageType => MessageType.MatchInfo;

    public MatchInfoHandler(
        IMatcherService matcher,
        IMatchRepository matchRepo,
        IUnmatchedEventStore unmatchedStore,
        ILogger<MatchInfoHandler> logger,
        bool autoCreateEventEnabled = false)
    {
        _matcher = matcher;
        _matchRepo = matchRepo;
        _unmatchedStore = unmatchedStore;
        _logger = logger;
        _autoCreateEventEnabled = autoCreateEventEnabled;
    }

    /// <inheritdoc />
    public async Task<ProcessResult> HandleAsync(MatchInfo match, CancellationToken cancellationToken = default)
    {
        try
        {
            // 1. 對點聯賽
            var leagueData = await _matcher.MatchLeagueAsync(match.SportType, match.League);
            if (leagueData == null)
            {
                // 嘗試自動新增
                if (_autoInsertEnabled)
                {
                    leagueData = await _matcher.AutoInsertLeagueAsync(match.SportType, match.League);
                }

                if (leagueData == null)
                {
                    _logger.LogDebug(
                        "聯賽未對點: {LeagueName}, SourceMatchId={SourceMatchId}",
                        match.League.Name, match.SourceMatchId);

                    // 記錄詳細對點資訊
                    await _unmatchedStore.AddAsync(
                        match,
                        ProcessResult.LeagueNotMatched,
                        leagueMatch: null,      // 聯賽未對點
                        homeTeamMatch: null,    // 後續未執行
                        awayTeamMatch: null);   // 後續未執行

                    return ProcessResult.LeagueNotMatched;
                }
            }

            // 2. 對點主隊
            var homeTeamData = await _matcher.MatchTeamAsync(match.SportType, match.HomeTeam);
            if (homeTeamData == null)
            {
                // 嘗試自動新增
                if (_autoInsertEnabled)
                {
                    homeTeamData = await _matcher.AutoInsertTeamAsync(match.SportType, match.HomeTeam);
                }

                if (homeTeamData == null)
                {
                    _logger.LogDebug(
                        "主隊未對點: {TeamName}, SourceMatchId={SourceMatchId}",
                        match.HomeTeam.Name, match.SourceMatchId);

                    // 記錄部分對點結果 (聯賽已對點)
                    await _unmatchedStore.AddAsync(
                        match,
                        ProcessResult.TeamNotMatched,
                        leagueMatch: leagueData,    // 聯賽已對點 ✓
                        homeTeamMatch: null,        // 主隊未對點 ✗
                        awayTeamMatch: null);       // 後續未執行

                    return ProcessResult.TeamNotMatched;
                }
            }

            // 3. 對點客隊
            var awayTeamData = await _matcher.MatchTeamAsync(match.SportType, match.AwayTeam);
            if (awayTeamData == null)
            {
                // 嘗試自動新增
                if (_autoInsertEnabled)
                {
                    awayTeamData = await _matcher.AutoInsertTeamAsync(match.SportType, match.AwayTeam);
                }

                if (awayTeamData == null)
                {
                    _logger.LogDebug(
                        "客隊未對點: {TeamName}, SourceMatchId={SourceMatchId}",
                        match.AwayTeam.Name, match.SourceMatchId);

                    // 記錄部分對點結果 (聯賽、主隊已對點)
                    await _unmatchedStore.AddAsync(
                        match,
                        ProcessResult.TeamNotMatched,
                        leagueMatch: leagueData,     // 聯賽已對點 ✓
                        homeTeamMatch: homeTeamData, // 主隊已對點 ✓
                        awayTeamMatch: null);        // 客隊未對點 ✗

                    return ProcessResult.TeamNotMatched;
                }
            }

            // 4. 新增或更新賽事
            var existingEventId = await _matchRepo.GetEventIdAsync(match.SourceMatchId);

            if (existingEventId.HasValue)
            {
                await _matchRepo.UpdateEventAsync(existingEventId.Value, match);
                _logger.LogDebug("更新賽事: EvtID={EventId}", existingEventId.Value);
            }
            else
            {
                // 嘗試反向對點：查找現有賽事（控端建立的）
                // 使用 leagueData.CatId 而非 match.SportType，確保 CatID 正確
                var foundEventId = await _matchRepo.FindExistingEventAsync(
                    leagueData.CatId,  // 使用聯賽的 CatID
                    leagueData.Id,
                    homeTeamData.Id,
                    awayTeamData.Id,
                    match.ScheduleTime);

                if (foundEventId.HasValue)
                {
                    // 反向對點成功：更新現有賽事的 ProviderMatchId
                    await _matchRepo.UpdateProviderMatchIdAsync(foundEventId.Value, match.SourceMatchId);
                    await _matchRepo.UpdateEventAsync(foundEventId.Value, match);
                    
                    _logger.LogInformation(
                        "反向對點成功: EvtID={EventId}, ProviderMatchId={SourceMatchId}, CatID={CatId}, {Home} vs {Away}",
                        foundEventId.Value, match.SourceMatchId, leagueData.CatId,
                        match.HomeTeam.Name, match.AwayTeam.Name);
                }
                else if (_autoCreateEventEnabled)
                {
                    // 沒有現有賽事，且開啟自動建立功能才新建
                    var newEventId = await _matchRepo.CreateEventAsync(
                        match, leagueData.CatId, leagueData.Id, homeTeamData.Id, awayTeamData.Id);

                    _logger.LogInformation(
                        "新增賽事: EvtID={EventId}, CatID={CatId}, LeagueID={LeagueID}, ModID={ModID}, {Home} vs {Away}",
                        newEventId, leagueData.CatId, leagueData.Id, leagueData.ModId,
                        match.HomeTeam.Name, match.AwayTeam.Name);
                }
                else
                {
                    // 自動建立功能關閉，僅記錄 log
                    _logger.LogDebug(
                        "對點完成但未找到現有賽事，AutoCreateEvent=false 跳過建立: SourceMatchId={SourceMatchId}, CatID={CatId}, {Home} vs {Away}",
                        match.SourceMatchId, leagueData.CatId, match.HomeTeam.Name, match.AwayTeam.Name);
                }
            }

            // 對點成功，從未對點清單中移除
            await _unmatchedStore.RemoveAsync(match.SourceMatchId);

            return ProcessResult.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "處理賽事資訊失敗: SourceMatchId={SourceMatchId}", match.SourceMatchId);
            return ProcessResult.DatabaseError;
        }
    }
}
