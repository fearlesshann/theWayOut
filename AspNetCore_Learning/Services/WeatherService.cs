using AspNetCore_Learning.Data;
using AspNetCore_Learning.Models;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AspNetCore_Learning.Services;

public class WeatherService : IWeatherService
{
    private readonly WeatherSettings _settings;
    private readonly WeatherContext _context;
    private readonly IPublishEndpoint _publishEndpoint; // MassTransit 发布者

    public WeatherService(IOptions<WeatherSettings> options, WeatherContext context, IPublishEndpoint publishEndpoint)
    {
        _settings = options.Value;
        _context = context;
        _publishEndpoint = publishEndpoint;
    }

    public PagedResult<WeatherForecast> GetForecasts(int page, int pageSize)
    {
        // 1. 先查总数
        var total = _context.Forecasts.Count();

        // 2. 分页查询
        var forecasts = _context.Forecasts
            .OrderByDescending(f => f.Date) // 通常按日期倒序
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        // 演示配置读取
        foreach (var f in forecasts)
        {
            if (f.Summary != null && !f.Summary.Contains(_settings.DefaultCity))
            {
                f.Summary += $" ({_settings.DefaultCity})";
            }
        }

        return new PagedResult<WeatherForecast>
        {
            Data = forecasts,
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async void AddForecast(CreateWeatherForecastDto dto)
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

        // 发送消息通知消费者
        // 注意：为了不阻塞当前 HTTP 请求，这里使用了 Fire-and-Forget 模式
        // 在真实业务中，如果对一致性要求高，这里应该 await
        await _publishEndpoint.Publish(new WeatherUpdated(forecast.Date, forecast.TemperatureC, forecast.Summary));
    }

    public bool UpdateForecast(UpdateWeatherForecastDto dto)
    {
        var existing = _context.Forecasts.Find(dto.Id);
        if (existing == null)
        {
            return false; // Not found
        }

        // 关键点：将 EF Core 追踪的“原始”RowVersion 设置为客户端传来的值
        // 这样在 SaveChanges 时，EF 会生成 WHERE Id = @Id AND RowVersion = @OriginalRowVersion
        _context.Entry(existing).Property(p => p.RowVersion).OriginalValue = dto.RowVersion;

        existing.Date = dto.Date;
        existing.TemperatureC = dto.TemperatureC;
        existing.Summary = dto.Summary;

        try
        {
            _context.SaveChanges();
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            // 捕获并发异常，说明数据库中的 RowVersion 已经变了（被其他人修改过）
            return false; 
        }
    }
}
