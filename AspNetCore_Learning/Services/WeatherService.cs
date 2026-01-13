using AspNetCore_Learning.Data;
using AspNetCore_Learning.Models;
using Microsoft.Extensions.Options;

namespace AspNetCore_Learning.Services;

public class WeatherService : IWeatherService
{
    private readonly WeatherSettings _settings;
    private readonly WeatherContext _context; // 注入 DbContext

    public WeatherService(IOptions<WeatherSettings> options, WeatherContext context)
    {
        _settings = options.Value;
        _context = context;
    }

    public IEnumerable<WeatherForecast> GetForecasts()
    {
        // 从数据库查询所有数据
        // 注意：EF Core 查询出来的对象是实体，如果要修改它（比如拼接城市名），
        // 最好先转成 List 或者 DTO，以免污染数据库上下文中的追踪状态（虽然这里只是读，没关系）
        var forecasts = _context.Forecasts.ToList();

        // 演示配置读取
        foreach (var f in forecasts)
        {
            if (f.Summary != null && !f.Summary.Contains(_settings.DefaultCity))
            {
                f.Summary += $" ({_settings.DefaultCity})";
            }
        }
        return forecasts;
    }

    public void AddForecast(CreateWeatherForecastDto dto)
    {
        // 校验逻辑
        if (dto.TemperatureC > _settings.MaxTemperature || dto.TemperatureC < _settings.MinTemperature)
        {
            if (dto.TemperatureC > _settings.MaxTemperature) dto.TemperatureC = _settings.MaxTemperature;
            if (dto.TemperatureC < _settings.MinTemperature) dto.TemperatureC = _settings.MinTemperature;
        }

        var forecast = new WeatherForecast(dto.Date, dto.TemperatureC, dto.Summary);
        
        // 写入数据库
        _context.Forecasts.Add(forecast);
        _context.SaveChanges(); // 必须调用，否则不会写入
    }
}
