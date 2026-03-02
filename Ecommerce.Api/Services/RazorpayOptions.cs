namespace Ecommerce.Api.Services;

public class RazorpayOptions
{
    public const string SectionName = "Razorpay";

    public string BaseUrl { get; set; } = "https://api.razorpay.com";
    public string KeyId { get; set; } = string.Empty;
    public string KeySecret { get; set; } = string.Empty;
    public string Currency { get; set; } = "INR";
}
