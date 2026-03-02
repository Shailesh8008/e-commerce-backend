using System.Security.Claims;
using Ecommerce.Api.Data;
using Ecommerce.Api.DTOs;
using Ecommerce.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Ecommerce.Api.Controllers;

[ApiController]
[Authorize(Roles = "Customer")]
[Route("api/[controller]")]
public class CartController : ControllerBase
{
    private readonly MongoDbContext _dbContext;

    public CartController(MongoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetMyCart()
    {
        var userId = GetUserId();
        var cartItems = await _dbContext.CartItems.Find(ci => ci.UserId == userId).ToListAsync();

        var productIds = cartItems.Select(i => i.ProductId).Distinct().ToList();
        var products = await _dbContext.Products.Find(p => productIds.Contains(p.Id)).ToListAsync();
        var productById = products.ToDictionary(p => p.Id);

        var items = cartItems.Select(ci =>
        {
            productById.TryGetValue(ci.ProductId, out var product);
            var unitPrice = product?.Price ?? 0;
            var productName = product?.Name ?? "Unknown";
            return new
            {
                ci.Id,
                ci.ProductId,
                ProductName = productName,
                ci.Quantity,
                UnitPrice = unitPrice,
                Subtotal = unitPrice * ci.Quantity
            };
        }).ToList();

        return Ok(new
        {
            Items = items,
            Total = items.Sum(i => i.Subtotal)
        });
    }

    [HttpPost]
    public async Task<IActionResult> AddToCart(AddToCartRequest request)
    {
        if (!ObjectId.TryParse(request.ProductId, out _))
        {
            return BadRequest(new { message = "Invalid product id." });
        }

        var userId = GetUserId();
        var product = await _dbContext.Products.Find(p => p.Id == request.ProductId).FirstOrDefaultAsync();
        if (product is null)
        {
            return NotFound(new { message = "Product not found." });
        }

        var existingItem = await _dbContext.CartItems
            .Find(ci => ci.UserId == userId && ci.ProductId == request.ProductId)
            .FirstOrDefaultAsync();

        var desiredQuantity = request.Quantity + (existingItem?.Quantity ?? 0);
        if (product.StockQuantity < desiredQuantity)
        {
            return BadRequest(new { message = "Insufficient stock." });
        }

        if (existingItem is null)
        {
            await _dbContext.CartItems.InsertOneAsync(new CartItem
            {
                Id = ObjectId.GenerateNewId().ToString(),
                UserId = userId,
                ProductId = request.ProductId,
                Quantity = request.Quantity
            });
        }
        else
        {
            var update = Builders<CartItem>.Update.Set(ci => ci.Quantity, desiredQuantity);
            await _dbContext.CartItems.UpdateOneAsync(ci => ci.Id == existingItem.Id, update);
        }

        return Ok(new { message = "Item added to cart." });
    }

    [HttpPut("{cartItemId}")]
    public async Task<IActionResult> UpdateCartItem(string cartItemId, UpdateCartItemRequest request)
    {
        if (!ObjectId.TryParse(cartItemId, out _))
        {
            return BadRequest(new { message = "Invalid cart item id." });
        }

        var userId = GetUserId();
        var cartItem = await _dbContext.CartItems.Find(ci => ci.Id == cartItemId && ci.UserId == userId).FirstOrDefaultAsync();
        if (cartItem is null)
        {
            return NotFound();
        }

        var product = await _dbContext.Products.Find(p => p.Id == cartItem.ProductId).FirstOrDefaultAsync();
        if (product is null || product.StockQuantity < request.Quantity)
        {
            return BadRequest(new { message = "Insufficient stock." });
        }

        await _dbContext.CartItems.UpdateOneAsync(ci => ci.Id == cartItemId, Builders<CartItem>.Update.Set(ci => ci.Quantity, request.Quantity));
        return Ok(new { message = "Cart item updated." });
    }

    [HttpDelete("{cartItemId}")]
    public async Task<IActionResult> RemoveItem(string cartItemId)
    {
        if (!ObjectId.TryParse(cartItemId, out _))
        {
            return BadRequest(new { message = "Invalid cart item id." });
        }

        var userId = GetUserId();
        var result = await _dbContext.CartItems.DeleteOneAsync(ci => ci.Id == cartItemId && ci.UserId == userId);
        return result.DeletedCount == 0 ? NotFound() : NoContent();
    }

    [HttpDelete]
    public async Task<IActionResult> ClearCart()
    {
        var userId = GetUserId();
        await _dbContext.CartItems.DeleteManyAsync(ci => ci.UserId == userId);
        return NoContent();
    }

    private string GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("User not authenticated.");
}
