using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Ecommerce.Api.Services;

public class RazorpayService : IRazorpayService
{
    private readonly HttpClient _httpClient;
    private readonly RazorpayOptions _options;

    public RazorpayService(HttpClient httpClient, IOptions<RazorpayOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;

        var keyBytes = Encoding.UTF8.GetBytes($"{_options.KeyId}:{_options.KeySecret}");
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", Convert.ToBase64String(keyBytes));
    }

    public async Task<RazorpayOrderResult> CreateOrderAsync(
        long amountInPaise,
        string receipt,
        string? currency = null,
        IReadOnlyDictionary<string, string>? notes = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.KeyId) || string.IsNullOrWhiteSpace(_options.KeySecret))
        {
            throw new InvalidOperationException("Razorpay credentials are missing in configuration.");
        }

        var payload = new
        {
            amount = amountInPaise,
            currency = string.IsNullOrWhiteSpace(currency) ? _options.Currency : currency,
            receipt,
            payment_capture = 1,
            notes
        };

        using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync("/v1/orders", content, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Razorpay order creation failed: {body}");
        }

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        return new RazorpayOrderResult(
            root.GetProperty("id").GetString() ?? string.Empty,
            root.GetProperty("amount").GetInt64(),
            root.GetProperty("currency").GetString() ?? _options.Currency,
            root.GetProperty("receipt").GetString() ?? receipt,
            root.GetProperty("status").GetString() ?? "created");
    }

    public bool VerifyPaymentSignature(string razorpayOrderId, string razorpayPaymentId, string razorpaySignature)
    {
        var payload = $"{razorpayOrderId}|{razorpayPaymentId}";
        var keyBytes = Encoding.UTF8.GetBytes(_options.KeySecret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(payloadBytes);
        var generatedSignature = Convert.ToHexString(hash).ToLowerInvariant();

        return generatedSignature == razorpaySignature.Trim().ToLowerInvariant();
    }

    public string GetKeyId() => _options.KeyId;
}
