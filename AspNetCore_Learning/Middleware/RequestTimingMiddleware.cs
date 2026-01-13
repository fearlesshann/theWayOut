using System.Diagnostics;

namespace AspNetCore_Learning.Middleware;

public class RequestTimingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestTimingMiddleware> _logger;

    // 构造函数注入 RequestDelegate (必须) 和 Logger
    public RequestTimingMiddleware(RequestDelegate next, ILogger<RequestTimingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    // Invoke 或 InvokeAsync 方法是约定的入口
    public async Task InvokeAsync(HttpContext context)
    {
        // 1. 请求进来前：启动计时器
        var stopwatch = Stopwatch.StartNew();

        // 2. 调用下一个中间件 (把接力棒传下去)
        await _next(context);

        // 3. 请求回来后：停止计时并记录
        stopwatch.Stop();
        
        var elapsed = stopwatch.ElapsedMilliseconds;
        var path = context.Request.Path;
        var method = context.Request.Method;
        var statusCode = context.Response.StatusCode;

        // 如果处理时间超过 500ms，记录为警告，否则为信息
        if (elapsed > 500)
        {
            _logger.LogWarning("慢请求警告: {Method} {Path} 耗时 {Elapsed}ms (状态码: {Status})", 
                method, path, elapsed, statusCode);
        }
        else
        {
            _logger.LogInformation("请求完成: {Method} {Path} 耗时 {Elapsed}ms", 
                method, path, elapsed);
        }
    }
}
