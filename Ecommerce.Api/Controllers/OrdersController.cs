using System.Security.Claims;
using Ecommerce.Api.Data;
using Ecommerce.Api.DTOs;
using Ecommerce.Api.Models;
using Ecommerce.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Ecommerce.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly MongoDbContext _dbContext;
    private readonly IRazorpayService _razorpayService;

    public OrdersController(MongoDbContext dbContext, IRazorpayService razorpayService)
    {
        _dbContext = dbContext;
        _razorpayService = razorpayService;
    }

    [Authorize(Roles = "Customer")]
    [HttpPost("place")]
    public async Task<IActionResult> PlaceOrder()
    {
        var userId = GetUserId();
        var cartItems = await _dbContext.CartItems.Find(ci => ci.UserId == userId).ToListAsync();
        if (cartItems.Count == 0)
        {
            return BadRequest(new { message = "Cart is empty." });
        }

        var productIds = cartItems.Select(ci => ci.ProductId).Distinct().ToList();
        var products = await _dbContext.Products.Find(p => productIds.Contains(p.Id)).ToListAsync();
        var productById = products.ToDictionary(p => p.Id);

        foreach (var item in cartItems)
        {
            if (!productById.TryGetValue(item.ProductId, out var product) || product.StockQuantity < item.Quantity)
            {
                return BadRequest(new { message = $"Insufficient stock for product ID {item.ProductId}." });
            }
        }

        var orderItems = new List<OrderItem>();
        decimal total = 0;

        foreach (var cartItem in cartItems)
        {
            var product = productById[cartItem.ProductId];
            var unitPrice = product.Price;
            var lineTotal = unitPrice * cartItem.Quantity;
            total += lineTotal;

            orderItems.Add(new OrderItem
            {
                ProductId = cartItem.ProductId,
                Quantity = cartItem.Quantity,
                UnitPrice = unitPrice,
                TotalPrice = lineTotal
            });

            var stockUpdate = Builders<Product>.Update.Inc(p => p.StockQuantity, -cartItem.Quantity);
            await _dbContext.Products.UpdateOneAsync(p => p.Id == product.Id, stockUpdate);
        }

        var order = new Order
        {
            Id = ObjectId.GenerateNewId().ToString(),
            UserId = userId,
            OrderedAtUtc = DateTime.UtcNow,
            Status = OrderStatus.Pending,
            PaymentStatus = PaymentStatus.Pending,
            TotalAmount = total,
            Items = orderItems
        };

        await _dbContext.Orders.InsertOneAsync(order);
        await _dbContext.CartItems.DeleteManyAsync(ci => ci.UserId == userId);

        return Ok(new
        {
            message = "Order placed successfully.",
            order.Id,
            order.TotalAmount,
            order.Status,
            order.PaymentStatus
        });
    }

    [Authorize(Roles = "Customer")]
    [HttpGet("mine")]
    public async Task<IActionResult> GetMyOrders()
    {
        var userId = GetUserId();
        var orders = await _dbContext.Orders
            .Find(o => o.UserId == userId)
            .SortByDescending(o => o.OrderedAtUtc)
            .ToListAsync();

        var productIds = orders.SelectMany(o => o.Items).Select(i => i.ProductId).Distinct().ToList();
        var products = await _dbContext.Products.Find(p => productIds.Contains(p.Id)).ToListAsync();
        var productById = products.ToDictionary(p => p.Id, p => p.Name);

        var response = orders.Select(o => new
        {
            o.Id,
            o.OrderedAtUtc,
            o.TotalAmount,
            o.Status,
            o.PaymentStatus,
            Items = o.Items.Select(i => new
            {
                i.ProductId,
                ProductName = productById.TryGetValue(i.ProductId, out var name) ? name : "Unknown",
                i.Quantity,
                i.UnitPrice,
                i.TotalPrice
            })
        });

        return Ok(response);
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> GetAllOrders()
    {
        var orders = await _dbContext.Orders
            .Find(Builders<Order>.Filter.Empty)
            .SortByDescending(o => o.OrderedAtUtc)
            .ToListAsync();

        var productIds = orders.SelectMany(o => o.Items).Select(i => i.ProductId).Distinct().ToList();
        var products = await _dbContext.Products.Find(p => productIds.Contains(p.Id)).ToListAsync();
        var productById = products.ToDictionary(p => p.Id, p => p.Name);

        var response = orders.Select(o => new
        {
            o.Id,
            o.UserId,
            o.OrderedAtUtc,
            o.TotalAmount,
            o.Status,
            o.PaymentStatus,
            Items = o.Items.Select(i => new
            {
                i.ProductId,
                ProductName = productById.TryGetValue(i.ProductId, out var name) ? name : "Unknown",
                i.Quantity,
                i.UnitPrice,
                i.TotalPrice
            })
        });

        return Ok(response);
    }

    [Authorize(Roles = "Customer")]
    [HttpPost("{orderId}/razorpay-order")]
    public async Task<IActionResult> CreateRazorpayOrder(string orderId, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(orderId, out _))
        {
            return BadRequest(new { message = "Invalid order id." });
        }

        var userId = GetUserId();
        var order = await _dbContext.Orders.Find(o => o.Id == orderId && o.UserId == userId).FirstOrDefaultAsync();
        if (order is null)
        {
            return NotFound();
        }

        if (order.PaymentStatus == PaymentStatus.Success)
        {
            return BadRequest(new { message = "Order is already paid." });
        }

        var amountInPaise = Convert.ToInt64(decimal.Round(order.TotalAmount * 100m, 0, MidpointRounding.AwayFromZero));
        var receipt = $"order_{order.Id}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        var razorpayOrder = await _razorpayService.CreateOrderAsync(
            amountInPaise,
            receipt,
            cancellationToken: cancellationToken);

        return Ok(new
        {
            order.Id,
            order.TotalAmount,
            order.PaymentStatus,
            RazorpayKeyId = _razorpayService.GetKeyId(),
            RazorpayOrderId = razorpayOrder.Id,
            razorpayOrder.Amount,
            razorpayOrder.Currency,
            razorpayOrder.Receipt,
            razorpayOrder.Status
        });
    }

    [Authorize(Roles = "Customer")]
    [HttpPost("{orderId}/verify-payment")]
    public async Task<IActionResult> VerifyPayment(string orderId, RazorpayPaymentVerifyRequest request)
    {
        if (!ObjectId.TryParse(orderId, out _))
        {
            return BadRequest(new { message = "Invalid order id." });
        }

        var userId = GetUserId();
        var order = await _dbContext.Orders.Find(o => o.Id == orderId && o.UserId == userId).FirstOrDefaultAsync();
        if (order is null)
        {
            return NotFound();
        }

        if (order.PaymentStatus == PaymentStatus.Success)
        {
            return BadRequest(new { message = "Order is already paid." });
        }

        var isSignatureValid = _razorpayService.VerifyPaymentSignature(
            request.RazorpayOrderId,
            request.RazorpayPaymentId,
            request.RazorpaySignature);

        if (!isSignatureValid)
        {
            var failedUpdate = Builders<Order>.Update
                .Set(o => o.PaymentStatus, PaymentStatus.Failed)
                .Set(o => o.Status, OrderStatus.Cancelled);

            await _dbContext.Orders.UpdateOneAsync(o => o.Id == orderId, failedUpdate);
            return BadRequest(new { message = "Invalid Razorpay signature. Payment verification failed." });
        }

        var successUpdate = Builders<Order>.Update
            .Set(o => o.PaymentStatus, PaymentStatus.Success)
            .Set(o => o.Status, OrderStatus.Paid);

        await _dbContext.Orders.UpdateOneAsync(o => o.Id == orderId, successUpdate);

        return Ok(new
        {
            order.Id,
            PaymentStatus = PaymentStatus.Success,
            Status = OrderStatus.Paid,
            request.RazorpayOrderId,
            request.RazorpayPaymentId,
            Message = "Payment verified successfully."
        });
    }

    private string GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("User not authenticated.");
}
