using System.ComponentModel.DataAnnotations;

namespace Ecommerce.Api.DTOs;

public class CategoryUpsertRequest
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }
}
