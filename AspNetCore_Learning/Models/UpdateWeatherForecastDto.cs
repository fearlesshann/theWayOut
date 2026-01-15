using System.ComponentModel.DataAnnotations;

namespace AspNetCore_Learning.Models;

public class UpdateWeatherForecastDto
{
    [Required]
    public int Id { get; set; }

    [Required]
    public DateOnly Date { get; set; }

    [Required]
    [Range(-100, 100, ErrorMessage = "温度必须在 -100 到 100 之间")]
    public int TemperatureC { get; set; }

    [StringLength(100, ErrorMessage = "摘要太长了，最多100个字")]
    public string? Summary { get; set; }

    [Required]
    public byte[] RowVersion { get; set; } = null!; // 必须提供版本号以进行并发检查
}
