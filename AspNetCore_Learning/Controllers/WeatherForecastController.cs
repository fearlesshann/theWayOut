using Microsoft.AspNetCore.Mvc;
using AspNetCore_Learning.Models;
using AspNetCore_Learning.Services;

namespace AspNetCore_Learning.Controllers;

[ApiController]
[Route("[controller]")]
public class WeatherForecastController : ControllerBase
{
    private readonly ILogger<WeatherForecastController> _logger;
    private readonly IWeatherService _weatherService; // 声明依赖

    // 构造函数注入：请求 IWeatherService
    public WeatherForecastController(
        ILogger<WeatherForecastController> logger,
        IWeatherService weatherService)
    {
        _logger = logger;
        _weatherService = weatherService;
    }

    [HttpGet(Name = "GetWeatherForecast")]
    public IEnumerable<WeatherForecast> Get()
    {
        _logger.LogInformation("正在调用 WeatherService 获取天气数据...");
        // 委托给 Service 处理
        return _weatherService.GetForecasts();
    }

    [HttpPost]
    public IActionResult Create([FromBody] CreateWeatherForecastDto dto)
    {
        _logger.LogInformation("接收到添加天气请求: {Date}, {Temp}C", dto.Date, dto.TemperatureC);
        
        _weatherService.AddForecast(dto);
        
        // 返回 201 Created，并在响应头中包含获取资源的 URL (虽然我们这里列表页没有 ID，暂时指向列表页)
        return CreatedAtRoute("GetWeatherForecast", null, dto);
    }
}
