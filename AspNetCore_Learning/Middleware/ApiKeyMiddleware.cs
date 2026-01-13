namespace AspNetCore_Learning.Middleware;

public class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfiguration _configuration;

    public ApiKeyMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        _configuration = configuration;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // 1. 如果是 GET 请求，直接放行 (假设读是公开的)
        if (context.Request.Method == "GET")
        {
            await _next(context);
            return;
        }

        // 2. 检查请求头
        if (!context.Request.Headers.TryGetValue("X-Api-Key", out var extractedApiKey))
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsync("Api Key was not provided.");
            return;
        }

        // 3. 校验密钥
        var apiKey = _configuration.GetValue<string>("Authentication:ApiKey");
        if (!apiKey.Equals(extractedApiKey))
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsync("Unauthorized client.");
            return;
        }

        // 4. 校验通过，放行
        await _next(context);
    }
}
