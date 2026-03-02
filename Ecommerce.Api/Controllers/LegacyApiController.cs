using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Mail;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Ecommerce.Api.Data;
using Ecommerce.Api.Models;
using Ecommerce.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace Ecommerce.Api.Controllers;

[ApiController]
[Route("")]
public class LegacyApiController : ControllerBase
{
    private readonly MongoDbContext _dbContext;
    private readonly IRazorpayService _razorpayService;
    private readonly IImageKitService _imageKitService;
    private readonly IConfiguration _configuration;

    public LegacyApiController(
        MongoDbContext dbContext,
        IRazorpayService razorpayService,
        IImageKitService imageKitService,
        IConfiguration configuration)
    {
        _dbContext = dbContext;
        _razorpayService = razorpayService;
        _imageKitService = imageKitService;
        _configuration = configuration;
    }

    [HttpGet("health")]
    public IActionResult Health() => Ok(new { response = "ok" });

    [HttpPost("api/reg")]
    public async Task<IActionResult> Register([FromBody] LegacyRegisterRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.fname) ||
                string.IsNullOrWhiteSpace(request.email) ||
                string.IsNullOrWhiteSpace(request.pass1))
            {
                return Ok(new { message = "first name, email or password is/are missing!" });
            }

            var normalizedEmail = request.email.Trim().ToLowerInvariant();
            var exists = await _dbContext.LegacyUsers.Find(u => u.email == normalizedEmail).FirstOrDefaultAsync();
            if (exists is not null)
            {
                return Ok(new { ok = false, message = "Email already Exists!" });
            }

            var user = new LegacyUser
            {
                _id = ObjectId.GenerateNewId().ToString(),
                fname = request.fname,
                lname = request.lname,
                email = normalizedEmail,
                pass = BCrypt.Net.BCrypt.HashPassword(request.pass1),
                role = "user"
            };

            await _dbContext.LegacyUsers.InsertOneAsync(user);
            return Ok(new { ok = true, message = "User registered successfully" });
        }
        catch
        {
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    [HttpPost("api/login")]
    public async Task<IActionResult> Login([FromBody] LegacyLoginRequest request)
    {
        try
        {
            var normalizedEmail = request.email?.Trim().ToLowerInvariant() ?? string.Empty;
            var user = await _dbContext.LegacyUsers.Find(u => u.email == normalizedEmail).FirstOrDefaultAsync();
            if (user is null)
            {
                return Ok(new { ok = false, message = "Email does not exist!" });
            }

            var isPass = false;
            try
            {
                isPass = BCrypt.Net.BCrypt.Verify(request.pass ?? string.Empty, user.pass ?? string.Empty);
            }
            catch
            {
                isPass = false;
            }

            if (!isPass)
            {
                return Ok(new { ok = false, message = "Invalid Password!" });
            }

            var role = string.IsNullOrWhiteSpace(user.role) ? "user" : user.role;
            var token = CreateLegacyToken(user._id, role);
            var isProd = string.Equals(Environment.GetEnvironmentVariable("ENV"), "prod", StringComparison.OrdinalIgnoreCase);

            Response.Cookies.Append("token", token, new CookieOptions
            {
                HttpOnly = true,
                Secure = isProd,
                SameSite = isProd ? SameSiteMode.None : SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddDays(7)
            });

            return Ok(new
            {
                ok = true,
                message = role == "admin" ? "Welcome Admin" : "Login Successfully",
                userId = user._id
            });
        }
        catch
        {
            return Ok(new { ok = false, message = "Internal server error" });
        }
    }

    [HttpPost("api/addproduct")]
    public async Task<IActionResult> AddProduct([FromForm] LegacyAddProductRequest request, IFormFile? pimage)
    {
        var authResult = await EnsureAdminAsync();
        if (authResult.Error is not null)
        {
            return Ok(authResult.Error);
        }

        try
        {
            if (string.IsNullOrWhiteSpace(request.pname) ||
                request.price is null ||
                string.IsNullOrWhiteSpace(request.category) ||
                pimage is null)
            {
                return Ok(new { ok = false, message = "All fields are required!" });
            }

            await using var ms = new MemoryStream();
            await pimage.CopyToAsync(ms);

            var fileName = $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-{pimage.FileName}";
            var uploadedUrl = await _imageKitService.UploadFileAsync(ms.ToArray(), fileName, "/products");

            var product = new LegacyProduct
            {
                _id = ObjectId.GenerateNewId().ToString(),
                pname = request.pname,
                price = request.price.Value,
                category = request.category,
                status = "In Stock",
                pimage = uploadedUrl
            };

            await _dbContext.LegacyProducts.InsertOneAsync(product);
            return Ok(new { ok = true, message = "Product added successfully" });
        }
        catch
        {
            return Ok(new { ok = false, message = "Internal server error" });
        }
    }

    [HttpDelete("api/deleteproduct/{pid}")]
    public async Task<IActionResult> DeleteProduct(string pid)
    {
        var authResult = await EnsureAdminAsync();
        if (authResult.Error is not null)
        {
            return Ok(authResult.Error);
        }

        try
        {
            await _dbContext.LegacyProducts.DeleteOneAsync(p => p._id == pid);
            var data = await _dbContext.LegacyProducts.Find(Builders<LegacyProduct>.Filter.Empty).ToListAsync();
            return Ok(new { ok = true, data });
        }
        catch
        {
            return Ok(new { ok = false, message = "Internal server error" });
        }
    }

    [HttpPost("api/editproduct/{pid}")]
    public async Task<IActionResult> EditProduct(string pid, [FromBody] LegacyEditProductRequest request)
    {
        var authResult = await EnsureAdminAsync();
        if (authResult.Error is not null)
        {
            return Ok(authResult.Error);
        }

        try
        {
            if (string.IsNullOrWhiteSpace(request.pname) ||
                request.price is null ||
                string.IsNullOrWhiteSpace(request.category) ||
                string.IsNullOrWhiteSpace(request.status))
            {
                return Ok(new { ok = false, message = "All fields are required" });
            }

            var update = Builders<LegacyProduct>.Update
                .Set(x => x.pname, request.pname)
                .Set(x => x.price, request.price.Value)
                .Set(x => x.category, request.category)
                .Set(x => x.status, request.status);

            var updated = await _dbContext.LegacyProducts.UpdateOneAsync(p => p._id == pid, update);
            if (updated.MatchedCount == 0)
            {
                return Ok(new { ok = false, message = "Cannot update this product" });
            }

            return Ok(new { ok = true, message = "Updated Successfully" });
        }
        catch
        {
            return Ok(new { ok = false, message = "Internal server error" });
        }
    }

    [HttpGet("api/getproducts")]
    public async Task<IActionResult> GetProducts()
    {
        try
        {
            var data = await _dbContext.LegacyProducts.Find(Builders<LegacyProduct>.Filter.Empty).ToListAsync();
            return Ok(new { ok = true, data });
        }
        catch
        {
            return Ok(new { ok = false, message = "Internal server error" });
        }
    }

    [HttpGet("api/getproduct/{pid}")]
    public async Task<IActionResult> GetOneProduct(string pid)
    {
        try
        {
            var record = await _dbContext.LegacyProducts.Find(p => p._id == pid).FirstOrDefaultAsync();
            if (record is null)
            {
                return Ok(new { ok = false, message = "Cannot find product" });
            }

            return Ok(new { ok = true, data = record });
        }
        catch
        {
            return Ok(new { ok = false, message = "Internal server error" });
        }
    }

    [HttpGet("api/getqueries")]
    public async Task<IActionResult> GetQueries()
    {
        try
        {
            var record = await _dbContext.LegacyQueries.Find(Builders<LegacyQuery>.Filter.Empty).ToListAsync();
            return Ok(new { ok = true, data = record });
        }
        catch
        {
            return Ok(new { ok = false, message = "Internal server error" });
        }
    }

    [HttpGet("api/getquerydetails/{qid}")]
    public async Task<IActionResult> GetOneQuery(string qid)
    {
        try
        {
            var record = await _dbContext.LegacyQueries.Find(q => q._id == qid).FirstOrDefaultAsync();
            if (record is null)
            {
                return Ok(new { ok = false, message = "Cannot find query" });
            }

            return Ok(new { ok = true, data = record });
        }
        catch
        {
            return Ok(new { ok = false, message = "Internal server error" });
        }
    }

    [HttpDelete("api/deletequery/{qid}")]
    public async Task<IActionResult> DeleteQuery(string qid)
    {
        var authResult = await EnsureAdminAsync();
        if (authResult.Error is not null)
        {
            return Ok(authResult.Error);
        }

        try
        {
            await _dbContext.LegacyQueries.DeleteOneAsync(q => q._id == qid);
            var record = await _dbContext.LegacyQueries.Find(Builders<LegacyQuery>.Filter.Empty).ToListAsync();
            return Ok(new { ok = true, data = record });
        }
        catch
        {
            return Ok(new { ok = false, message = "Internal server error" });
        }
    }

    [HttpGet("api/updatestatus/{qid}")]
    public async Task<IActionResult> UpdateQuery(string qid)
    {
        var authResult = await EnsureAdminAsync();
        if (authResult.Error is not null)
        {
            return Ok(authResult.Error);
        }

        try
        {
            await _dbContext.LegacyQueries.UpdateOneAsync(q => q._id == qid, Builders<LegacyQuery>.Update.Set(q => q.status, "Seen"));
            var record = await _dbContext.LegacyQueries.Find(Builders<LegacyQuery>.Filter.Empty).ToListAsync();
            return Ok(new { ok = true, data = record });
        }
        catch
        {
            return Ok(new { ok = false, message = "Internal server error" });
        }
    }

    [HttpPost("api/queryreply/{qid}")]
    public async Task<IActionResult> QueryReply(string qid, [FromBody] LegacyQueryReplyRequest request)
    {
        var authResult = await EnsureAdminAsync();
        if (authResult.Error is not null)
        {
            return Ok(authResult.Error);
        }

        try
        {
            if (string.IsNullOrWhiteSpace(request.to) ||
                string.IsNullOrWhiteSpace(request.sub) ||
                string.IsNullOrWhiteSpace(request.reply))
            {
                return Ok(new { ok = false, message = "All fields are required" });
            }

            var adminEmail = Environment.GetEnvironmentVariable("ADMIN_EMAIL");
            var smtpPass = Environment.GetEnvironmentVariable("SMTP_PASS");

            if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(smtpPass))
            {
                return Ok(new { ok = false, message = "Cannot sent" });
            }

            using var smtp = new SmtpClient("smtp.gmail.com", 587)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(adminEmail, smtpPass)
            };

            var mail = new MailMessage(adminEmail, request.to, request.sub, request.reply)
            {
                IsBodyHtml = true
            };

            await smtp.SendMailAsync(mail);

            await _dbContext.LegacyQueries.UpdateOneAsync(
                q => q._id == qid,
                Builders<LegacyQuery>.Update.Set(q => q.status, "Replied"));

            return Ok(new { ok = true, message = "Successfully sent" });
        }
        catch
        {
            return Ok(new { ok = false, message = "Internal server error" });
        }
    }

    [HttpGet("api/checkadmin")]
    public async Task<IActionResult> CheckAdmin()
    {
        var authResult = await EnsureAdminAsync();
        if (authResult.Error is not null)
        {
            return Ok(authResult.Error);
        }

        return Ok(new { ok = true });
    }

    [HttpDelete("api/logout")]
    public async Task<IActionResult> Logout()
    {
        var authResult = await EnsureAuthAsync();
        if (authResult.Error is not null)
        {
            return Ok(authResult.Error);
        }

        try
        {
            var isProd = string.Equals(Environment.GetEnvironmentVariable("ENV"), "prod", StringComparison.OrdinalIgnoreCase);
            Response.Cookies.Delete("token", new CookieOptions
            {
                HttpOnly = true,
                Secure = isProd,
                SameSite = isProd ? SameSiteMode.None : SameSiteMode.Lax
            });

            return Ok(new { ok = true, message = "Successfully Logout" });
        }
        catch
        {
            return Ok(new { ok = false, message = "Internal server error" });
        }
    }

    [HttpGet("api/auth/user")]
    public async Task<IActionResult> CheckUser()
    {
        var authResult = await EnsureAuthAsync();
        if (authResult.Error is not null)
        {
            return Ok(authResult.Error);
        }

        return Ok(new { ok = true, userId = authResult.UserId });
    }

    [HttpPost("api/submitquery")]
    public async Task<IActionResult> SubmitQuery([FromBody] LegacySubmitQueryRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.username) ||
                string.IsNullOrWhiteSpace(request.email) ||
                string.IsNullOrWhiteSpace(request.query))
            {
                return Ok(new { ok = false, message = "All fields are required" });
            }

            var record = new LegacyQuery
            {
                _id = ObjectId.GenerateNewId().ToString(),
                username = request.username,
                email = request.email,
                query = request.query,
                status = "Unread"
            };

            await _dbContext.LegacyQueries.InsertOneAsync(record);
            return Ok(new { ok = true, message = "Query Submitted Successfully" });
        }
        catch
        {
            return Ok(new { ok = false, message = "Internal server error" });
        }
    }

    [HttpPost("api/savecart")]
    public async Task<IActionResult> SaveCart([FromBody] LegacySaveCartRequest request)
    {
        var authResult = await EnsureAuthAsync();
        if (authResult.Error is not null)
        {
            return Ok(authResult.Error);
        }

        try
        {
            var userId = authResult.UserId!;
            var userObjectId = ObjectId.Parse(userId);
            var cartFilter = Builders<BsonDocument>.Filter.Eq("userId", userObjectId);
            var existing = await _dbContext.LegacyCarts.Find(cartFilter).FirstOrDefaultAsync();

            var cartDoc = new BsonDocument
            {
                ["_id"] = existing?["_id"] ?? ObjectId.GenerateNewId(),
                ["userId"] = userObjectId,
                ["CartItems"] = request.cartData.HasValue
                    ? BsonSerializer.Deserialize<BsonValue>(request.cartData.Value.GetRawText())
                    : new BsonArray()
            };

            await _dbContext.LegacyCarts.ReplaceOneAsync(
                cartFilter,
                cartDoc,
                new ReplaceOptions { IsUpsert = true });

            return Ok(new { ok = true });
        }
        catch
        {
            return Ok(new { ok = false, message = "Internal server error" });
        }
    }

    [HttpGet("api/search")]
    public async Task<IActionResult> Search([FromQuery] string? query)
    {
        try
        {
            var pattern = query ?? string.Empty;
            var filter = Builders<LegacyProduct>.Filter.Regex(p => p.pname, new BsonRegularExpression(pattern, "i")) &
                         Builders<LegacyProduct>.Filter.Eq(p => p.status, "In Stock");

            var rec = await _dbContext.LegacyProducts.Find(filter).ToListAsync();
            return Ok(new { ok = true, data = rec });
        }
        catch
        {
            return Ok(new { ok = false, message = "Internal server error" });
        }
    }

    [HttpGet("api/fetchcart")]
    public async Task<IActionResult> FetchCart()
    {
        var authResult = await EnsureAuthAsync();
        if (authResult.Error is not null)
        {
            return Ok(authResult.Error);
        }

        try
        {
            var userId = ObjectId.Parse(authResult.UserId!);
            var rec = await _dbContext.LegacyCarts.Find(Builders<BsonDocument>.Filter.Eq("userId", userId)).FirstOrDefaultAsync();
            if (rec is null)
            {
                return Ok(new { ok = true, data = (object?)null });
            }

            var data = new Dictionary<string, object?>
            {
                ["_id"] = rec.GetValue("_id").AsObjectId.ToString(),
                ["userId"] = rec.GetValue("userId").AsObjectId.ToString(),
                ["CartItems"] = BsonTypeMapper.MapToDotNetValue(rec.GetValue("CartItems"))
            };

            return Ok(new { ok = true, data });
        }
        catch
        {
            return Ok(new { ok = false, message = "Internal server error" });
        }
    }

    [HttpPost("api/checkout")]
    public async Task<IActionResult> Checkout([FromBody] LegacyCheckoutRequest request)
    {
        var authResult = await EnsureAuthAsync();
        if (authResult.Error is not null)
        {
            return Ok(authResult.Error);
        }

        try
        {
            var userId = authResult.UserId!;
            var user = await _dbContext.LegacyUsers.Find(u => u._id == userId).FirstOrDefaultAsync();
            if (user is null)
            {
                return Ok(new { ok = false, message = "Error creating order" });
            }

            var notes = new Dictionary<string, string>
            {
                ["name"] = string.Join(" ", new[] { user.fname, user.lname }.Where(x => !string.IsNullOrWhiteSpace(x))),
                ["userId"] = userId
            };

            var order = await _razorpayService.CreateOrderAsync(
                Convert.ToInt64(request.amount * 100m),
                request.receipt ?? $"rcpt_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}",
                request.currency,
                notes);

            var data = new
            {
                id = order.Id,
                amount = order.Amount,
                currency = order.Currency,
                receipt = order.Receipt,
                status = order.Status
            };

            return Ok(new { ok = true, data, email = user.email });
        }
        catch
        {
            return Ok(new { ok = false, message = "Internal server error" });
        }
    }

    [HttpPost("api/verifypayment")]
    public async Task<IActionResult> VerifyPayment([FromBody] LegacyVerifyPaymentRequest request)
    {
        var authResult = await EnsureAuthAsync();
        if (authResult.Error is not null)
        {
            return Ok(authResult.Error);
        }

        try
        {
            var userId = authResult.UserId!;
            var isValid = _razorpayService.VerifyPaymentSignature(request.orderId ?? string.Empty, request.paymentId ?? string.Empty, request.signature ?? string.Empty);

            if (!isValid)
            {
                return Ok(new { ok = false, message = "Payment verification failed" });
            }

            var rec = new LegacyOrder
            {
                _id = ObjectId.GenerateNewId().ToString(),
                userId = userId,
                orderId = request.orderId ?? string.Empty,
                paymentId = request.paymentId ?? string.Empty,
                signature = request.signature ?? string.Empty,
                amount = request.amount,
                status = "paid",
                createdAt = DateTime.UtcNow
            };

            await _dbContext.LegacyOrders.InsertOneAsync(rec);
            return Ok(new { ok = true, message = "Payment Success" });
        }
        catch
        {
            return Ok(new { ok = false, message = "Internal server error" });
        }
    }

    private async Task<(string? UserId, string? Role, object? Error)> EnsureAuthAsync()
    {
        var token = Request.Cookies["token"];
        if (string.IsNullOrWhiteSpace(token))
        {
            return (null, null, new { ok = false, message = "Access Denied: No token provided" });
        }

        try
        {
            var secret = Environment.GetEnvironmentVariable("JWT_SECRET_KEY") ?? _configuration["Jwt:Key"] ?? string.Empty;
            var tokenHandler = new JwtSecurityTokenHandler();
            var parameters = new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                ClockSkew = TimeSpan.Zero
            };

            var principal = tokenHandler.ValidateToken(token, parameters, out _);
            var userId = principal.FindFirstValue("id") ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
            var role = principal.FindFirstValue("role") ?? principal.FindFirstValue(ClaimTypes.Role);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return (null, null, new { ok = false, message = "Token is invalid or expired" });
            }

            var exists = await _dbContext.LegacyUsers.Find(u => u._id == userId).AnyAsync();
            if (!exists)
            {
                return (null, null, new { ok = false, message = "Token is invalid or expired" });
            }

            return (userId, role, null);
        }
        catch
        {
            return (null, null, new { ok = false, message = "Token is invalid or expired" });
        }
    }

    private async Task<(string? UserId, string? Role, object? Error)> EnsureAdminAsync()
    {
        var auth = await EnsureAuthAsync();
        if (auth.Error is not null)
        {
            return auth;
        }

        if (!string.Equals(auth.Role, "admin", StringComparison.OrdinalIgnoreCase))
        {
            return (auth.UserId, auth.Role, new { ok = false, message = "Only Admin can access this page" });
        }

        return auth;
    }

    private string CreateLegacyToken(string userId, string role)
    {
        var secret = Environment.GetEnvironmentVariable("JWT_SECRET_KEY") ?? _configuration["Jwt:Key"] ?? string.Empty;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new("id", userId),
            new("role", role),
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Role, role)
        };

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public class LegacyRegisterRequest
    {
        public string? fname { get; set; }
        public string? lname { get; set; }
        public string? email { get; set; }
        public string? pass1 { get; set; }
    }

    public class LegacyLoginRequest
    {
        public string? email { get; set; }
        public string? pass { get; set; }
    }

    public class LegacyAddProductRequest
    {
        public string? pname { get; set; }
        public decimal? price { get; set; }
        public string? category { get; set; }
    }

    public class LegacyEditProductRequest
    {
        public string? pname { get; set; }
        public decimal? price { get; set; }
        public string? category { get; set; }
        public string? status { get; set; }
    }

    public class LegacyQueryReplyRequest
    {
        public string? to { get; set; }
        public string? sub { get; set; }
        public string? reply { get; set; }
    }

    public class LegacySubmitQueryRequest
    {
        public string? username { get; set; }
        public string? email { get; set; }
        public string? query { get; set; }
    }

    public class LegacySaveCartRequest
    {
        public JsonElement? cartData { get; set; }
    }

    public class LegacyCheckoutRequest
    {
        public decimal amount { get; set; }
        public string? currency { get; set; }
        public string? receipt { get; set; }
    }

    public class LegacyVerifyPaymentRequest
    {
        public decimal amount { get; set; }
        public string? orderId { get; set; }
        public string? paymentId { get; set; }
        public string? signature { get; set; }
    }
}
