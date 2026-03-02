namespace Ecommerce.Api.Services;

public class ImageKitOptions
{
    public const string SectionName = "ImageKit";

    public string Endpoint { get; set; } = string.Empty;
    public string PublicKey { get; set; } = string.Empty;
    public string PrivateKey { get; set; } = string.Empty;
}
