namespace AspNetCore_Learning.Models;

// 定义一个事件消息：当天气数据更新时触发
public record WeatherUpdated(DateOnly Date, int TemperatureC, string Summary);
