using System.ComponentModel.DataAnnotations;
namespace ScrapManagement.Web.Models;

public enum PaymentStatus { Created = 0, Paid = 1, Failed = 2 }

public class Payment
{
    public int Id { get; set; }
    public int ShopId { get; set; }
    public Shop? Shop { get; set; }
    public int PlanId { get; set; }
    [StringLength(80)] public string PlanName { get; set; } = "";
    public BillingPeriod Period { get; set; }
    public decimal Amount { get; set; }                       // paid by the shop
    [StringLength(3)] public string Currency { get; set; } = "INR";
    public PaymentStatus Status { get; set; } = PaymentStatus.Created;
    [StringLength(60)] public string? RazorpayOrderId { get; set; }
    [StringLength(60)] public string? RazorpayPaymentId { get; set; }
    [StringLength(200)] public string? RazorpaySignature { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAt { get; set; }

    // Set by the main admin: what actually reached the bank account
    public decimal? CreditedAmount { get; set; }
    public DateTime? CreditedOn { get; set; }
    [StringLength(200)] public string? CreditedNote { get; set; }
    [StringLength(120)] public string? CreditedBy { get; set; }
}