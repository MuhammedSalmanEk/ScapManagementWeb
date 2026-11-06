namespace ScrapManagement.Web.Models;

// public enum ShopStatus { Pending = 0, Active = 1, Suspended = 2 }
public enum ShopStatus { Pending = 0, Active = 1, Suspended = 2, Terminated = 3 }
public enum SubscriptionAction { Registered, Activated, Renewed, Suspended, Reactivated, PlanChanged, Terminated }
public enum BillStatus { Draft = 0, Finalized = 1 }

/// <summary>Every stock change is a ledger row. Sales (future module) will add negative rows of type Sale.</summary>
public enum StockMovementType { Purchase = 1, Sale = 2 }
public enum BillingPeriod { Monthly = 1, Yearly = 2 }
/// <summary>What a shop's users may do right now, derived from status + subscription dates.</summary>
public enum ShopAccess
{
    Full,       // active subscription
    ReadOnly,   // subscription expired: can view stock/reports/bills, cannot create or change anything
    Blocked,    // suspended by main admin
    Pending     // registered, waiting for main admin activation
}

// public enum SubscriptionAction { Registered, Activated, Renewed, Suspended, Reactivated, PlanChanged }

public static class AppRoles
{
    public const string MainAdmin = "MainAdmin";
    public const string ShopAdmin = "ShopAdmin";
    public const string Worker = "Worker";
}

public static class AppClaims
{
    public const string ShopId = "shop_id";
    public const string FullName = "full_name";
    public const string Permission = "perm";
}

public static class Permissions
{
    public const string AddProducts = "add-products";
    public const string ManagePurchases = "manage-purchases";
     public const string ManageCategories = "manage-categories";
    public const string ViewReports = "view-reports";
    public const string ViewStock = "view-stock";
     public const string EditProducts = "edit-products";
}

public static class PolicyNames
{
    public const string MainAdmin = "MainAdmin";
    public const string ShopAdmin = "ShopAdmin";
    public const string ShopUser = "ShopUser";
    public const string AddProducts = "AddProducts";
    public const string ManagePurchases = "ManagePurchases";
    public const string ViewReports = "ViewReports";
}
