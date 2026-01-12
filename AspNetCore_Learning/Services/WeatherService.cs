using AspNetCore_Learning.Models;

namespace AspNetCore_Learning.Services;

public class WeatherService : IWeatherService
{
    private static readonly string[] Summaries = new[]
    {
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    };

    // 内存存储：使用 List 模拟数据库
    // 注意：因为 WeatherService 是 Scoped (每次请求新建)，所以这个 List 如果是实例字段，每次请求都会重置！
    // 为了演示“添加后能查到”，我必须把它改成 static，或者把 Service 注册为 Singleton。
    // 这里为了简单，我先把 List 改成 static。
    private static readonly List<WeatherForecast> _forecasts = new();

    static WeatherService()
    {
        // 初始化一些假数据
        var initialData = Enumerable.Range(1, 5).Select(index => new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            Summaries[Random.Shared.Next(Summaries.Length)]
        ));
        _forecasts.AddRange(initialData);
    }

    public IEnumerable<WeatherForecast> GetForecasts()
    {
        return _forecasts;
    }

    public void AddForecast(CreateWeatherForecastDto dto)
    {
        var forecast = new WeatherForecast(dto.Date, dto.TemperatureC, dto.Summary);
        _forecasts.Add(forecast);
    }
}
