using System.ComponentModel.DataAnnotations;

namespace ScrapManagement.Web.Models;

public class Product : ITenantEntity
{
    public int Id { get; set; }
    public int ShopId { get; set; }

    public int? CategoryId { get; set; }
    public Category? Category { get; set; }

    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    /// <summary>Retail rate per kg. (Column keeps its old name so existing data stays valid.)</summary>
    [Range(typeof(decimal), "0", "999999999")]
    [Display(Name = "Retail rate per kg")]
    public decimal DefaultRatePerKg { get; set; }

    /// <summary>Wholesale rate per kg. 0 means "not set", and the retail rate is used instead.</summary>
    [Range(typeof(decimal), "0", "999999999")]
    [Display(Name = "Wholesale rate per kg")]
    public decimal WholesaleRatePerKg { get; set; }

    /// <summary>Current stock in kg. Only changed by finalized bills (via StockMovement ledger rows).</summary>
    [Display(Name = "Stock (kg)")]
    public decimal StockKg { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public decimal RateFor(RateType type) =>
        type == RateType.Wholesale && WholesaleRatePerKg > 0 ? WholesaleRatePerKg : DefaultRatePerKg;
}



// using System.ComponentModel.DataAnnotations;

// namespace ScrapManagement.Web.Models;

// public class Product : ITenantEntity
// {
//     public int Id { get; set; }
//     public int ShopId { get; set; }

//     public int? CategoryId { get; set; }
//     public Category? Category { get; set; }

//     [Required, StringLength(100)]
//     public string Name { get; set; } = string.Empty;

//     [StringLength(500)]
//     public string? Description { get; set; }

//     /// <summary>Suggested rate when buying; every bill line still stores its own rate.</summary>
//     [Range(typeof(decimal), "0", "999999999")]
//     [Display(Name = "Default rate per kg")]
//     public decimal DefaultRatePerKg { get; set; }

//     /// <summary>Current stock in kg. Only changed by finalized bills (via StockMovement ledger rows).</summary>
//     [Display(Name = "Stock (kg)")]
//     public decimal StockKg { get; set; }

//     public bool IsActive { get; set; } = true;

//     public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
// }
