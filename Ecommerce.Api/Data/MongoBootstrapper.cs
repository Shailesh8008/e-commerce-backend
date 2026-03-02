using Ecommerce.Api.Models;
using MongoDB.Driver;

namespace Ecommerce.Api.Data;

public static class MongoBootstrapper
{
    public static async Task EnsureIndexesAsync(MongoDbContext dbContext)
    {
        var userEmailIndex = new CreateIndexModel<ApplicationUser>(
            Builders<ApplicationUser>.IndexKeys.Ascending(x => x.Email),
            new CreateIndexOptions { Unique = true });

        var categoryNameIndex = new CreateIndexModel<Category>(
            Builders<Category>.IndexKeys.Ascending(x => x.Name),
            new CreateIndexOptions { Unique = true });

        var cartCompoundIndex = new CreateIndexModel<CartItem>(
            Builders<CartItem>.IndexKeys.Combine(
                Builders<CartItem>.IndexKeys.Ascending(x => x.UserId),
                Builders<CartItem>.IndexKeys.Ascending(x => x.ProductId)),
            new CreateIndexOptions { Unique = true });

        await dbContext.Users.Indexes.CreateOneAsync(userEmailIndex);
        await dbContext.Categories.Indexes.CreateOneAsync(categoryNameIndex);
        await dbContext.CartItems.Indexes.CreateOneAsync(cartCompoundIndex);
    }
}
