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
public class CategoriesController : ControllerBase
{
    private readonly MongoDbContext _dbContext;

    public CategoriesController(MongoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var categories = await _dbContext.Categories
            .Find(Builders<Category>.Filter.Empty)
            .SortBy(c => c.Name)
            .ToListAsync();

        return Ok(categories);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        if (!ObjectId.TryParse(id, out _))
        {
            return BadRequest(new { message = "Invalid category id." });
        }

        var category = await _dbContext.Categories.Find(c => c.Id == id).FirstOrDefaultAsync();
        return category is null ? NotFound() : Ok(category);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> Create(CategoryUpsertRequest request)
    {
        var normalizedName = request.Name.Trim();
        var exists = await _dbContext.Categories.Find(c => c.Name == normalizedName).AnyAsync();
        if (exists)
        {
            return BadRequest(new { message = "Category name already exists." });
        }

        var category = new Category
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Name = normalizedName,
            Description = request.Description
        };

        await _dbContext.Categories.InsertOneAsync(category);
        return CreatedAtAction(nameof(GetById), new { id = category.Id }, category);
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, CategoryUpsertRequest request)
    {
        if (!ObjectId.TryParse(id, out _))
        {
            return BadRequest(new { message = "Invalid category id." });
        }

        var category = await _dbContext.Categories.Find(c => c.Id == id).FirstOrDefaultAsync();
        if (category is null)
        {
            return NotFound();
        }

        var normalizedName = request.Name.Trim();
        var duplicate = await _dbContext.Categories.Find(c => c.Id != id && c.Name == normalizedName).AnyAsync();
        if (duplicate)
        {
            return BadRequest(new { message = "Category name already exists." });
        }

        var update = Builders<Category>.Update
            .Set(c => c.Name, normalizedName)
            .Set(c => c.Description, request.Description);

        await _dbContext.Categories.UpdateOneAsync(c => c.Id == id, update);
        category.Name = normalizedName;
        category.Description = request.Description;

        return Ok(category);
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!ObjectId.TryParse(id, out _))
        {
            return BadRequest(new { message = "Invalid category id." });
        }

        var category = await _dbContext.Categories.Find(c => c.Id == id).FirstOrDefaultAsync();
        if (category is null)
        {
            return NotFound();
        }

        var hasProducts = await _dbContext.Products.Find(p => p.CategoryId == id).AnyAsync();
        if (hasProducts)
        {
            return BadRequest(new { message = "Cannot delete category with associated products." });
        }

        await _dbContext.Categories.DeleteOneAsync(c => c.Id == id);
        return NoContent();
    }
}
