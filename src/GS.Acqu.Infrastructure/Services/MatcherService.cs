using System.Collections.Concurrent;
using Dapper;
using GS.Acqu.Application.Interfaces;
using GS.Acqu.Domain.Entities;
using GS.Acqu.Domain.Enums;
using GS.Acqu.Infrastructure.Data;
using GS.Acqu.Infrastructure.Utilities;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GS.Acqu.Infrastructure.Services;

/// <summary>
/// 對點服務實作
/// 使用現有資料表：tblLeague / tblTeam / tblSubLeague / tblSubTeam
/// </summary>
public class MatcherService : IMatcherService
{
    private readonly DatabaseOptions _dbOptions;
    private readonly ILogger<MatcherService> _logger;

    // 數據源標識
    private const string SourceCode = "GS";

    // 內存快取: SportType -> NormalizedName -> MatchData
    private ConcurrentDictionary<SportType, ConcurrentDictionary<string, MatchData>> _leagueCache = new();
    private ConcurrentDictionary<SportType, ConcurrentDictionary<string, MatchData>> _teamCache = new();

    public MatcherService(IOptions<DatabaseOptions> dbOptions, ILogger<MatcherService> logger)
    {
        _dbOptions = dbOptions.Value;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_dbOptions.MainConnectionString))
        {
            _logger.LogWarning("主資料庫連線字串(BigballMain)未設定，對點服務將無法使用資料庫功能");
        }

        // 預先初始化所有球種
        foreach (var sportType in Enum.GetValues<SportType>())
        {
            _leagueCache[sportType] = new ConcurrentDictionary<string, MatchData>();
            _teamCache[sportType] = new ConcurrentDictionary<string, MatchData>();
        }
    }

    /// <inheritdoc />
    public Task<MatchData?> MatchLeagueAsync(SportType sportType, LeagueInfo league)
    {
        // 依序嘗試對點：主名稱 → 中文名 → 英文名
        var namesToTry = new[]
        {
            league.Name,
            league.NameCn,
            league.NameEn
        };

        if (!_leagueCache.TryGetValue(sportType, out var leagues))
        {
            _logger.LogDebug("聯賽快取不存在: SportType={SportType}({SportTypeInt})", sportType, (int)sportType);
            return Task.FromResult<MatchData?>(null);
        }

        foreach (var name in namesToTry)
        {
            if (string.IsNullOrWhiteSpace(name))
                continue;

            var normalized = NameNormalizer.Normalize(name);
            if (leagues.TryGetValue(normalized, out var matchData))
            {
                return Task.FromResult<MatchData?>(matchData);
            }
            
            _logger.LogDebug("聯賽對點嘗試: SportType={SportType}({SportTypeInt}), Name={Name}, Normalized={Normalized}, CacheCount={Count}",
                sportType, (int)sportType, name, normalized, leagues.Count);
        }

        return Task.FromResult<MatchData?>(null);
    }

    /// <inheritdoc />
    public Task<MatchData?> MatchTeamAsync(SportType sportType, TeamInfo team)
    {
        // 依序嘗試對點：主名稱 → 中文名 → 英文名
        var namesToTry = new[]
        {
            team.Name,
            team.NameCn,
            team.NameEn
        };

        if (!_teamCache.TryGetValue(sportType, out var teams))
        {
            return Task.FromResult<MatchData?>(null);
        }

        foreach (var name in namesToTry)
        {
            if (string.IsNullOrWhiteSpace(name))
                continue;

            var normalized = NameNormalizer.Normalize(name);
            if (teams.TryGetValue(normalized, out var matchData))
            {
                return Task.FromResult<MatchData?>(matchData);
            }
        }

        return Task.FromResult<MatchData?>(null);
    }

    /// <inheritdoc />
    public async Task RefreshCacheAsync()
    {
        _logger.LogInformation("開始重新載入對點快取");

        if (string.IsNullOrWhiteSpace(_dbOptions.MainConnectionString))
        {
            _logger.LogWarning("主資料庫連線字串(BigballMain)未設定，跳過快取載入");
            return;
        }

        try
        {
            // 使用 BigballMain 資料庫 (聯賽/隊伍資料)
            await using var db = new SqlConnection(_dbOptions.MainConnectionString);
            await db.OpenAsync();

            // 建立新的快取字典
            var newLeagueCache = new ConcurrentDictionary<SportType, ConcurrentDictionary<string, MatchData>>();
            var newTeamCache = new ConcurrentDictionary<SportType, ConcurrentDictionary<string, MatchData>>();

            // 預先初始化所有球種
            foreach (var sportType in Enum.GetValues<SportType>())
            {
                newLeagueCache[sportType] = new ConcurrentDictionary<string, MatchData>();
                newTeamCache[sportType] = new ConcurrentDictionary<string, MatchData>();
            }

            // 載入聯賽對點（主表 tblLeague）
            await LoadLeaguesFromMainTableAsync(db, newLeagueCache);

            // 載入聯賽別名（副表 tblSubLeague）
            await LoadLeagueAliasesAsync(db, newLeagueCache);

            // 載入隊伍對點（主表 tblTeam）
            await LoadTeamsFromMainTableAsync(db, newTeamCache);

            // 載入隊伍別名（副表 tblSubTeam）
            await LoadTeamAliasesAsync(db, newTeamCache);

            // 原子性替換快取
            _leagueCache = newLeagueCache;
            _teamCache = newTeamCache;

            var (leagueCount, teamCount) = GetCacheStats();
            _logger.LogInformation(
                "對點快取載入完成: 聯賽 {LeagueCount} 筆, 隊伍 {TeamCount} 筆",
                leagueCount, teamCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "載入對點快取失敗");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<MatchData?> AutoInsertLeagueAsync(SportType sportType, LeagueInfo league)
    {
        var sourceName = NameNormalizer.SelectBestName(league.Name, league.NameCn, league.NameEn);
        if (string.IsNullOrWhiteSpace(sourceName))
            return null;

        var catId = (int)sportType;

        try
        {
            // 使用 BigballMain 資料庫 (聯賽資料)
            await using var db = new SqlConnection(_dbOptions.MainConnectionString);
            await db.OpenAsync();

            // 修正：無論新增或已存在都要返回 ID 和 ModID
            const string sql = @"
                DECLARE @ResultId INT;
                
                SELECT @ResultId = id 
                FROM tblLeague 
                WHERE MapName_GS = @SourceName AND CatID = @CatID AND Status = 1;
                
                IF @ResultId IS NULL
                BEGIN
                    INSERT INTO tblLeague (
                        CatID, Name, Name_cn, Name_en, 
                        MapName_GS, 
                        Status, CreateTime, LastTime
                    ) 
                    VALUES (
                        @CatID, @Name, @NameCn, @NameEn, 
                        @SourceName, 
                        1, GETDATE(), GETDATE()
                    );
                    SET @ResultId = SCOPE_IDENTITY();
                END
                
                SELECT @ResultId AS Id, ISNULL((SELECT ModID FROM tblLeague WHERE id = @ResultId), 0) AS ModId;";

            var leagueData = await db.QuerySingleOrDefaultAsync<(int Id, int ModId)>(sql, new
            {
                CatID = catId,
                SourceName = sourceName,
                Name = league.Name ?? sourceName,
                NameCn = league.NameCn ?? "",
                NameEn = league.NameEn ?? ""
            });

            if (leagueData.Id > 0)
            {
                var matchData = new MatchData
                {
                    Id = leagueData.Id,
                    Name = sourceName,
                    ModId = leagueData.ModId
                };

                // 加入快取
                var normalized = NameNormalizer.Normalize(sourceName);
                if (_leagueCache.TryGetValue(sportType, out var leagues))
                {
                    leagues.TryAdd(normalized, matchData);
                }

                _logger.LogDebug(
                    "自動新增/取得聯賽: CatID={CatID}, Name={Name}, LeagueID={LeagueID}",
                    catId, sourceName, leagueData.Id);

                return matchData;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "自動新增聯賽失敗: {Name}", sourceName);
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<MatchData?> AutoInsertTeamAsync(SportType sportType, TeamInfo team)
    {
        var sourceName = NameNormalizer.SelectBestName(team.Name, team.NameCn, team.NameEn);
        if (string.IsNullOrWhiteSpace(sourceName))
            return null;

        var catId = (int)sportType;

        try
        {
            // 使用 BigballMain 資料庫 (隊伍資料)
            await using var db = new SqlConnection(_dbOptions.MainConnectionString);
            await db.OpenAsync();

            // 修正：無論新增或已存在都要返回 ID
            const string sql = @"
                DECLARE @ResultId INT;
                
                SELECT @ResultId = id 
                FROM tblTeam 
                WHERE TeamName_GS = @SourceName AND CatID = @CatID AND Status = 1;
                
                IF @ResultId IS NULL
                BEGIN
                    INSERT INTO tblTeam (
                        CatID, TeamName, TeamNameShort, TeamName_en, TeamName_cn, 
                        TeamName_GS, 
                        Status, CreateTime, LastTime
                    )
                    VALUES (
                        @CatID, @TeamName, @TeamNameShort, @TeamNameEn, @TeamNameCn, 
                        @SourceName, 
                        1, GETDATE(), GETDATE()
                    );
                    SET @ResultId = SCOPE_IDENTITY();
                END
                
                SELECT @ResultId AS Id;";

            var teamId = await db.QuerySingleOrDefaultAsync<int>(sql, new
            {
                CatID = catId,
                SourceName = sourceName,
                TeamName = team.Name ?? sourceName,
                TeamNameShort = team.Name ?? sourceName,
                TeamNameEn = team.NameEn ?? "",
                TeamNameCn = team.NameCn ?? ""
            });

            if (teamId > 0)
            {
                var matchData = new MatchData
                {
                    Id = teamId,
                    Name = sourceName,
                    ModId = 0
                };

                // 加入快取
                var normalized = NameNormalizer.Normalize(sourceName);
                if (_teamCache.TryGetValue(sportType, out var teams))
                {
                    teams.TryAdd(normalized, matchData);
                }

                _logger.LogDebug(
                    "自動新增/取得球隊: CatID={CatID}, Name={Name}, TeamID={TeamID}",
                    catId, sourceName, teamId);

                return matchData;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "自動新增球隊失敗: {Name}", sourceName);
        }

        return null;
    }

    /// <inheritdoc />
    public (int LeagueCount, int TeamCount) GetCacheStats()
    {
        var leagueCount = _leagueCache.Values.Sum(x => x.Count);
        var teamCount = _teamCache.Values.Sum(x => x.Count);
        return (leagueCount, teamCount);
    }

    #region 私有方法 - 從 tblLeague / tblTeam 載入

    /// <summary>
    /// 從 tblLeague 主表載入聯賽對點資料
    /// </summary>
    private async Task LoadLeaguesFromMainTableAsync(
        SqlConnection db,
        ConcurrentDictionary<SportType, ConcurrentDictionary<string, MatchData>> cache)
    {
        // 從 MapName_GS 欄位載入 (GS 專用)
        var sql = @"
            SELECT 
                id AS LeagueId,
                CatID,
                MapName_GS AS SourceName,
                ISNULL(ModID, 0) AS ModId
            FROM tblLeague WITH(NOLOCK)
            WHERE Status = 1 
              AND MapName_GS IS NOT NULL 
              AND MapName_GS <> ''
            ORDER BY id ASC";

        var leagues = await db.QueryAsync<(int LeagueId, int CatId, string SourceName, int ModId)>(sql);

        var count = 0;
        foreach (var league in leagues)
        {
            AddToCache(cache, league.CatId, league.SourceName, league.LeagueId, league.ModId);
            count++;
        }

        _logger.LogDebug("從 tblLeague.MapName_GS 載入: {Count} 筆", count);
    }

    /// <summary>
    /// 從 tblSubLeague 別名表載入聯賽別名
    /// </summary>
    private async Task LoadLeagueAliasesAsync(
        SqlConnection db,
        ConcurrentDictionary<SportType, ConcurrentDictionary<string, MatchData>> cache)
    {
        var sql = @"
            SELECT 
                s.lid AS LeagueId,
                l.CatID,
                s.MapName AS SourceName,
                ISNULL(l.ModID, 0) AS ModId
            FROM tblSubLeague s WITH(NOLOCK)
            INNER JOIN tblLeague l WITH(NOLOCK) ON s.lid = l.id
            WHERE s.MapSite = @Source 
              AND l.Status = 1
            ORDER BY s.id DESC";

        var aliases = await db.QueryAsync<(int LeagueId, int CatId, string SourceName, int ModId)>(
            sql, new { Source = SourceCode });

        var count = 0;
        foreach (var alias in aliases)
        {
            AddToCache(cache, alias.CatId, alias.SourceName, alias.LeagueId, alias.ModId);
            count++;
        }

        _logger.LogDebug("從 tblSubLeague 載入 GS 別名: {Count} 筆", count);
    }

    /// <summary>
    /// 從 tblTeam 主表載入球隊對點資料
    /// </summary>
    private async Task LoadTeamsFromMainTableAsync(
        SqlConnection db,
        ConcurrentDictionary<SportType, ConcurrentDictionary<string, MatchData>> cache)
    {
        // 從 TeamName_GS 欄位載入 (GS 專用)
        var sql = @"
            SELECT 
                id AS TeamId,
                CatID,
                TeamName_GS AS SourceName
            FROM tblTeam WITH(NOLOCK)
            WHERE Status = 1 
              AND TeamName_GS IS NOT NULL 
              AND TeamName_GS <> ''
            ORDER BY id DESC";

        var teams = await db.QueryAsync<(int TeamId, int CatId, string SourceName)>(sql);

        var count = 0;
        foreach (var team in teams)
        {
            AddToCache(cache, team.CatId, team.SourceName, team.TeamId, 0);
            count++;
        }

        _logger.LogDebug("從 tblTeam.TeamName_GS 載入: {Count} 筆", count);
    }

    /// <summary>
    /// 從 tblSubTeam 別名表載入球隊別名
    /// </summary>
    private async Task LoadTeamAliasesAsync(
        SqlConnection db,
        ConcurrentDictionary<SportType, ConcurrentDictionary<string, MatchData>> cache)
    {
        var sql = @"
            SELECT 
                s.tid AS TeamId,
                t.CatID,
                s.MapName AS SourceName
            FROM tblSubTeam s WITH(NOLOCK)
            INNER JOIN tblTeam t WITH(NOLOCK) ON s.tid = t.id
            WHERE s.MapSite = @Source 
              AND t.Status = 1
            ORDER BY s.id ASC";

        var aliases = await db.QueryAsync<(int TeamId, int CatId, string SourceName)>(
            sql, new { Source = SourceCode });

        var count = 0;
        foreach (var alias in aliases)
        {
            AddToCache(cache, alias.CatId, alias.SourceName, alias.TeamId, 0);
            count++;
        }

        _logger.LogDebug("從 tblSubTeam 載入 GS 別名: {Count} 筆", count);
    }

    /// <summary>
    /// 加入快取
    /// </summary>
    private void AddToCache(
        ConcurrentDictionary<SportType, ConcurrentDictionary<string, MatchData>> cache,
        int catId, string sourceName, int id, int modId)
    {
        if (string.IsNullOrWhiteSpace(sourceName))
            return;

        if (!Enum.IsDefined(typeof(SportType), catId))
        {
            _logger.LogDebug("跳過未定義的 CatID: {CatId}, Name={Name}", catId, sourceName);
            return;
        }

        var sportType = (SportType)catId;
        var normalized = NameNormalizer.Normalize(sourceName);

        if (string.IsNullOrEmpty(normalized))
            return;

        var dict = cache.GetOrAdd(sportType, _ => new ConcurrentDictionary<string, MatchData>());

        // 只保留第一個（先到先得）
        if (dict.TryAdd(normalized, new MatchData
        {
            Id = id,
            Name = sourceName,
            ModId = modId,
            CatId = catId  // 保存原始 CatID，用於反向對點和建立賽事
        }))
        {
            _logger.LogDebug("快取已加入: CatID={CatId}, SportType={SportType}, Name={Name}, Normalized={Normalized}",
                catId, sportType, sourceName, normalized);
        }
    }

    #endregion
}
