using Ecommerce.Api.Common;
using Ecommerce.Api.Data;
using Ecommerce.Api.DTOs;
using Ecommerce.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Ecommerce.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly MongoDbContext _dbContext;

    public ProductsController(MongoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] ProductQueryParams queryParams)
    {
        var categories = await _dbContext.Categories.Find(Builders<Category>.Filter.Empty).ToListAsync();
        var categoriesById = categories.ToDictionary(c => c.Id, c => c.Name);

        var productQuery = _dbContext.Products.AsQueryable();

        if (!string.IsNullOrWhiteSpace(queryParams.Search))
        {
            var search = queryParams.Search.Trim().ToLowerInvariant();
            productQuery = productQuery.Where(p =>
                p.Name.ToLower().Contains(search) ||
                p.Description.ToLower().Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(queryParams.CategoryId))
        {
            productQuery = productQuery.Where(p => p.CategoryId == queryParams.CategoryId);
        }

        if (queryParams.MinPrice.HasValue)
        {
            productQuery = productQuery.Where(p => p.Price >= queryParams.MinPrice.Value);
        }

        if (queryParams.MaxPrice.HasValue)
        {
            productQuery = productQuery.Where(p => p.Price <= queryParams.MaxPrice.Value);
        }

        productQuery = queryParams.SortBy?.ToLowerInvariant() switch
        {
            "price" => queryParams.Descending
                ? productQuery.OrderByDescending(p => p.Price)
                : productQuery.OrderBy(p => p.Price),
            "created" => queryParams.Descending
                ? productQuery.OrderByDescending(p => p.CreatedAtUtc)
                : productQuery.OrderBy(p => p.CreatedAtUtc),
            _ => queryParams.Descending
                ? productQuery.OrderByDescending(p => p.Name)
                : productQuery.OrderBy(p => p.Name)
        };

        var pageNumber = Math.Max(queryParams.PageNumber, 1);
        var pageSize = Math.Clamp(queryParams.PageSize, 1, 100);

        var totalCount = productQuery.Count();
        var items = productQuery
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList()
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Description,
                p.ImageUrl,
                p.Price,
                p.StockQuantity,
                p.CreatedAtUtc,
                Category = categoriesById.TryGetValue(p.CategoryId, out var categoryName)
                    ? new { Id = p.CategoryId, Name = categoryName }
                    : null
            })
            .ToList();

        var result = new PagedResult<object>
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount
        };

        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        if (!ObjectId.TryParse(id, out _))
        {
            return BadRequest(new { message = "Invalid product id." });
        }

        var product = await _dbContext.Products.Find(p => p.Id == id).FirstOrDefaultAsync();
        if (product is null)
        {
            return NotFound();
        }

        var category = await _dbContext.Categories.Find(c => c.Id == product.CategoryId).FirstOrDefaultAsync();

        return Ok(new
        {
            product.Id,
            product.Name,
            product.Description,
            product.ImageUrl,
            product.Price,
            product.StockQuantity,
            product.CreatedAtUtc,
            Category = category is null ? null : new { category.Id, category.Name }
        });
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> Create(ProductUpsertRequest request)
    {
        if (!ObjectId.TryParse(request.CategoryId, out _))
        {
            return BadRequest(new { message = "Invalid category id." });
        }

        var categoryExists = await _dbContext.Categories.Find(c => c.Id == request.CategoryId).AnyAsync();
        if (!categoryExists)
        {
            return BadRequest(new { message = "Invalid category." });
        }

        var product = new Product
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Name = request.Name.Trim(),
            Description = request.Description.Trim(),
            ImageUrl = request.ImageUrl,
            Price = request.Price,
            StockQuantity = request.StockQuantity,
            CategoryId = request.CategoryId,
            CreatedAtUtc = DateTime.UtcNow
        };

        await _dbContext.Products.InsertOneAsync(product);
        return CreatedAtAction(nameof(GetById), new { id = product.Id }, product);
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, ProductUpsertRequest request)
    {
        if (!ObjectId.TryParse(id, out _) || !ObjectId.TryParse(request.CategoryId, out _))
        {
            return BadRequest(new { message = "Invalid id provided." });
        }

        var product = await _dbContext.Products.Find(p => p.Id == id).FirstOrDefaultAsync();
        if (product is null)
        {
            return NotFound();
        }

        var categoryExists = await _dbContext.Categories.Find(c => c.Id == request.CategoryId).AnyAsync();
        if (!categoryExists)
        {
            return BadRequest(new { message = "Invalid category." });
        }

        var update = Builders<Product>.Update
            .Set(p => p.Name, request.Name.Trim())
            .Set(p => p.Description, request.Description.Trim())
            .Set(p => p.ImageUrl, request.ImageUrl)
            .Set(p => p.Price, request.Price)
            .Set(p => p.StockQuantity, request.StockQuantity)
            .Set(p => p.CategoryId, request.CategoryId);

        await _dbContext.Products.UpdateOneAsync(p => p.Id == id, update);
        return Ok(new { message = "Product updated." });
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!ObjectId.TryParse(id, out _))
        {
            return BadRequest(new { message = "Invalid product id." });
        }

        var result = await _dbContext.Products.DeleteOneAsync(p => p.Id == id);
        if (result.DeletedCount == 0)
        {
            return NotFound();
        }

        return NoContent();
    }
}
