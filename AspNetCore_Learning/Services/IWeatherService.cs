using AspNetCore_Learning.Models;

namespace AspNetCore_Learning.Services;

public interface IWeatherService
{
    IEnumerable<WeatherForecast> GetForecasts();
}
