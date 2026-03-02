using Ecommerce.Api.Data;
using Ecommerce.Api.Models;
using Microsoft.AspNetCore.Identity;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Ecommerce.Api.Services;

public static class SeedService
{
    public static async Task SeedRolesAndAdminAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MongoDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<ApplicationUser>>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        var adminEmail = config["AdminUser:Email"] ?? "admin@ecommerce.local";
        var adminPassword = config["AdminUser:Password"] ?? "Admin@123";
        var adminName = config["AdminUser:FullName"] ?? "System Admin";

        var existingAdmin = await dbContext.Users.Find(u => u.Email == adminEmail).FirstOrDefaultAsync();
        if (existingAdmin is not null)
        {
            return;
        }

        var admin = new ApplicationUser
        {
            Id = ObjectId.GenerateNewId().ToString(),
            FullName = adminName,
            Email = adminEmail,
            Role = "Admin",
            CreatedAtUtc = DateTime.UtcNow
        };

        admin.PasswordHash = passwordHasher.HashPassword(admin, adminPassword);
        await dbContext.Users.InsertOneAsync(admin);

        var legacyAdmin = await dbContext.LegacyUsers.Find(u => u.email == adminEmail).FirstOrDefaultAsync();
        if (legacyAdmin is null)
        {
            await dbContext.LegacyUsers.InsertOneAsync(new LegacyUser
            {
                _id = ObjectId.GenerateNewId().ToString(),
                fname = adminName,
                lname = string.Empty,
                email = adminEmail,
                pass = BCrypt.Net.BCrypt.HashPassword(adminPassword),
                role = "admin"
            });
        }
    }
}
