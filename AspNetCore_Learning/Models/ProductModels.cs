using System.ComponentModel.DataAnnotations;

namespace AspNetCore_Learning.Models;

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public string Category { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class CreateProductDto
{
    [Required(ErrorMessage = "产品名称是必填的")]
    [StringLength(50, MinimumLength = 2, ErrorMessage = "产品名称长度必须在2到50之间")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Range(0.01, 10000, ErrorMessage = "价格必须在 0.01 到 10000 之间")]
    public decimal Price { get; set; }

    [Range(0, 9999)]
    public int Stock { get; set; }

    [Required]
    [RegularExpression(@"^[a-zA-Z]+$", ErrorMessage = "分类只能包含字母")]
    public string Category { get; set; } = string.Empty;
}

public class ProductSearchDto
{
    public string? Keyword { get; set; }
    public string? Category { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    
    [Range(1, 100)]
    public int PageSize { get; set; } = 10;
    
    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;
}
