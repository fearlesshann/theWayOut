using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace AspNetCore_Learning.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BasicsController : ControllerBase
{
    // ==========================================
    // 第一部分：ASP.NET Core 能收到什么 (Inputs)
    // ==========================================

    // 1. 从 URL 查询字符串获取 (Query String)
    // URL: GET /api/basics/query?name=Trae&age=1
    [HttpGet("query")]
    public IActionResult GetFromQuery([FromQuery] string name, [FromQuery] int age)
    {
        return Ok($"收到查询参数 - Name: {name}, Age: {age}");
    }

    // 2. 从 URL 路径获取 (Route Data)
    // URL: GET /api/basics/route/123
    [HttpGet("route/{id}")]
    public async Task<IActionResult> GetFromRoute([FromRoute] int id)
    {
        return Ok($"收到路径参数 - ID: {id}");
    }

    // 3. 从请求体获取 JSON (Body)
    // URL: POST /api/basics/body
    // Body: { "name": "Trae", "description": "AI Assistant" }
    [HttpPost("body")]
    public IActionResult GetFromBody([FromBody] SimpleModel model)
    {
        return Ok($"收到Body JSON - Name: {model.Name}, Desc: {model.Description}");
    }

    // 4. 从请求头获取 (Header)
    // URL: GET /api/basics/header
    // Header: User-Agent: MyCustomClient
    [HttpGet("header")]
    public IActionResult GetFromHeader([FromHeader(Name = "User-Agent")] string userAgent)
    {
        return Ok($"收到Header - User-Agent: {userAgent}");
    }

    // 5. 从表单获取 (Form Data - 通常用于上传文件)
    // URL: POST /api/basics/form
    [HttpPost("form")]
    public IActionResult GetFromForm([FromForm] string username, [FromForm] IFormFile file)
    {
        return Ok($"收到表单 - Username: {username}, File: {file.FileName}, Size: {file.Length} bytes");
    }

    // 6. 最底层的 HttpContext (万能钥匙)
    // 当以上属性绑定无法满足需求时，可以直接访问 HttpContext
    [HttpGet("context")]
    public IActionResult GetFromContext()
    {
        var method = HttpContext.Request.Method;
        var path = HttpContext.Request.Path;
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        
        return Ok($"底层信息 - Method: {method}, Path: {path}, IP: {ip}");
    }

    // ==========================================
    // 第二部分：ASP.NET Core 能返回什么 (Outputs)
    // ==========================================

    // 1. 返回状态码 + 数据 (最常用)
    [HttpGet("return/ok")]
    public IActionResult ReturnOk()
    {
        // 自动序列化为 JSON，状态码 200
        return Ok(new { Status = "Success", Data = "一切正常" });
    }

    // 2. 返回特定的状态码 (错误处理)
    [HttpGet("return/error")]
    public IActionResult ReturnError([FromQuery] bool error)
    {
        if (error)
        {
            // 返回 400 Bad Request
            return BadRequest("参数有误，不能为 true");
        }
        // 返回 404 Not Found
        return NotFound("找不到资源");
    }

    // 3. 返回纯文本
    [HttpGet("return/text")]
    public IActionResult ReturnText()
    {
        return Content("这是一段纯文本，不是 JSON", "text/plain", Encoding.UTF8);
    }

    // 4. 返回文件 (下载)
    [HttpGet("return/file")]
    public IActionResult ReturnFile()
    {
        var content = "这是文件内容";
        var bytes = Encoding.UTF8.GetBytes(content);
        // 返回文件流，MIME类型，文件名
        return File(bytes, "application/octet-stream", "test.txt");
    }

    // 5. 重定向 (跳转)
    [HttpGet("return/redirect")]
    public IActionResult ReturnRedirect()
    {
        // 302 跳转到百度
        return Redirect("https://www.baidu.com");
    }
    
    // 简单模型类
    public class SimpleModel
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}
