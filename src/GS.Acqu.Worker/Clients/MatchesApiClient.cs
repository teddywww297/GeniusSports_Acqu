using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using GS.Acqu.Domain.Entities;
using GS.Acqu.Domain.Enums;
using GS.Acqu.Worker.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GS.Acqu.Worker.Clients;

/// <summary>
/// 賽事 REST API 客戶端
/// 用於從 admin API 取得完整賽事資訊（聯賽、球隊名稱）
/// </summary>
public interface IMatchesApiClient
{
    /// <summary>
    /// 取得指定日期範圍的賽事清單
    /// </summary>
    Task<IEnumerable<MatchInfo>> GetMatchesAsync(
        int sportId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);
}

public class MatchesApiClient : IMatchesApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<MatchesApiClient> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public MatchesApiClient(
        HttpClient httpClient,
        ILogger<MatchesApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task<IEnumerable<MatchInfo>> GetMatchesAsync(
        int sportId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        var allMatches = new List<MatchInfo>();
        var startDateStr = startDate.ToString("yyyy/MM/dd");
        var endDateStr = endDate.ToString("yyyy/MM/dd");

        // 同時取得早盤 (MarketPhase=0) 和走地 (MarketPhase=1) 賽事
        foreach (var marketPhase in new[] { 0, 1 })
        {
            try
            {
                var url = $"/api/odds/matches?MarketPhase={marketPhase}&SportId={sportId}&StartDate={Uri.EscapeDataString(startDateStr)}&EndDate={Uri.EscapeDataString(endDateStr)}";

                _logger.LogDebug("呼叫賽事 API: {Url}", url);

                var response = await _httpClient.GetAsync(url, cancellationToken);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<MatchesApiResponse>(_jsonOptions, cancellationToken);

                if (result?.Items != null && result.Items.Count > 0)
                {
                    var matches = result.Items.Select(item => ToMatchInfo(item, sportId)).ToList();
                    allMatches.AddRange(matches);

                    _logger.LogInformation(
                        "從 API 取得 {Count} 筆賽事: SportId={SportId}, MarketPhase={Phase}, {StartDate} ~ {EndDate}",
                        matches.Count, sportId, marketPhase == 0 ? "早盤" : "走地", startDateStr, endDateStr);
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "賽事 API 請求失敗: SportId={SportId}, MarketPhase={Phase}", sportId, marketPhase);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "賽事 API 回應解析失敗: SportId={SportId}, MarketPhase={Phase}", sportId, marketPhase);
            }
        }

        // 去重複 (以 MatchId 為鍵)
        var distinctMatches = allMatches
            .GroupBy(m => m.SourceMatchId)
            .Select(g => g.First())
            .ToList();

        _logger.LogInformation(
            "SportId={SportId} 總共取得 {Count} 筆不重複賽事",
            sportId, distinctMatches.Count);

        return distinctMatches;
    }

    private static MatchInfo ToMatchInfo(MatchApiItem item, int sportId)
    {
        return new MatchInfo
        {
            SourceMatchId = item.MatchId.ToString(),
            SportType = ToSportType(sportId),
            GameType = ToGameType(item.Status),
            Status = ToMatchStatus(item.Status),
            ScheduleTime = item.MatchTime.ToLocalTime(),
            SourceUpdateTime = item.Timestamp.ToLocalTime(),
            League = new LeagueInfo
            {
                SourceLeagueId = item.CompetitionId,
                Name = item.Competition
            },
            HomeTeam = new TeamInfo
            {
                SourceTeamId = "",
                Name = item.HomeTeam
            },
            AwayTeam = new TeamInfo
            {
                SourceTeamId = "",
                Name = item.AwayTeam
            }
        };
    }

    private static SportType ToSportType(int sportId) => sportId switch
    {
        1 => SportType.Soccer,
        3 => SportType.Basketball,
        7 => SportType.MLB,
        _ => SportType.Soccer
    };

    private static GameType ToGameType(int status) => status switch
    {
        0 => GameType.Early,      // 未開始
        1 => GameType.Live,       // 進行中
        2 => GameType.Live,       // 比賽中
        _ => GameType.Early
    };

    private static MatchStatus ToMatchStatus(int status) => status switch
    {
        0 => MatchStatus.NotStarted,
        1 => MatchStatus.Live,
        2 => MatchStatus.Live,
        3 => MatchStatus.Ended,
        _ => MatchStatus.NotStarted
    };
}

#region API Response Models

internal class MatchesApiResponse
{
    public List<MatchApiItem> Items { get; set; } = [];
}

internal class MatchApiItem
{
    public long MatchId { get; set; }
    public string CompetitionId { get; set; } = "";
    public string Competition { get; set; } = "";
    public string HomeTeam { get; set; } = "";
    public string AwayTeam { get; set; } = "";
    public DateTime MatchTime { get; set; }
    public int Status { get; set; }
    public DateTime Timestamp { get; set; }
}

#endregion
