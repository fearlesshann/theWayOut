using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AspNetCore_Learning;

public class GlobalExceptionFilter : IExceptionFilter
{
    private readonly ILogger<GlobalExceptionFilter> _logger;

    public GlobalExceptionFilter(ILogger<GlobalExceptionFilter> logger)
    {
        _logger = logger;
    }

    public void OnException(ExceptionContext context)
    {
        // 1. 记录错误日志
        _logger.LogError(context.Exception, "发生了未处理的异常: {Message}", context.Exception.Message);

        // 2. 定义返回给客户端的错误格式
        var errorResponse = new
        {
            Success = false,
            Message = "服务器内部错误，请稍后重试。",
            // 在生产环境不要返回详细的 StackTrace，这里为了演示可以加上
            Detail = context.Exception.Message
        };

        // 3. 设置 Result
        context.Result = new ObjectResult(errorResponse)
        {
            StatusCode = 500
        };

        // 4. 标记为已处理，防止异常继续冒泡
        context.ExceptionHandled = true;
    }
}
