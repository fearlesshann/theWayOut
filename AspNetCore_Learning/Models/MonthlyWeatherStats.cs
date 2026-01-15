namespace AspNetCore_Learning.Models;

public class MonthlyWeatherStats
{
    public string Month { get; set; } = string.Empty;
    public double AverageTemp { get; set; }
    public int MaxTemp { get; set; }
    public int MinTemp { get; set; }
    public int RainyDaysCount { get; set; }
}
