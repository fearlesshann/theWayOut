using AspNetCore_Learning.Data;
using AspNetCore_Learning.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AspNetCore_Learning.Controllers;

[ApiController]
[Route("[controller]")]
public class ConnectionTestController : ControllerBase
{
    private readonly WeatherContext _context;
    private readonly AnotherWeatherService _anotherService;
    private readonly ILogger<ConnectionTestController> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public ConnectionTestController(
        WeatherContext context, 
        AnotherWeatherService anotherService,
        ILogger<ConnectionTestController> logger,
        IServiceScopeFactory scopeFactory)
    {
        _context = context;
        _anotherService = anotherService;
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    [HttpGet("inspect")]
    public IActionResult InspectContext()
    {
        // 1. 模拟加载一个实体 (此时它会被 Context 追踪)
        var forecast = _context.Forecasts.FirstOrDefault();
        if (forecast != null)
        {
            forecast.Summary = "Modified by Inspect"; // 修改它，让 ChangeTracker 感知
        }

        // 2. 模拟添加一个实体
        _context.Forecasts.Add(new Models.WeatherForecast { Date = DateOnly.FromDateTime(DateTime.Now), TemperatureC = 20, Summary = "New One" });

        // 3. 获取 Context 内部信息
        var info = new
        {
            ContextId = _context.ContextId.ToString(),
            
            // A. ChangeTracker (变更追踪器) - 這是 DbContext 最“重”的部分
            // 它维护着当前内存中所有实体的状态副本 (Original Values vs Current Values)
            TrackedEntities = _context.ChangeTracker.Entries().Select(e => new 
            {
                Entity = e.Entity.GetType().Name,
                State = e.State.ToString(), // Unchanged, Modified, Added, Deleted
                IsDirty = e.State != EntityState.Unchanged
            }),

            // B. Database (数据库交互入口)
            DatabaseProvider = _context.Database.ProviderName, // 如 Microsoft.EntityFrameworkCore.Sqlite
            
            // C. Model (元数据模型) - 缓存的数据库结构定义
            DefinedTables = _context.Model.GetEntityTypes().Select(t => t.GetTableName())
        };

        return Ok(new 
        { 
            Summary = "DbContext 主要包含三个核心组件：",
            Components = new Dictionary<string, string>
            {
                { "1. ChangeTracker (变更追踪器)", "最核心部分。它记录了你查出来的所有对象。当你修改对象属性时，它会对比原始值，知道哪些字段变了，以便生成 UPDATE 语句。" },
                { "2. Database Facade (数据库门面)", "负责管理物理连接、事务、执行原生 SQL。" },
                { "3. Model (模型元数据)", "一张详细的地图，记录了 C# 类和数据库表是如何映射的。" }
            },
            RealTimeData = info
        });
    }
}
