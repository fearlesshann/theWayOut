using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AspNetCore_Learning.Filters;

// 这是一个 Action Filter，它可以在 Action 执行前后“插一脚”
public class ValidateProductAttribute : ActionFilterAttribute
{
    private readonly ILogger<ValidateProductAttribute> _logger;

    public ValidateProductAttribute(ILogger<ValidateProductAttribute> logger)
    {
        _logger = logger;
    }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        _logger.LogInformation("[Filter] 正在进入 Action: {ActionName}", context.ActionDescriptor.DisplayName);

        // 演示：检查是否包含特定的 Header
        if (!context.HttpContext.Request.Headers.ContainsKey("X-Client-Version"))
        {
            _logger.LogWarning("[Filter] 缺少 X-Client-Version 头");
            // 可以在这里直接短路返回，Action 就不会被执行了
            // context.Result = new BadRequestObjectResult("Missing X-Client-Version header");
        }

        base.OnActionExecuting(context);
    }

    public override void OnActionExecuted(ActionExecutedContext context)
    {
        _logger.LogInformation("[Filter] Action 执行完毕。耗时操作可以在这里记录。");
        base.OnActionExecuted(context);
    }
}
