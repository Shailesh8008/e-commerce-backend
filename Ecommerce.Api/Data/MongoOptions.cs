namespace Ecommerce.Api.Data;

public class MongoOptions
{
    public const string SectionName = "Mongo";

    public string ConnectionString { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = "ecommerce";
}
