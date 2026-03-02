using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Ecommerce.Api.Models;

[BsonIgnoreExtraElements]
public class LegacyUser
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string _id { get; set; } = string.Empty;

    public string fname { get; set; } = string.Empty;
    public string? lname { get; set; }
    public string email { get; set; } = string.Empty;
    public string pass { get; set; } = string.Empty;
    public string role { get; set; } = "user";
}
