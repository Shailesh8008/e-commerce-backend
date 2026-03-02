using Ecommerce.Api.Data;
using Ecommerce.Api.DTOs;
using Ecommerce.Api.Models;
using Ecommerce.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Ecommerce.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly MongoDbContext _dbContext;
    private readonly ITokenService _tokenService;
    private readonly IPasswordHasher<ApplicationUser> _passwordHasher;

    public AuthController(
        MongoDbContext dbContext,
        ITokenService tokenService,
        IPasswordHasher<ApplicationUser> passwordHasher)
    {
        _dbContext = dbContext;
        _tokenService = tokenService;
        _passwordHasher = passwordHasher;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var existing = await _dbContext.Users.Find(u => u.Email == email).FirstOrDefaultAsync();
        if (existing is not null)
        {
            return BadRequest(new { message = "Email already registered." });
        }

        var user = new ApplicationUser
        {
            Id = ObjectId.GenerateNewId().ToString(),
            FullName = request.FullName,
            Email = email,
            Role = "Customer",
            CreatedAtUtc = DateTime.UtcNow
        };

        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);
        await _dbContext.Users.InsertOneAsync(user);

        var roles = new[] { user.Role };
        var token = _tokenService.GenerateToken(user, roles);

        return Ok(new AuthResponse
        {
            Token = token,
            ExpiresAtUtc = _tokenService.GetTokenExpiryUtc(),
            Email = user.Email,
            Role = user.Role
        });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _dbContext.Users.Find(u => u.Email == email).FirstOrDefaultAsync();
        if (user is null)
        {
            return Unauthorized(new { message = "Invalid email or password." });
        }

        var verifyResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verifyResult == PasswordVerificationResult.Failed)
        {
            return Unauthorized(new { message = "Invalid email or password." });
        }

        var roles = new[] { user.Role };
        var token = _tokenService.GenerateToken(user, roles);

        return Ok(new AuthResponse
        {
            Token = token,
            ExpiresAtUtc = _tokenService.GetTokenExpiryUtc(),
            Email = user.Email,
            Role = user.Role
        });
    }
}
