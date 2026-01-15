using AspNetCore_Learning.Data;
using Microsoft.EntityFrameworkCore;

namespace AspNetCore_Learning.Services;

public class AnotherWeatherService
{
    private readonly WeatherContext _context;

    public AnotherWeatherService(WeatherContext context)
    {
        _context = context;
    }

    public string GetContextInfo()
    {
        var connection = _context.Database.GetDbConnection();
        return $"Service: AnotherWeatherService, ContextId: {_context.ContextId}, ConnectionHashCode: {connection.GetHashCode()}";
    }
}
