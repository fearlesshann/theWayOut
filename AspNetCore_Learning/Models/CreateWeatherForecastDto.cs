using System.ComponentModel.DataAnnotations;

namespace AspNetCore_Learning.Models;

public class CreateWeatherForecastDto
{
    [Required(ErrorMessage = "日期是必填的")]
    public DateOnly Date { get; set; }

    [Required]
    [Range(-100, 100, ErrorMessage = "温度必须在 -100 到 100 之间")]
    public int TemperatureC { get; set; }

    [StringLength(100, ErrorMessage = "摘要太长了，最多100个字")]
    public string? Summary { get; set; }
}
