using System.ComponentModel.DataAnnotations;

namespace ScrapManagement.Web.Models;

public class SubscriptionPlan
{
    public int Id { get; set; }

    [Required, StringLength(80)]
    public string Name { get; set; } = string.Empty;

    [StringLength(300)]
    public string? Description { get; set; }

    // ---- billing options the main admin can switch on/off per plan
    [Display(Name = "Offer monthly billing")]
    public bool OfferMonthly { get; set; } = true;

    [Range(0, 100000000)]
    [Display(Name = "Monthly price")]
    public decimal MonthlyPrice { get; set; }

    [Display(Name = "Offer yearly billing")]
    public bool OfferYearly { get; set; } = true;

    [Range(0, 100000000)]
    [Display(Name = "Yearly price")]
    public decimal YearlyPrice { get; set; }

    // ---- limits and features
    [Range(0, 100)]
    [Display(Name = "Max workers (besides admin)")]
    public int MaxWorkers { get; set; } = 2;

    [Display(Name = "Allow report downloads (CSV)")]
    public bool AllowReportDownloads { get; set; } = true;

    [Display(Name = "Sales module (future)")]
    public bool SalesModuleEnabled { get; set; }

    [Display(Name = "Plan is active")]
    public bool IsActive { get; set; } = true;

    // ---- helpers
    public bool Offers(BillingPeriod period) =>
        period == BillingPeriod.Monthly ? OfferMonthly : OfferYearly;

    public decimal PriceFor(BillingPeriod period) =>
        period == BillingPeriod.Monthly ? MonthlyPrice : YearlyPrice;
}

// using System.ComponentModel.DataAnnotations;

// namespace ScrapManagement.Web.Models;

// public class SubscriptionPlan
// {
//     public int Id { get; set; }

//     [Required, StringLength(80)]
//     public string Name { get; set; } = string.Empty;

//     [StringLength(300)]
//     public string? Description { get; set; }

//     // [Range(0, 100000000)]
//     // [Display(Name = "Yearly price")]
//     // public decimal YearlyPrice { get; set; }
//     // [Range(0, 100000000)]
//     // [Display(Name = "Monthly price")]
//     // public decimal MonthlyPrice { get; set; }

//     // [Range(0, 100000000)]
//     // [Display(Name = "Yearly price")]
//     // public decimal YearlyPrice { get; set; }
//     [Display(Name = "Offer monthly billing")]
//     public bool OfferMonthly { get; set; } = true;

//     [Display(Name = "Offer yearly billing")]
//     public bool OfferYearly { get; set; } = true;

//     public bool Offers(BillingPeriod p) => p == BillingPeriod.Monthly ? OfferMonthly : OfferYearly;

//     [Range(0, 100)]
//     [Display(Name = "Max workers (besides admin)")]
//     public int MaxWorkers { get; set; } = 2;

//     [Display(Name = "Allow report downloads (CSV)")]
//     public bool AllowReportDownloads { get; set; } = true;

//     [Display(Name = "Sales module (future)")]
//     public bool SalesModuleEnabled { get; set; }

//     // public bool IsActive { get; set; } = true;
//      [Display(Name = "Plan is active")]
//     public bool IsActive { get; set; } = true;

//     // public decimal PriceFor(BillingPeriod period) =>
//     //     period == BillingPeriod.Monthly ? MonthlyPrice : YearlyPrice;
// }
