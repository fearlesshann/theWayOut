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
}
