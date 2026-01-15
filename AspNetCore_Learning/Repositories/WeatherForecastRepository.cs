using AspNetCore_Learning.Data;
using AspNetCore_Learning.Models;
using Microsoft.EntityFrameworkCore; // 必须添加这个引用

namespace AspNetCore_Learning.Repositories;

public interface IWeatherForecastRepository : IRepository<WeatherForecast>
{
    // 这里可以定义特有的查询方法
    IEnumerable<WeatherForecast> GetHotDays(int thresholdTemp);

    // 复杂查询：获取某年的月度统计报表
    // 这种逻辑如果写在 Service 层，会充满 GroupBy/Select 等数据库细节，非常难维护
    Task<List<MonthlyWeatherStats>> GetMonthlyStatsAsync(int year);
}

public class WeatherForecastRepository : Repository<WeatherForecast>, IWeatherForecastRepository
{
    public WeatherForecastRepository(WeatherContext context) : base(context)
    {
    }

    public IEnumerable<WeatherForecast> GetHotDays(int thresholdTemp)
    {
        return _context.Forecasts
            .Where(f => f.TemperatureC > thresholdTemp)
            .OrderByDescending(f => f.TemperatureC)
            .ToList();
    }

    public async Task<List<MonthlyWeatherStats>> GetMonthlyStatsAsync(int year)
    {
        // 这里的查询逻辑很复杂：筛选年份 -> 按月分组 -> 聚合计算 -> 投影成 DTO
        // 这种"怎么取数据"的脏活累活，绝对不应该污染 Service 层
        return await _context.Forecasts
            .Where(f => f.Date.Year == year)
            .GroupBy(f => f.Date.Month)
            .Select(g => new MonthlyWeatherStats
            {
                Month = $"{year}-{g.Key:D2}",
                AverageTemp = g.Average(f => f.TemperatureC),
                MaxTemp = g.Max(f => f.TemperatureC),
                MinTemp = g.Min(f => f.TemperatureC),
                RainyDaysCount = g.Count(f => f.Summary != null && f.Summary.Contains("Rain"))
            })
            .OrderBy(s => s.Month)
            .ToListAsync();
    }
}
