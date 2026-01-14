using GS.Acqu.Domain.Enums;

namespace GS.Acqu.Application.DTOs;

/// <summary>
/// 未對點賽事 DTO
/// </summary>
public class UnmatchedEventDto
{
    /// <summary>
    /// 來源賽事編號
    /// </summary>
    public string SourceMatchId { get; set; } = string.Empty;

    /// <summary>
    /// 來源站點
    /// </summary>
    public string Source { get; set; } = "GS";

    /// <summary>
    /// 球種類型
    /// </summary>
    public SportType SportType { get; set; }

    /// <summary>
    /// 球種類型名稱
    /// </summary>
    public string SportTypeName { get; set; } = string.Empty;

    // ===== 聯賽資訊 =====

    /// <summary>
    /// 聯賽名稱 (原始)
    /// </summary>
    public string LeagueName { get; set; } = string.Empty;

    /// <summary>
    /// 聯賽中文名
    /// </summary>
    public string? LeagueNameCn { get; set; }

    /// <summary>
    /// 聯賽英文名
    /// </summary>
    public string? LeagueNameEn { get; set; }

    /// <summary>
    /// 聯賽是否已對點
    /// </summary>
    public bool LeagueMatched { get; set; }

    /// <summary>
    /// 聯賽對點 ID (0=未對點)
    /// </summary>
    public int LeagueId { get; set; }

    // ===== 主隊資訊 =====

    /// <summary>
    /// 主隊名稱 (原始)
    /// </summary>
    public string HomeTeamName { get; set; } = string.Empty;

    /// <summary>
    /// 主隊中文名
    /// </summary>
    public string? HomeTeamNameCn { get; set; }

    /// <summary>
    /// 主隊英文名
    /// </summary>
    public string? HomeTeamNameEn { get; set; }

    /// <summary>
    /// 主隊是否已對點
    /// </summary>
    public bool HomeTeamMatched { get; set; }

    /// <summary>
    /// 主隊對點 ID (0=未對點)
    /// </summary>
    public int HomeTeamId { get; set; }

    // ===== 客隊資訊 =====

    /// <summary>
    /// 客隊名稱 (原始)
    /// </summary>
    public string AwayTeamName { get; set; } = string.Empty;

    /// <summary>
    /// 客隊中文名
    /// </summary>
    public string? AwayTeamNameCn { get; set; }

    /// <summary>
    /// 客隊英文名
    /// </summary>
    public string? AwayTeamNameEn { get; set; }

    /// <summary>
    /// 客隊是否已對點
    /// </summary>
    public bool AwayTeamMatched { get; set; }

    /// <summary>
    /// 客隊對點 ID (0=未對點)
    /// </summary>
    public int AwayTeamId { get; set; }

    // ===== 賽事資訊 =====

    /// <summary>
    /// 預定開賽時間
    /// </summary>
    public DateTime ScheduleTime { get; set; }

    // ===== 狀態資訊 =====

    /// <summary>
    /// 未對點原因
    /// </summary>
    public ProcessResult Reason { get; set; }

    /// <summary>
    /// 未對點原因名稱
    /// </summary>
    public string ReasonName { get; set; } = string.Empty;

    /// <summary>
    /// 對點狀態摘要 (如: League:✓ Home:✗ Away:✗)
    /// </summary>
    public string MatchStatus { get; set; } = string.Empty;

    /// <summary>
    /// 是否為部分對點
    /// </summary>
    public bool IsPartialMatch { get; set; }

    /// <summary>
    /// 重試次數
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// 建立時間
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 更新時間
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// 數量結果
/// </summary>
public class CountResult
{
    /// <summary>
    /// 數量
    /// </summary>
    public int Count { get; set; }
}

