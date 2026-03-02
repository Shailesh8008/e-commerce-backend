using Ecommerce.Api.Models;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Ecommerce.Api.Data;

public class MongoDbContext
{
    public IMongoDatabase Database { get; }

    public MongoDbContext(IOptions<MongoOptions> options)
    {
        var mongoOptions = options.Value;
        if (string.IsNullOrWhiteSpace(mongoOptions.ConnectionString))
        {
            throw new InvalidOperationException("MongoDB connection string is missing.");
        }

        var client = new MongoClient(mongoOptions.ConnectionString);
        Database = client.GetDatabase(mongoOptions.DatabaseName);
    }

    public IMongoCollection<ApplicationUser> Users => Database.GetCollection<ApplicationUser>("dotnet_users");
    public IMongoCollection<Category> Categories => Database.GetCollection<Category>("dotnet_categories");
    public IMongoCollection<Product> Products => Database.GetCollection<Product>("dotnet_products");
    public IMongoCollection<CartItem> CartItems => Database.GetCollection<CartItem>("dotnet_cartItems");
    public IMongoCollection<Order> Orders => Database.GetCollection<Order>("dotnet_orders");
    public IMongoCollection<LegacyUser> LegacyUsers => Database.GetCollection<LegacyUser>("users");
    public IMongoCollection<LegacyProduct> LegacyProducts => Database.GetCollection<LegacyProduct>("products");
    public IMongoCollection<LegacyQuery> LegacyQueries => Database.GetCollection<LegacyQuery>("queries");
    public IMongoCollection<BsonDocument> LegacyCarts => Database.GetCollection<BsonDocument>("carts");
    public IMongoCollection<LegacyOrder> LegacyOrders => Database.GetCollection<LegacyOrder>("orders");
}
