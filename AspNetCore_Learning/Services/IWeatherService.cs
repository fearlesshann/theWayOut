using AspNetCore_Learning.Models;

namespace AspNetCore_Learning.Services;

public interface IWeatherService
{
    PagedResult<WeatherForecast> GetForecasts(int page, int pageSize);
    void AddForecast(CreateWeatherForecastDto forecast);
    bool UpdateForecast(UpdateWeatherForecastDto forecast);
}
