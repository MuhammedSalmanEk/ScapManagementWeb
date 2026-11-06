namespace ScrapManagement.Web.Infrastructure;

public class RazorpayOptions
{
    public const string SectionName = "Razorpay";
    public string KeyId { get; set; } = "";
    public string KeySecret { get; set; } = "";
    public string WebhookSecret { get; set; } = "";
      /// <summary>Online payment works only when both keys are present.</summary>
    public bool Enabled => !string.IsNullOrWhiteSpace(KeyId) && !string.IsNullOrWhiteSpace(KeySecret);
    // public bool Enabled => !string.IsNullOrWhiteSpace(KeyId) && !string.IsNullOrWhiteSpace(KeySecret);
}