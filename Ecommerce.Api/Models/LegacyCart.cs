using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Options;

namespace Ecommerce.Api.Models;

public class LegacyCart
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string _id { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.ObjectId)]
    public string userId { get; set; } = string.Empty;

    [BsonDictionaryOptions(DictionaryRepresentation.ArrayOfDocuments)]
    public List<Dictionary<string, object>> CartItems { get; set; } = new();
}
