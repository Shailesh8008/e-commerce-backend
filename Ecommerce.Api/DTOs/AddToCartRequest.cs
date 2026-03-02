using System.ComponentModel.DataAnnotations;

namespace Ecommerce.Api.DTOs;

public class AddToCartRequest
{
    [Required]
    public string ProductId { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }
}
