namespace Ecommerce.Api.Services;

public record ImageKitAuthResult(string Token, long Expire, string Signature, string PublicKey, string UrlEndpoint);

public interface IImageKitService
{
    ImageKitAuthResult GenerateUploadAuth();
    Task<string> UploadFileAsync(byte[] fileContent, string fileName, string folder, CancellationToken cancellationToken = default);
}
