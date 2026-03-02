namespace Ecommerce.Api.Services;

public record RazorpayOrderResult(string Id, long Amount, string Currency, string Receipt, string Status);

public interface IRazorpayService
{
    Task<RazorpayOrderResult> CreateOrderAsync(
        long amountInPaise,
        string receipt,
        string? currency = null,
        IReadOnlyDictionary<string, string>? notes = null,
        CancellationToken cancellationToken = default);
    bool VerifyPaymentSignature(string razorpayOrderId, string razorpayPaymentId, string razorpaySignature);
    string GetKeyId();
}
