using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Ecommerce.Api.Services;

public class ImageKitService : IImageKitService
{
    private readonly ImageKitOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;

    public ImageKitService(IOptions<ImageKitOptions> options, IHttpClientFactory httpClientFactory)
    {
        _options = options.Value;
        _httpClientFactory = httpClientFactory;
    }

    public ImageKitAuthResult GenerateUploadAuth()
    {
        if (string.IsNullOrWhiteSpace(_options.Endpoint) ||
            string.IsNullOrWhiteSpace(_options.PublicKey) ||
            string.IsNullOrWhiteSpace(_options.PrivateKey))
        {
            throw new InvalidOperationException("ImageKit configuration is missing.");
        }

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        var expire = DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds();
        var signatureRaw = token + expire + _options.PrivateKey;

        using var sha1 = SHA1.Create();
        var signatureBytes = sha1.ComputeHash(Encoding.UTF8.GetBytes(signatureRaw));
        var signature = Convert.ToHexString(signatureBytes).ToLowerInvariant();

        return new ImageKitAuthResult(
            token,
            expire,
            signature,
            _options.PublicKey,
            _options.Endpoint);
    }

    public async Task<string> UploadFileAsync(byte[] fileContent, string fileName, string folder, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.PrivateKey))
        {
            throw new InvalidOperationException("ImageKit private key is missing.");
        }

        var client = _httpClientFactory.CreateClient();
        var authRaw = $"{_options.PrivateKey}:";
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(authRaw)));

        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(fileContent), "file", fileName);
        form.Add(new StringContent(fileName), "fileName");
        form.Add(new StringContent(folder), "folder");

        using var response = await client.PostAsync("https://upload.imagekit.io/api/v1/files/upload", form, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"ImageKit upload failed: {body}");
        }

        using var json = JsonDocument.Parse(body);
        var url = json.RootElement.GetProperty("url").GetString();
        return url ?? string.Empty;
    }
}
