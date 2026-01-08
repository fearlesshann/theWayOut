using ConcurrencyDemo;
using Microsoft.Data.SqlClient;
using Prometheus;
using Serilog;
using StackExchange.Redis;
using RabbitMQ.Client;

var builder = WebApplication.CreateBuilder(args);

// =================================================================================
// 1. 日志与基础设施配置
// =================================================================================

// 配置 Serilog
// 关键优化：将 MinimumLevel 设为 Warning 以减少控制台 IO，这在高并发场景下是巨大的性能瓶颈。
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration) // 从 appsettings.json 读取配置
    .WriteTo.Console()
    .CreateLogger();
builder.Host.UseSerilog();

// Redis 连接配置
// 优先从 Configuration 读取，支持环境变量覆盖 (ConnectionStrings__Redis)
var redisConnStr = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379,abortConnect=false";

Log.Information("正在连接 Redis: {RedisConnStr}", redisConnStr);

// =================================================================================
// 2. 依赖注入 (DI) 注册
// =================================================================================

// 注册 Redis 多路复用器 (单例)
builder.Services.AddSingleton<IConnectionMultiplexer>(sp => 
    ConnectionMultiplexer.Connect(redisConnStr));

// 注册 RabbitMQ 连接 (单例)
builder.Services.AddSingleton<IConnection>(sp => 
{
    var config = builder.Configuration.GetSection("RabbitMQ");
    var factory = new ConnectionFactory() 
    { 
        HostName = config["HostName"] ?? "rabbitmq", 
        Port = int.Parse(config["Port"] ?? "5672"),
        UserName = config["UserName"] ?? "guest",
        Password = config["Password"] ?? "guest",
        DispatchConsumersAsync = true // 允许异步消费者
    };
    return factory.CreateConnection();
});

// 注册 IDatabase (RedisLuaStock 依赖此接口)
builder.Services.AddSingleton<IDatabase>(sp => 
    sp.GetRequiredService<IConnectionMultiplexer>().GetDatabase());

// 注册 Health Checks
// 添加 Redis 健康检查，Kubernetes 或 Docker Compose 会利用此端点判断服务是否就绪
builder.Services.AddHealthChecks()
    .AddRedis(redisConnStr, name: "redis", tags: new[] { "ready" });

// 数据库连接字符串 (SQL Server)
var dbConnStr = builder.Configuration.GetConnectionString("SqlServer") 
    ?? "Server=localhost;Database=StockDb;User Id=sa;Password=YourStrong@Password;TrustServerCertificate=True;";
    
InitializeDatabase(dbConnStr); // 应用启动时初始化 SQL Server 表结构和数据

// 注册核心业务服务
// StockSyncService 既作为单例被注入(用于 Enqueue)，又作为后台服务运行(用于 Consume)
builder.Services.AddSingleton(sp => new StockSyncService(
    dbConnStr, 
    sp.GetRequiredService<IConnectionMultiplexer>(), 
    sp.GetRequiredService<IConnection>(), // 注入 RabbitMQ 连接
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

// 增加库存 API
app.MapPost("/api/add", async (RedisLuaStock stockService, StockSyncService syncService) =>
{
    // 硬编码 ID=1, Qty=1 用于测试
    int id = 1;
    int qty = 1;

    // 1. Redis 增加库存
    await stockService.AddStockAsync(qty);
    
    // 2. 异步队列同步 (传入负数表示反向扣减 = 增加)
    string tid = Guid.NewGuid().ToString("N");
    syncService.Enqueue(id, -qty, tid);
    
    return Results.Ok(new { success = true, tid });
});

// Redis 预热逻辑：将数据库中的库存同步到 Redis
// 必须在 Run() 之前执行，确保流量进来前缓存已就绪
using (var scope = app.Services.CreateScope())
{
    try 
    {
        var redisStock = scope.ServiceProvider.GetRequiredService<RedisLuaStock>();
        // 这里硬编码 100000 与 InitializeDatabase 保持一致
        // 生产环境应从数据库读取真实库存: var qty = dbContext.Stocks.Find(1).Qty;
        await redisStock.InitializeStockAsync(100000); 
        Log.Information("Redis 库存预热完成: Key=material:stock:1, Qty=100000");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Redis 库存预热失败");
    }
}

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
/// 初始化 SQL Server 数据库。
/// </summary>
void InitializeDatabase(string connStr)
{
    int retries = 20; // 增加重试次数，SQL Server 启动较慢
    while (retries > 0)
    {
        try 
        {
            // 1. 解析连接字符串，连接 master 创建数据库
            var builder = new SqlConnectionStringBuilder(connStr);
            string dbName = builder.InitialCatalog;
            builder.InitialCatalog = "master"; // 先连 master
            
            using (var masterConn = new SqlConnection(builder.ConnectionString))
            {
                masterConn.Open();
                var cmd = masterConn.CreateCommand();
                cmd.CommandText = $"IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'{dbName}') CREATE DATABASE [{dbName}]";
                cmd.ExecuteNonQuery();
            }

            // 2. 连接业务数据库创建表
            using var connection = new SqlConnection(connStr);
            connection.Open();
            var command = connection.CreateCommand();
            
            command.CommandText = @"
                IF OBJECT_ID('dbo.MaterialStock', 'U') IS NOT NULL DROP TABLE dbo.MaterialStock;
                CREATE TABLE dbo.MaterialStock (Id INT PRIMARY KEY, Qty INT);
                
                IF OBJECT_ID('dbo.ProcessedTransactions', 'U') IS NOT NULL DROP TABLE dbo.ProcessedTransactions;
                CREATE TABLE dbo.ProcessedTransactions (TransactionId NVARCHAR(50) PRIMARY KEY);
                
                INSERT INTO dbo.MaterialStock (Id, Qty) VALUES (1, 100000);
            ";
            command.ExecuteNonQuery();
            Log.Information("SQL Server 数据库初始化完成");
            return;
        }
        catch (SqlException ex)
        {
            retries--;
            Log.Warning("SQL Server 连接失败，正在重试 ({Retries})... 错误: {Message}", retries, ex.Message);
            Thread.Sleep(3000);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "SQL Server 初始化发生未知错误");
            throw;
        }
    }
    throw new Exception("无法连接到 SQL Server，初始化失败");
}
