using System.Data;
using GS.Acqu.Application.Interfaces;
using GS.Acqu.Domain.Interfaces;
using GS.Acqu.Infrastructure.Data;
using GS.Acqu.Infrastructure.Repositories;
using GS.Acqu.Infrastructure.Services;
using Microsoft.Data.SqlClient;

var builder = WebApplication.CreateBuilder(args);

// 設定選項
var dbSection = builder.Configuration.GetSection(DatabaseOptions.SectionName);
var connectionString = dbSection.GetValue<string>("ConnectionString");

if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.WriteLine($"警告: Database:ConnectionString 未設定或為空");
    Console.WriteLine($"可用的設定區段: {string.Join(", ", builder.Configuration.GetChildren().Select(c => c.Key))}");
}
else
{
    Console.WriteLine($"資料庫連線字串已載入 (長度: {connectionString.Length})");
}

builder.Services.Configure<DatabaseOptions>(dbSection);

// 資料庫連線
builder.Services.AddScoped<IDbConnection>(sp =>
{
    var connectionString = builder.Configuration
        .GetSection(DatabaseOptions.SectionName)
        .GetValue<string>("ConnectionString");

    return new SqlConnection(connectionString);
});

// 快取服務
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ICacheService, MemoryCacheService>();

// 核心服務 (Application 層)
builder.Services.AddSingleton<IMatcherService, MatcherService>();
builder.Services.AddSingleton<IUnmatchedEventStore, UnmatchedEventStore>();

// 儲存庫 (Infrastructure 層)
builder.Services.AddScoped<IMatchRepository, MatchRepository>();
builder.Services.AddScoped<IMarketRepository, MarketRepository>();
builder.Services.AddScoped<IResultRepository, ResultRepository>();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "GS ACQU API",
        Version = "v1",
        Description = "Genius Sports 資料採集服務 API"
    });
    
    // 載入 XML 註解
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
});

builder.Services.AddControllers();

var app = builder.Build();

// 初始化對點快取 (允許失敗，服務仍可運行)
try
{
    using var scope = app.Services.CreateScope();
    var matcher = scope.ServiceProvider.GetRequiredService<IMatcherService>();
    await matcher.RefreshCacheAsync();
    app.Logger.LogInformation("對點快取初始化完成");
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "對點快取初始化失敗，服務將在無快取模式下啟動");
}

// Swagger UI (所有環境都啟用)
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "GS ACQU API v1");
    c.RoutePrefix = "swagger";
});

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
