using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Ecommerce.Api.Models;

[BsonIgnoreExtraElements]
public class LegacyOrder
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string _id { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.ObjectId)]
    public string userId { get; set; } = string.Empty;

    public string orderId { get; set; } = string.Empty;
    public string paymentId { get; set; } = string.Empty;
    public string signature { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.String)]
    public decimal amount { get; set; }

    public string status { get; set; } = "pending";
    public DateTime createdAt { get; set; } = DateTime.UtcNow;
}
