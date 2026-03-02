using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Ecommerce.Api.Models;

[BsonIgnoreExtraElements]
public class LegacyProduct
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string _id { get; set; } = string.Empty;

    public string pname { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.String)]
    public decimal price { get; set; }

    public string category { get; set; } = string.Empty;
    public string status { get; set; } = "In Stock";
    public string? pimage { get; set; }
}
