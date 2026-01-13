using AspNetCore_Learning.Data;
using AspNetCore_Learning.Models;

namespace AspNetCore_Learning.Services;

public class WeatherBackgroundWorker : BackgroundService
{
    private readonly ILogger<WeatherBackgroundWorker> _logger;
    private readonly IServiceProvider _serviceProvider; // 必须注入 ServiceProvider 来手动创建 Scope

    public WeatherBackgroundWorker(ILogger<WeatherBackgroundWorker> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("后台天气抓取服务已启动...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("正在抓取最新天气数据...");

                // 模拟耗时操作 (比如请求外部 API)
                await Task.Delay(1000, stoppingToken);

                // 重点：手动创建 Scope
                // 因为 BackgroundService 是 Singleton，而 DbContext 是 Scoped
                using (var scope = _serviceProvider.CreateScope())
                {
                    // 从 Scope 里获取 DbContext
                    var context = scope.ServiceProvider.GetRequiredService<WeatherContext>();

                    // 模拟生成一条新数据
                    var newForecast = new WeatherForecast
                    (
                        DateOnly.FromDateTime(DateTime.Now),
                        Random.Shared.Next(-10, 40),
                        "AutoFetched"
                    );

                    context.Forecasts.Add(newForecast);
                    await context.SaveChangesAsync(stoppingToken);

                    _logger.LogInformation("成功抓取并保存了一条天气数据: {Temp}C", newForecast.TemperatureC);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "抓取任务发生错误");
            }

            // 等待 10 秒再执行下一次
            await Task.Delay(10000, stoppingToken);
        }

        _logger.LogInformation("后台天气抓取服务已停止。");
    }
}
