using AspNetCore_Learning;
using AspNetCore_Learning.Data;
using AspNetCore_Learning.Models;
using AspNetCore_Learning.Services;
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

builder.Services.AddScoped<IWeatherService, WeatherService>(); // 注册自定义服务
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
 