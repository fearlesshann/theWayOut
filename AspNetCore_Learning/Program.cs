using AspNetCore_Learning;
using AspNetCore_Learning.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers(); // 1. 注册 Controller 服务
builder.Services.AddScoped<IWeatherService, WeatherService>(); // 注册自定义服务
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.

// 注册自定义中间件 (放在最前面，以便监控所有请求)
app.UseMiddleware<RequestTimingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapControllers(); // 2. 映射 Controller 路由

app.Run();
 