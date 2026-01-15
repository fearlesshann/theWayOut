using System.ComponentModel.DataAnnotations;

namespace AspNetCore_Learning.Models;

public class WeatherForecast
{
    public int Id { get; set; } // 新增主键

    [Timestamp]
    public byte[] RowVersion { get; set; } = null!; // 乐观并发控制令牌

    public DateOnly Date { get; set; }

    public int TemperatureC { get; set; }

    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);

    public string? Summary { get; set; }

    public WeatherForecast(DateOnly date, int temperatureC, string? summary)
    {
        Date = date;
        TemperatureC = temperatureC;
        Summary = summary;
    }

    public WeatherForecast() { }
}
