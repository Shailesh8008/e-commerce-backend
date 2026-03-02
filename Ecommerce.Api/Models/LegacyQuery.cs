using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Ecommerce.Api.Models;

[BsonIgnoreExtraElements]
public class LegacyQuery
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string _id { get; set; } = string.Empty;

    public string username { get; set; } = string.Empty;
    public string email { get; set; } = string.Empty;
    public string query { get; set; } = string.Empty;
    public string status { get; set; } = "Unread";
}
