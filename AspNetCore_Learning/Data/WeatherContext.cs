using AspNetCore_Learning.Models;
using Microsoft.EntityFrameworkCore;

namespace AspNetCore_Learning.Data;

public class WeatherContext : DbContext
{
    public WeatherContext(DbContextOptions<WeatherContext> options) : base(options)
    {
    }

    public DbSet<WeatherForecast> Forecasts { get; set; }
}
