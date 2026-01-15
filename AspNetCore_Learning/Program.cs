using AspNetCore_Learning.Data;
using AspNetCore_Learning.Filters;
using AspNetCore_Learning.Middleware;
using AspNetCore_Learning.Models;
using AspNetCore_Learning.Repositories;
using AspNetCore_Learning.Services;
using MassTransit;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers(options =>
{
    // 注册全局异常过滤器
    options.Filters.Add<GlobalExceptionFilter>();
}); // 1. 注册 Controller 服务

// 注册 DbContext (SQLite)
builder.Services.AddDbContext<WeatherContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("WeatherDb")));

// 注册配置模式 (Options Pattern)
// 这行代码会自动读取 appsettings.json 中的 "WeatherSettings" 节点
builder.Services.Configure<WeatherSettings>(builder.Configuration.GetSection("WeatherSettings"));

// 注册 MassTransit
builder.Services.AddMassTransit(x =>
{
    // 1. 注册消费者
    x.AddConsumer<WeatherUpdateConsumer>();

    // 2. 配置总线 (这里使用内存模式 In-Memory，生产环境可改为 UsingRabbitMq)
    x.UsingInMemory((context, cfg) =>
    {
        cfg.ConfigureEndpoints(context);
    });
});

builder.Services.AddScoped<IWeatherService, WeatherService>(); // 注册自定义服务
builder.Services.AddScoped<AnotherWeatherService>(); // 注册用于测试连接的服务

// 注册仓储和服务
builder.Services.AddScoped<IWeatherForecastRepository, WeatherForecastRepository>();
builder.Services.AddScoped<RepositoryDemoService>();

// 注册后台任务
// builder.Services.AddHostedService<WeatherBackgroundWorker>();

builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

// Configure the HTTP request pipeline.

// 注册自定义中间件 (放在最前面，以便监控所有请求)
app.UseMiddleware<RequestTimingMiddleware>();

// 鉴权中间件
app.UseMiddleware<ApiKeyMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapControllers(); // 2. 映射 Controller 路由

app.Run();
 