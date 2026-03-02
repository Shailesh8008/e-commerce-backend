using System.ComponentModel.DataAnnotations;

namespace Ecommerce.Api.DTOs;

public class ProductUpsertRequest
{
    [Required]
    [StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(1000)]
    public string Description { get; set; } = string.Empty;

    [Url]
    [StringLength(2048)]
    public string? ImageUrl { get; set; }

    [Range(0.01, 999999.99)]
    public decimal Price { get; set; }

    [Range(0, int.MaxValue)]
    public int StockQuantity { get; set; }

    [Required]
    public string CategoryId { get; set; } = string.Empty;
}
