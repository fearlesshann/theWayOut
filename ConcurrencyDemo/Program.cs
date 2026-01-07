using ConcurrencyDemo;
using Microsoft.Data.Sqlite;
using Prometheus;
using Serilog;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// 1. Serilog 配置
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Warning() // 性能优化：仅输出 Warning 及以上级别日志，避免 Console IO 瓶颈
    .WriteTo.Console()
    .CreateLogger();
builder.Host.UseSerilog();

// 2. Redis 连接配置
// 支持通过环境变量覆盖 Redis 地址 (Docker Compose 中使用 'redis')
var redisHost = Environment.GetEnvironmentVariable("REDIS_HOST") ?? "localhost";
var redisConnStr = $"{redisHost}:6379,abortConnect=false"; // abortConnect=false 允许在 Redis 未就绪时启动应用

Log.Information("正在连接 Redis: {RedisConnStr}", redisConnStr);

// 注册 Redis 多路复用器
builder.Services.AddSingleton<IConnectionMultiplexer>(sp => 
    ConnectionMultiplexer.Connect(redisConnStr));

// 注册 IDatabase (RedisLuaStock 依赖此接口)
builder.Services.AddSingleton<IDatabase>(sp => 
    sp.GetRequiredService<IConnectionMultiplexer>().GetDatabase());

// 3. Health Checks
builder.Services.AddHealthChecks()
    .AddRedis(redisConnStr, name: "redis", tags: new[] { "ready" });

// 4. 业务服务注册
var dbConnStr = "Data Source=stock.db";
InitializeDatabase(dbConnStr); // 应用启动时初始化 SQLite

// StockSyncService 既作为单例被注入，又作为后台服务运行
builder.Services.AddSingleton(sp => new StockSyncService(
    dbConnStr, 
    sp.GetRequiredService<IConnectionMultiplexer>(), 
    sp.GetRequiredService<ILogger<StockSyncService>>()));

builder.Services.AddHostedService(sp => sp.GetRequiredService<StockSyncService>());

builder.Services.AddSingleton<RedisLuaStock>();

// 注册模拟服务 (用于产生流量)
builder.Services.AddHostedService<SimulationService>();

var app = builder.Build();

// 5. 中间件管道
app.UseRouting();
app.UseHttpMetrics(); // 开启 Prometheus HTTP 指标收集

app.MapMetrics(); // 暴露 /metrics 端点
app.MapHealthChecks("/health"); // 暴露 /health 端点

// 6. 业务 API (用于压测)
app.MapPost("/api/deduct", async (RedisLuaStock stockService, StockSyncService syncService) =>
{
    // 硬编码 ID=1, Qty=1 用于测试
    int id = 1;
    int qty = 1;

    // 调用 Lua 脚本扣减
    bool success = await stockService.DeductStockAsync(qty);
    if (success)
    {
        string tid = Guid.NewGuid().ToString("N");
        // 异步写入 DB
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

// 辅助方法：初始化数据库
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
