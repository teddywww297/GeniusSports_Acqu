using System.Data;
using GS.Acqu.Application.Interfaces;
using GS.Acqu.Application.UseCases;
using GS.Acqu.Domain.Entities;
using GS.Acqu.Domain.Interfaces;
using GS.Acqu.Infrastructure.Data;
using GS.Acqu.Infrastructure.Repositories;
using GS.Acqu.Infrastructure.Services;
using GS.Acqu.Worker;
using GS.Acqu.Worker.Channels;
using GS.Acqu.Worker.Clients;
using GS.Acqu.Worker.Options;
using GS.Acqu.Worker.Processors;
using GS.Acqu.Worker.Services;
using GS.Acqu.Worker.Workers;
using Microsoft.Data.SqlClient;

var builder = WebApplication.CreateBuilder(args);

// ========== 設定選項 ==========
builder.Services.Configure<DatabaseOptions>(
    builder.Configuration.GetSection(DatabaseOptions.SectionName));

builder.Services.Configure<GeniusSportsOptions>(
    builder.Configuration.GetSection(GeniusSportsOptions.SectionName));

// ========== 資料庫連線 ==========
builder.Services.AddScoped<IDbConnection>(sp =>
{
    var connectionString = builder.Configuration
        .GetSection(DatabaseOptions.SectionName)
        .GetValue<string>("ConnectionString");
    
    return new SqlConnection(connectionString);
});

// ========== 快取服務 (Singleton - 共用記憶體) ==========
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ICacheService, MemoryCacheService>();

// 盤口快取服務 (ConcurrentDictionary)
builder.Services.AddSingleton<IMarketCacheService, MarketCacheService>();

// 盤口批次隊列服務 (背景 TVP 處理)
builder.Services.AddSingleton<IMarketQueueService, MarketQueueService>();

// ⭐ 未對點快取服務 (Singleton - 方案 B 共用記憶體核心)
builder.Services.AddSingleton<IUnmatchedEventStore, UnmatchedEventStore>();

// ⭐ 賽事資訊快取服務 (Singleton - 補齊串流資料的名稱)
builder.Services.AddSingleton<IMatchInfoCacheService, MatchInfoCacheService>();

// ========== 資料 Channel (方案 E: 背壓控制) ==========
// 盤口 Channel (容量 50,000，滿載丟棄最舊)
builder.Services.AddSingleton<IDataChannel<MarketInfo>, MarketDataChannel>();

// 賽事 Channel (容量 10,000)
builder.Services.AddSingleton<IDataChannel<MatchInfo>, MatchDataChannel>();

// ========== 核心服務 (Application 層) ==========
builder.Services.AddSingleton<IMatcherService, MatcherService>();

// ========== 儲存庫 (Infrastructure 層) ==========
builder.Services.AddScoped<IMatchRepository, MatchRepository>();
builder.Services.AddScoped<IMarketRepository, MarketRepository>();
builder.Services.AddScoped<IResultRepository, ResultRepository>();

// ========== 讀取 GeniusSports 設定 ==========
var geniusSportsConfig = builder.Configuration.GetSection(GeniusSportsOptions.SectionName);
var autoCreateEvent = geniusSportsConfig.GetValue<bool>("AutoCreateEvent", false);

// ========== 訊息處理器 (Application 層 UseCases) ==========
builder.Services.AddScoped<IMatchInfoHandler>(sp =>
{
    return new MatchInfoHandler(
        sp.GetRequiredService<IMatcherService>(),
        sp.GetRequiredService<IMatchRepository>(),
        sp.GetRequiredService<IUnmatchedEventStore>(),
        sp.GetRequiredService<ILogger<MatchInfoHandler>>(),
        autoCreateEvent);
});
builder.Services.AddScoped<IMarketOddsHandler, MarketOddsHandler>();
builder.Services.AddScoped<IMatchResultHandler, MatchResultHandler>();

// ========== Genius Sports gRPC Client ==========
builder.Services.AddSingleton<IGeniusSportsClient, GeniusSportsClient>();

// ========== 賽事 REST API Client (用於建立快取) ==========
builder.Services.AddHttpClient<IMatchesApiClient, MatchesApiClient>(client =>
{
    client.BaseAddress = new Uri("https://admin-uat.supersignal.info");
    client.Timeout = TimeSpan.FromSeconds(30);
});

// ========== gRPC 服務 (本地 Server) ==========
builder.Services.AddGrpc();

// ⭐ ========== REST API 服務 (方案 B: 同一 Process 內共用記憶體) ==========
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "GS ACQU API",
        Version = "v1",
        Description = "Genius Sports 資料採集服務 API (gRPC + REST)"
    });
});

// ========== 背景服務 ==========
// 定時任務：對點快取刷新、過期清理
builder.Services.AddHostedService<Worker>();

// Genius Sports 資料訂閱服務 (Producer: gRPC → Channel)
builder.Services.AddHostedService<GeniusSportsSubscriber>();

// 批次處理器 (Consumer: Channel → Handler)
builder.Services.AddHostedService<MarketBatchProcessor>();
builder.Services.AddHostedService<MatchBatchProcessor>();

// ⭐ ========== 配置 Kestrel (雙協定: gRPC + REST API) ==========
// Port 規則: BasePort + CatID (例如 CatID=3 → gRPC=40003, API=41003)
var catId = builder.Configuration.GetValue<int>("CatID", 3);
var grpcBasePort = builder.Configuration.GetValue<int>("Grpc:BasePort", 40000);
var apiBasePort = builder.Configuration.GetValue<int>("Api:BasePort", 40000);
var grpcPort = grpcBasePort + catId;
var apiPort = apiBasePort + 1000 + catId;  // API 用 41000+CatID 避免衝突

builder.WebHost.ConfigureKestrel(options =>
{
    // gRPC Server (HTTP/2) - Port: 40000 + CatID
    options.ListenAnyIP(grpcPort, listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2;
    });

    // REST API Server (HTTP/1.1) - Port: 41000 + CatID
    options.ListenAnyIP(apiPort, listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1;
    });
});

var app = builder.Build();

// ========== 初始化 ==========

var logger = app.Services.GetRequiredService<ILogger<Program>>();

logger.LogInformation("GS ACQU 服務啟動中... (CatID={CatID})", catId);
logger.LogInformation("  - gRPC Server: Port {GrpcPort}", grpcPort);
logger.LogInformation("  - REST API Server: Port {ApiPort}", apiPort);

// 初始化對點快取 (可選，資料庫不可用時跳過)
try
{
    using var scope = app.Services.CreateScope();
    var matcher = scope.ServiceProvider.GetRequiredService<IMatcherService>();
    await matcher.RefreshCacheAsync();
    logger.LogInformation("對點快取初始化完成");
}
catch (Exception ex)
{
    logger.LogWarning(ex, "對點快取初始化失敗，服務將在無快取模式下啟動 (僅 gRPC 訂閱功能可用)");
}

// 啟動盤口批次隊列服務 (可選)
var queueService = app.Services.GetRequiredService<IMarketQueueService>();
var queueServiceStarted = false;

try
{
    await queueService.StartAsync(CancellationToken.None);
    queueServiceStarted = true;
    logger.LogInformation("盤口批次隊列服務已啟動");
}
catch (Exception ex)
{
    logger.LogWarning(ex, "盤口批次隊列服務啟動失敗，批次寫入功能不可用");
}

// 無論啟動是否成功，都註冊關閉處理程序以確保資源清理
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStopping.Register(() =>
{
    try
    {
        queueService.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
        if (queueServiceStarted)
        {
            logger.LogInformation("盤口批次隊列服務已停止");
        }
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "盤口批次隊列服務停止時發生錯誤");
    }
});

// ========== 端點映射 ==========

// ⭐ Swagger UI (所有環境都啟用)
app.UseSwagger();
app.UseSwaggerUI();

// ⭐ REST API 控制器路由
app.MapControllers();

// 映射本地 gRPC 服務端點
app.MapGrpcService<AcquGrpcService>();

// 健康檢查端點
app.MapGet("/", () => $"GS ACQU Service is running. CatID: {catId}, gRPC: Port {grpcPort}, REST API: Port {apiPort}");
app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", CatID = catId, GrpcPort = grpcPort, ApiPort = apiPort, Timestamp = DateTime.Now }));

// Channel 統計端點
app.MapGet("/stats/channels", (
    IDataChannel<MarketInfo> marketChannel,
    IDataChannel<MatchInfo> matchChannel) =>
{
    return Results.Ok(new
    {
        Market = marketChannel.GetStats(),
        Match = matchChannel.GetStats()
    });
});

app.Run();
