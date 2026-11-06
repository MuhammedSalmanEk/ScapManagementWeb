using System.ComponentModel.DataAnnotations;

namespace ScrapManagement.Web.Models.ViewModels;

public class ShopUsageRow
{
    public Shop Shop { get; set; } = null!;
    public ShopAccess Access { get; set; }
    public int? DaysLeft { get; set; }
    public int WorkerCount { get; set; }
    public int WorkerLimit { get; set; }
    public int ProductCount { get; set; }
    public int FinalizedBillCount { get; set; }
    public decimal TotalPurchaseAmount { get; set; }
    public decimal TotalStockKg { get; set; }
    public DateTime? LastBillAt { get; set; }
}

public class AdminDashboardViewModel
{
    public int Pending { get; set; }
    public int Active { get; set; }
    public int Expired { get; set; }
    public int Suspended { get; set; }
    public List<ShopUsageRow> ExpiringSoon { get; set; } = new();
    public List<ShopUsageRow> PendingShops { get; set; } = new();
}

public class AdminShopsIndexViewModel
{
    public List<ShopUsageRow> Rows { get; set; } = new();
    public string? Search { get; set; }
    public string? Filter { get; set; }
}

public class AdminShopDetailsViewModel
{
    public ShopUsageRow Usage { get; set; } = null!;
    public List<SubscriptionPlan> Plans { get; set; } = new();
    public List<SubscriptionEvent> History { get; set; } = new();
    public List<ApplicationUser> Users { get; set; } = new();
}

public class WorkerFormViewModel
{
    public string? Id { get; set; }

    [Required, StringLength(120), Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress, Display(Name = "Login email")]
    public string Email { get; set; } = string.Empty;

    /// <summary>Required when creating; optional (reset) when editing.</summary>
    [StringLength(100, MinimumLength = 8), DataType(DataType.Password)]
    public string? Password { get; set; }

    [Display(Name = "Can add new products")]
    public bool CanAddProducts { get; set; }

    [Display(Name = "Can create purchase bills")]
    public bool CanManagePurchases { get; set; } = true;

    [Display(Name = "Can view reports")]
    public bool CanViewReports { get; set; } = true;

    [Display(Name = "Account active")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "Can view stock")]
    public bool CanViewStock { get; set; }
    [Display(Name = "Can edit products")]
    public bool CanEditProducts { get; set; }
    [Display(Name = "Can manage categories")]
    public bool CanManageCategories { get; set; }
}

public class WorkersIndexViewModel
{
    public List<ApplicationUser> Workers { get; set; } = new();
    public int Limit { get; set; }
    public int ActiveCount { get; set; }
}

public class ShopDashboardViewModel
{
    public int ProductCount { get; set; }
    public decimal TotalStockKg { get; set; }
    public int TodayBills { get; set; }
    public decimal TodayKg { get; set; }
    public decimal TodayAmount { get; set; }
    public int DraftCount { get; set; }
    public List<PurchaseBill> RecentBills { get; set; } = new();
    public List<Product> LowestStock { get; set; } = new();
}
public class AdminPaymentsViewModel
{
    public List<Payment> Rows { get; set; } = new();
    public string Status { get; set; } = "paid";
    public int? ShopId { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal TotalCredited { get; set; }
    public decimal NotCredited => TotalPaid - TotalCredited;
}