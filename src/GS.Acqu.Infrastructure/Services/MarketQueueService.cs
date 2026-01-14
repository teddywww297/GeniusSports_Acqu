using System.Collections.Concurrent;
using System.Data;
using System.Diagnostics;
using Dapper;
using GS.Acqu.Domain.Entities;
using GS.Acqu.Domain.Interfaces;
using GS.Acqu.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GS.Acqu.Infrastructure.Services;

/// <summary>
/// 盤口批次隊列服務實作
/// 使用現有 GDetailType TVP 和 usp_ctl_UpdateDetail SP
/// </summary>
public class MarketQueueService : IMarketQueueService
{
    private readonly DatabaseOptions _dbOptions;
    private readonly ILogger<MarketQueueService> _logger;

    // 盤口更新隊列 (用於 TVP 批次更新)
    private readonly ConcurrentQueue<MarketDetailCache> _updateQueue = new();
    
    // SQL 執行隊列 (用於 INSERT、拉盤等)
    private readonly ConcurrentQueue<string> _sqlQueue = new();

    // 背景任務控制
    private CancellationTokenSource? _cts;
    private Task? _updateTask;
    private Task? _sqlTask;

    // 配置參數
    private const int BatchSize = 300;        // 批次大小
    private const int PollIntervalMs = 5;     // 輪詢間隔 (毫秒)

    public MarketQueueService(
        IOptions<DatabaseOptions> dbOptions,
        ILogger<MarketQueueService> logger)
    {
        _dbOptions = dbOptions.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public void EnqueueUpdate(MarketDetailCache market)
    {
        _updateQueue.Enqueue(market);
    }

    /// <inheritdoc />
    public void EnqueueSql(string sql)
    {
        _sqlQueue.Enqueue(sql);
    }

    /// <inheritdoc />
    public int UpdateQueueCount => _updateQueue.Count;

    /// <inheritdoc />
    public int SqlQueueCount => _sqlQueue.Count;

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("啟動盤口批次處理服務");
        
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // 啟動 TVP 批次更新執行緒
        _updateTask = Task.Run(() => ProcessUpdateQueueAsync(_cts.Token), _cts.Token);

        // 啟動 SQL 執行執行緒
        _sqlTask = Task.Run(() => ProcessSqlQueueAsync(_cts.Token), _cts.Token);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("停止盤口批次處理服務，剩餘 UpdateQueue={UpdateCount}, SqlQueue={SqlCount}",
            _updateQueue.Count, _sqlQueue.Count);

        _cts?.Cancel();

        if (_updateTask != null && _sqlTask != null)
        {
            await Task.WhenAll(_updateTask, _sqlTask).ConfigureAwait(false);
        }

        _logger.LogInformation("盤口批次處理服務已停止");
    }

    /// <summary>
    /// 處理 TVP 批次更新隊列
    /// </summary>
    private async Task ProcessUpdateQueueAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("TVP 批次更新執行緒啟動");

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (_updateQueue.Count > 0)
                {
                    var dataTable = CreateGDetailTypeTable();
                    var count = 0;

                    // 批次取出 (最多 BatchSize 筆)
                    while (count < BatchSize && _updateQueue.TryDequeue(out var market))
                    {
                        AddRowToGDetailTable(dataTable, market);
                        count++;
                    }

                    if (count > 0)
                    {
                        var sw = Stopwatch.StartNew();
                        await ExecuteBulkUpdateAsync(dataTable);
                        sw.Stop();

                        _logger.LogDebug("批次更新完成: 數量={Count}, 耗時={ElapsedMs}ms",
                            count, sw.ElapsedMilliseconds);
                    }
                }
                else
                {
                    await Task.Delay(PollIntervalMs, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TVP 批次更新發生錯誤");
                await Task.Delay(100, cancellationToken); // 錯誤後稍等再重試
            }
        }

        _logger.LogDebug("TVP 批次更新執行緒結束");
    }

    /// <summary>
    /// 處理 SQL 執行隊列
    /// </summary>
    private async Task ProcessSqlQueueAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("SQL 執行執行緒啟動");
        var sqlList = new List<string>();

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // 條件：累積 BatchSize 筆 或 隊列已空
                if (sqlList.Count >= BatchSize || (_sqlQueue.IsEmpty && sqlList.Count > 0))
                {
                    var sw = Stopwatch.StartNew();
                    await ExecuteBatchSqlAsync(sqlList);
                    sw.Stop();

                    _logger.LogDebug("批次執行 SQL 完成: 數量={Count}, 耗時={ElapsedMs}ms",
                        sqlList.Count, sw.ElapsedMilliseconds);

                    sqlList.Clear();
                }
                else if (_sqlQueue.TryDequeue(out var sql))
                {
                    sqlList.Add(sql);
                }
                else
                {
                    await Task.Delay(PollIntervalMs, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SQL 批次執行發生錯誤");
                sqlList.Clear(); // 清除失敗的批次
                await Task.Delay(100, cancellationToken);
            }
        }

        _logger.LogDebug("SQL 執行執行緒結束");
    }

    /// <summary>
    /// 建立 GDetailType DataTable (對應現有 TVP)
    /// </summary>
    private static DataTable CreateGDetailTypeTable()
    {
        var dt = new DataTable();
        dt.Columns.Add("AcquUniqID", typeof(int));
        dt.Columns.Add("HomeHdp", typeof(string));
        dt.Columns.Add("AwayHdp", typeof(string));
        dt.Columns.Add("HdpPos", typeof(byte));
        dt.Columns.Add("HomeHdpOdds", typeof(decimal));
        dt.Columns.Add("AwayHdpOdds", typeof(decimal));
        dt.Columns.Add("OULine", typeof(string));
        dt.Columns.Add("OverOdds", typeof(decimal));
        dt.Columns.Add("UnderOdds", typeof(decimal));
        dt.Columns.Add("HomeOdds", typeof(decimal));
        dt.Columns.Add("DrewOdds", typeof(decimal));
        dt.Columns.Add("AwayOdds", typeof(decimal));
        dt.Columns.Add("HalfType", typeof(byte));
        dt.Columns.Add("WagerGrpID", typeof(short));
        dt.Columns.Add("Status", typeof(short));
        dt.Columns.Add("OptPer", typeof(short));
        dt.Columns.Add("Position", typeof(byte));
        dt.Columns.Add("Change", typeof(byte));
        dt.Columns.Add("CalOdds1", typeof(decimal));
        dt.Columns.Add("CalOdds2", typeof(decimal));
        dt.Columns.Add("CalOdds3", typeof(decimal));
        dt.Columns.Add("Margin", typeof(decimal));
        dt.Columns.Add("MarginCode", typeof(string));
        return dt;
    }

    /// <summary>
    /// 將盤口資料加入 DataTable
    /// </summary>
    private static void AddRowToGDetailTable(DataTable dt, MarketDetailCache market)
    {
        dt.Rows.Add(
            market.AcquUniqId,
            market.HomeHdp ?? "-",
            market.AwayHdp ?? "-",
            market.HdpPos,
            market.HomeHdpOdds,
            market.AwayHdpOdds,
            market.OULine ?? "-",
            market.OverOdds,
            market.UnderOdds,
            market.HomeOdds,
            market.DrawOdds,
            market.AwayOdds,
            market.HalfType,
            market.WagerGrpId,
            market.Status,
            market.OptPer,
            market.Position,
            market.Change,
            market.CalOdds1,
            market.CalOdds2,
            market.CalOdds3,
            market.Margin,
            market.MarginCode ?? ""
        );
    }

    /// <summary>
    /// 執行 TVP 批次更新 (呼叫現有 usp_ctl_UpdateDetail SP)
    /// </summary>
    private async Task ExecuteBulkUpdateAsync(DataTable dataTable)
    {
        await using var conn = new SqlConnection(_dbOptions.ConnectionString);
        await conn.OpenAsync();

        await using var cmd = new SqlCommand("usp_ctl_UpdateDetail", conn);
        cmd.CommandType = CommandType.StoredProcedure;

        // TVP 參數 (使用現有 GDetailType)
        var tvpParam = new SqlParameter
        {
            ParameterName = "@dTable",
            SqlDbType = SqlDbType.Structured,
            TypeName = "dbo.GDetailType",
            Value = dataTable
        };
        cmd.Parameters.Add(tvpParam);

        // 資料表參數 (1=tblGameDataDetail, 2=tblGameDataDetailSC)
        cmd.Parameters.AddWithValue("@table", 1);

        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// 批次執行 SQL
    /// </summary>
    private async Task ExecuteBatchSqlAsync(List<string> sqlList)
    {
        if (sqlList.Count == 0) return;

        var batchSql = string.Join(Environment.NewLine, sqlList);

        await using var conn = new SqlConnection(_dbOptions.ConnectionString);
        await conn.OpenAsync();
        await conn.ExecuteAsync(batchSql);
    }
}
