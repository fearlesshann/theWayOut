using AspNetCore_Learning.Models;
using MassTransit;

namespace AspNetCore_Learning.Services;

public class WeatherUpdateConsumer : IConsumer<WeatherUpdated>
{
    private readonly ILogger<WeatherUpdateConsumer> _logger;

    public WeatherUpdateConsumer(ILogger<WeatherUpdateConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<WeatherUpdated> context)
    {
        var message = context.Message;
        
        // 模拟处理逻辑，比如发送邮件通知、同步到搜索引擎等
        _logger.LogInformation("【MassTransit】收到天气更新通知！日期: {Date}, 温度: {Temp}C, 摘要: {Summary}", 
            message.Date, message.TemperatureC, message.Summary);

        return Task.CompletedTask;
    }
}
