using System.ComponentModel.DataAnnotations;

namespace ScrapManagement.Web.Models;

public class Shop
{
    public int Id { get; set; }

    [Required, StringLength(150)]
    [Display(Name = "Shop name")]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(120)]
    [Display(Name = "Owner name")]
    public string OwnerName { get; set; } = string.Empty;

    [StringLength(30)]
    public string? Phone { get; set; }

    [StringLength(400)]
    public string? Address { get; set; }

    [StringLength(60)]
    [Display(Name = "Tax / licence number")]
    public string? TaxNumber { get; set; }

    [StringLength(300)]
    [Display(Name = "Bill footer note")]
    public string? InvoiceFooter { get; set; }

    public ShopStatus Status { get; set; } = ShopStatus.Pending;

    public int PlanId { get; set; }
    public SubscriptionPlan? Plan { get; set; }

    public DateTime? SubscriptionStartsOn { get; set; }
    public DateTime? SubscriptionEndsOn { get; set; }

    /// <summary>Main admin can override the plan's worker limit for one shop.</summary>
    public int? WorkerLimitOverride { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Display(Name = "Receipt paper")]
    public int ReceiptPaperWidthMm { get; set; } = 80;   // 80, 58, or 0 = A4 / normal printer

    [Display(Name = "Open print dialog automatically")]
    public bool AutoPrintReceipt { get; set; }
    public BillingPeriod BillingPeriod { get; set; } = BillingPeriod.Yearly;
}

/// <summary>Audit trail of subscription changes made by the main admin.</summary>
public class SubscriptionEvent
{
    public int Id { get; set; }
    public int ShopId { get; set; }
    public Shop? Shop { get; set; }
    public SubscriptionAction Action { get; set; }
    public int? PlanId { get; set; }
    [StringLength(80)] public string? PlanName { get; set; }
    public decimal? Price { get; set; }
    public DateTime? StartsOn { get; set; }
    public DateTime? EndsOn { get; set; }
    [StringLength(200)] public string? Note { get; set; }
    [StringLength(120)] public string? PerformedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
