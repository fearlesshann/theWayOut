using ConcurrencyDemo;
using Microsoft.Data.Sqlite;
using Prometheus;
using Serilog;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// =================================================================================
// 1. 日志与基础设施配置
// =================================================================================

// 配置 Serilog
// 关键优化：将 MinimumLevel 设为 Warning 以减少控制台 IO，这在高并发场景下是巨大的性能瓶颈。
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Warning() 
    .WriteTo.Console()
    .CreateLogger();
builder.Host.UseSerilog();

// Redis 连接配置
// 支持通过环境变量覆盖 Redis 地址 (Docker Compose 中使用 'redis'，本地使用 'localhost')
var redisHost = Environment.GetEnvironmentVariable("REDIS_HOST") ?? "localhost";
// abortConnect=false: 允许在 Redis 暂时不可用时启动应用（依赖 HealthCheck 确保最终可用）
var redisConnStr = $"{redisHost}:6379,abortConnect=false"; 

Log.Information("正在连接 Redis: {RedisConnStr}", redisConnStr);

// =================================================================================
// 2. 依赖注入 (DI) 注册
// =================================================================================

// 注册 Redis 多路复用器 (单例)
builder.Services.AddSingleton<IConnectionMultiplexer>(sp => 
    ConnectionMultiplexer.Connect(redisConnStr));

// 注册 IDatabase (RedisLuaStock 依赖此接口)
builder.Services.AddSingleton<IDatabase>(sp => 
    sp.GetRequiredService<IConnectionMultiplexer>().GetDatabase());

// 注册 Health Checks
// 添加 Redis 健康检查，Kubernetes 或 Docker Compose 会利用此端点判断服务是否就绪
builder.Services.AddHealthChecks()
    .AddRedis(redisConnStr, name: "redis", tags: new[] { "ready" });

// 数据库连接字符串 (SQLite)
var dbConnStr = "Data Source=stock.db";
InitializeDatabase(dbConnStr); // 应用启动时初始化 SQLite 表结构和数据

// 注册核心业务服务
// StockSyncService 既作为单例被注入(用于 Enqueue)，又作为后台服务运行(用于 Consume)
builder.Services.AddSingleton(sp => new StockSyncService(
    dbConnStr, 
    sp.GetRequiredService<IConnectionMultiplexer>(), 
    sp.GetRequiredService<ILogger<StockSyncService>>()));

builder.Services.AddHostedService(sp => sp.GetRequiredService<StockSyncService>());

builder.Services.AddSingleton<RedisLuaStock>();

// 注册模拟服务 (用于产生流量，可选)
builder.Services.AddHostedService<SimulationService>();

var app = builder.Build();

// =================================================================================
// 3. 中间件管道配置
// =================================================================================

app.UseRouting();
app.UseHttpMetrics(); // 开启 Prometheus HTTP 指标收集中间件

// 暴露监控端点
app.MapMetrics(); // Prometheus Scrape Endpoint: /metrics
app.MapHealthChecks("/health"); // Health Check Endpoint: /health

// 暴露业务 API (用于压测)
// 逻辑：Redis 原子扣减 -> 成功后异步写入 Channel -> 返回 200
app.MapPost("/api/deduct", async (RedisLuaStock stockService, StockSyncService syncService) =>
{
    // 硬编码 ID=1, Qty=1 用于测试
    int id = 1;
    int qty = 1;

    // 1. 调用 Lua 脚本进行 Redis 内存扣减 (原子性)
    bool success = await stockService.DeductStockAsync(qty);
    if (success)
    {
        string tid = Guid.NewGuid().ToString("N");
        // 2. 扣减成功后，生成流水号并放入异步队列 (最终一致性)
        syncService.Enqueue(id, qty, tid);
        return Results.Ok(new { success = true, tid });
    }
    return Results.BadRequest(new { success = false, message = "库存不足" });
});

try
{
    Log.Information("应用启动中...");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "应用启动失败");
}
finally
{
    Log.CloseAndFlush();
}

/// <summary>
/// 初始化 SQLite 数据库。
/// </summary>
void InitializeDatabase(string connStr)
{
    try 
    {
        using var connection = new SqliteConnection(connStr);
        connection.Open();
        var command = connection.CreateCommand();
        command.CommandText = "DROP TABLE IF EXISTS MaterialStock";
        command.ExecuteNonQuery();
        command.CommandText = "CREATE TABLE MaterialStock (Id INTEGER PRIMARY KEY, Qty INTEGER)";
        command.ExecuteNonQuery();
        // 性能测试：初始库存 100000
        command.CommandText = "INSERT INTO MaterialStock (Id, Qty) VALUES (1, 100000)";
        command.ExecuteNonQuery();
        Log.Information("SQLite 数据库初始化完成");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "SQLite 初始化失败");
    }
}
