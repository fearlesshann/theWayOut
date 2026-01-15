using AspNetCore_Learning.Filters;
using AspNetCore_Learning.Models;
using Microsoft.AspNetCore.Mvc;

namespace AspNetCore_Learning.Controllers;

[ApiController]
[Route("api/[controller]")]
// [ServiceFilter(typeof(ValidateProductAttribute))] // 可以在这里对整个 Controller 启用 Filter
public class ProductsController : ControllerBase
{
    // 模拟内存数据库
    private static readonly List<Product> _products = new()
    {
        new Product { Id = 1, Name = "iPhone 15", Price = 5999, Stock = 100, Category = "Electronics" },
        new Product { Id = 2, Name = "MacBook Pro", Price = 12999, Stock = 50, Category = "Electronics" },
        new Product { Id = 3, Name = "Coffee Mug", Price = 25, Stock = 200, Category = "Home" }
    };

    private readonly ILogger<ProductsController> _logger;

    public ProductsController(ILogger<ProductsController> logger)
    {
        _logger = logger;
    }

    // 1. 复杂路由 + FromQuery 绑定
    // GET: api/products?keyword=phone&minPrice=1000
    [HttpGet]
    public IActionResult Search([FromQuery] ProductSearchDto search)
    {
        _logger.LogInformation("搜索产品: {Keyword}, 价格范围: {Min}-{Max}", search.Keyword, search.MinPrice, search.MaxPrice);

        var query = _products.AsQueryable();

        if (!string.IsNullOrEmpty(search.Keyword))
            query = query.Where(p => p.Name.Contains(search.Keyword, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrEmpty(search.Category))
            query = query.Where(p => p.Category.Equals(search.Category, StringComparison.OrdinalIgnoreCase));

        if (search.MinPrice.HasValue)
            query = query.Where(p => p.Price >= search.MinPrice.Value);

        if (search.MaxPrice.HasValue)
            query = query.Where(p => p.Price <= search.MaxPrice.Value);

        var results = query
            .Skip((search.Page - 1) * search.PageSize)
            .Take(search.PageSize)
            .ToList();

        return Ok(new { Total = query.Count(), Data = results });
    }

    // 2. 路由约束 + FromHeader 绑定
    // GET: api/products/1
    [HttpGet("{id:int}")] // 约束 id 必须是整数
    [ServiceFilter(typeof(ValidateProductAttribute))] // 仅对这个 Action 启用过滤器
    public IActionResult GetById(int id, [FromHeader(Name = "X-Client-Version")] string? clientVersion)
    {
        _logger.LogInformation("获取产品详情 ID: {Id}, 客户端版本: {Ver}", id, clientVersion ?? "Unknown");

        var product = _products.FirstOrDefault(p => p.Id == id);
        if (product == null)
        {
            return NotFound(new { Message = $"产品 ID {id} 不存在" });
        }

        return Ok(product);
    }

    // 3. 路由约束 (字符串)
    // GET: api/products/category/Electronics
    [HttpGet("category/{category:alpha}")] // 约束 category 只能包含字母
    public IActionResult GetByCategory(string category)
    {
        var products = _products.Where(p => p.Category.Equals(category, StringComparison.OrdinalIgnoreCase)).ToList();
        return Ok(products);
    }

    // 4. POST + FromBody + 自动验证
    // POST: api/products
    [HttpPost]
    public IActionResult Create([FromBody] CreateProductDto dto)
    {
        // 注意：[ApiController] 属性会自动检查 ModelState.IsValid
        // 如果数据验证失败，代码根本不会执行到这里，会自动返回 400 Bad Request

        var newId = _products.Max(p => p.Id) + 1;
        var product = new Product
        {
            Id = newId,
            Name = dto.Name,
            Price = dto.Price,
            Stock = dto.Stock,
            Category = dto.Category
        };

        _products.Add(product);

        // 返回 201 Created，并在响应头中包含 Location: api/products/{newId}
        return CreatedAtAction(nameof(GetById), new { id = newId }, product);
    }

    // 5. PUT 更新
    [HttpPut("{id:int}")]
    public IActionResult Update(int id, [FromBody] CreateProductDto dto)
    {
        var product = _products.FirstOrDefault(p => p.Id == id);
        if (product == null) return NotFound();

        product.Name = dto.Name;
        product.Price = dto.Price;
        product.Stock = dto.Stock;
        product.Category = dto.Category;

        return NoContent(); // 204 No Content
    }

    // 6. DELETE 删除
    [HttpDelete("{id:int}")]
    public IActionResult Delete(int id)
    {
        var product = _products.FirstOrDefault(p => p.Id == id);
        if (product == null) return NotFound();

        _products.Remove(product);
        return NoContent();
    }
}
