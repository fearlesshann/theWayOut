using AspNetCore_Learning.Repositories;

namespace AspNetCore_Learning.Services;

public class RepositoryDemoService
{
    private readonly IWeatherForecastRepository _repository;
    private readonly AspNetCore_Learning.Data.WeatherContext _context; // 用于提交事务

    // 注意：Repository 模式通常配合 UnitOfWork 模式一起使用，
    // 这里为了演示简单，我们还是注入 Context 来做 SaveChanges (UnitOfWork 的职责)
    public RepositoryDemoService(IWeatherForecastRepository repository, AspNetCore_Learning.Data.WeatherContext context)
    {
        _repository = repository;
        _context = context;
    }

    public IEnumerable<string> GetHotDaysSummary()
    {
        // 业务逻辑层不再直接接触 DbContext 或 DbSet
        // 而是调用语义更清晰的 Repository 方法
        var hotDays = _repository.GetHotDays(30);
        
        return hotDays.Select(d => $"{d.Date}: {d.TemperatureC}°C - {d.Summary}");
    }

    public async Task<string> GenerateYearlyReport(int year)
    {
        // Service 层只需要做一件事：调用 Repository 拿数据
        // 不需要关心数据是 SQL 查出来的，还是 Redis 缓存里拿的
        var stats = await _repository.GetMonthlyStatsAsync(year);

        if (!stats.Any())
        {
            return $"No data found for year {year}";
        }

        // Service 层负责业务逻辑：比如格式化报表
        var report = $"Weather Report for {year}\n";
        report += "----------------------------\n";
        foreach (var month in stats)
        {
            report += $"{month.Month}: Avg {month.AverageTemp:F1}°C, Rain: {month.RainyDaysCount} days\n";
        }

        return report;
    }
}
