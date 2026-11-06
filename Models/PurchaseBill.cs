using System.ComponentModel.DataAnnotations;

namespace ScrapManagement.Web.Models;

public class PurchaseBill : ITenantEntity
{
    public int Id { get; set; }
    public int ShopId { get; set; }

    /// <summary>Assigned only when the bill is finalized, e.g. PUR-2026-000012.</summary>
    [StringLength(30)]
    public string? InvoiceNumber { get; set; }

    public BillStatus Status { get; set; } = BillStatus.Draft;

    public DateOnly BillDate { get; set; }

    /// <summary>Price list chosen for this bill (null on bills created before this feature).</summary>
    public RateType? RateType { get; set; }

    [Required, StringLength(150)]
    public string SellerName { get; set; } = string.Empty;
    [StringLength(30)] public string? SellerPhone { get; set; }
    [StringLength(400)] public string? SellerAddress { get; set; }
    [StringLength(60)] public string? SellerIdNumber { get; set; }
    [StringLength(500)] public string? Notes { get; set; }

    public decimal TotalKg { get; set; }
    public decimal TotalAmount { get; set; }

    public string? CreatedById { get; set; }
    [StringLength(120)] public string? CreatedByName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? FinalizedById { get; set; }
    [StringLength(120)] public string? FinalizedByName { get; set; }
    public DateTime? FinalizedAt { get; set; }

    public List<PurchaseBillItem> Items { get; set; } = new();
}

public class PurchaseBillItem : ITenantEntity
{
    public int Id { get; set; }
    public int ShopId { get; set; }

    public int PurchaseBillId { get; set; }
    public PurchaseBill? PurchaseBill { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    [StringLength(100)]
    public string ProductName { get; set; } = string.Empty;

    public decimal QuantityKg { get; set; }
    public decimal RatePerKg { get; set; }
    public decimal LineTotal { get; set; }
}

/// <summary>Stock ledger. The unique PurchaseBillItemId index guarantees a bill line can add stock only once.</summary>
public class StockMovement : ITenantEntity
{
    public long Id { get; set; }
    public int ShopId { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public StockMovementType Type { get; set; }

    /// <summary>Positive = stock in, negative = stock out.</summary>
    public decimal QuantityKg { get; set; }

    public int? PurchaseBillId { get; set; }
    public int? PurchaseBillItemId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Per-shop, per-year invoice counter. Incremented atomically with INSERT .. ON CONFLICT.</summary>
public class InvoiceSequence
{
    public int ShopId { get; set; }
    public int Year { get; set; }
    public int LastNumber { get; set; }
}
// using System.ComponentModel.DataAnnotations;

// namespace ScrapManagement.Web.Models;

// public class PurchaseBill : ITenantEntity
// {
//     public int Id { get; set; }
//     public int ShopId { get; set; }

//     /// <summary>Assigned only when the bill is finalized, e.g. PUR-2026-000012.</summary>
//     [StringLength(30)]
//     public string? InvoiceNumber { get; set; }

//     public BillStatus Status { get; set; } = BillStatus.Draft;

//     public DateOnly BillDate { get; set; }

//     [Required, StringLength(150)]
//     public string SellerName { get; set; } = string.Empty;
//     [StringLength(30)] public string? SellerPhone { get; set; }
//     [StringLength(400)] public string? SellerAddress { get; set; }
//     [StringLength(60)] public string? SellerIdNumber { get; set; }
//     [StringLength(500)] public string? Notes { get; set; }

//     public decimal TotalKg { get; set; }
//     public decimal TotalAmount { get; set; }

//     public string? CreatedById { get; set; }
//     [StringLength(120)] public string? CreatedByName { get; set; }
//     public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

//     public string? FinalizedById { get; set; }
//     [StringLength(120)] public string? FinalizedByName { get; set; }
//     public DateTime? FinalizedAt { get; set; }

//     public List<PurchaseBillItem> Items { get; set; } = new();
// }

// public class PurchaseBillItem : ITenantEntity
// {
//     public int Id { get; set; }
//     public int ShopId { get; set; }

//     public int PurchaseBillId { get; set; }
//     public PurchaseBill? PurchaseBill { get; set; }

//     public int ProductId { get; set; }
//     public Product? Product { get; set; }

//     [StringLength(100)]
//     public string ProductName { get; set; } = string.Empty;

//     public decimal QuantityKg { get; set; }
//     public decimal RatePerKg { get; set; }
//     public decimal LineTotal { get; set; }
// }

// /// <summary>Stock ledger. The unique PurchaseBillItemId index guarantees a bill line can add stock only once.</summary>
// public class StockMovement : ITenantEntity
// {
//     public long Id { get; set; }
//     public int ShopId { get; set; }

//     public int ProductId { get; set; }
//     public Product? Product { get; set; }

//     public StockMovementType Type { get; set; }

//     /// <summary>Positive = stock in, negative = stock out.</summary>
//     public decimal QuantityKg { get; set; }

//     public int? PurchaseBillId { get; set; }
//     public int? PurchaseBillItemId { get; set; }

//     public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
// }

// /// <summary>Per-shop, per-year invoice counter. Incremented atomically with INSERT .. ON CONFLICT.</summary>
// public class InvoiceSequence
// {
//     public int ShopId { get; set; }
//     public int Year { get; set; }
//     public int LastNumber { get; set; }
// }
