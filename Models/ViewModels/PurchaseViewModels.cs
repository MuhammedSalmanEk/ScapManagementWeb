using System.ComponentModel.DataAnnotations;

namespace ScrapManagement.Web.Models.ViewModels;

public class PurchaseBillFormModel
{
    public int? Id { get; set; }

    [Required, StringLength(150), Display(Name = "Seller name")]
    public string SellerName { get; set; } = string.Empty;

    [StringLength(30), Display(Name = "Seller phone")]
    public string? SellerPhone { get; set; }

    [StringLength(400), Display(Name = "Seller address")]
    public string? SellerAddress { get; set; }

    [StringLength(60), Display(Name = "Seller ID number")]
    public string? SellerIdNumber { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    [Required, Display(Name = "Bill date")]
    public DateOnly? BillDate { get; set; }

    /// <summary>Retail or Wholesale price list used to fill the rates.</summary>
    [Display(Name = "Rate type")]
    public RateType RateType { get; set; } = RateType.Retail;

    public List<PurchaseItemFormModel> Items { get; set; } = new();
}

public class PurchaseItemFormModel
{
    /// <summary>0 means "create a new product from NewProductName".</summary>
    public int? ProductId { get; set; }
    [StringLength(100)] public string? NewProductName { get; set; }
    public int? NewCategoryId { get; set; }
    public decimal? QuantityKg { get; set; }
    public decimal? RatePerKg { get; set; }

    /// <summary>New products only: the other price list's rate (optional).</summary>
    public decimal? NewOtherRatePerKg { get; set; }
}

public class ProductOption
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    /// <summary>Retail rate.</summary>
    public decimal Rate { get; set; }
    public decimal WholesaleRate { get; set; }
    public decimal StockKg { get; set; }
}

public class PurchaseFormPageModel
{
    public PurchaseBillFormModel Form { get; set; } = new();
    public List<ProductOption> Products { get; set; } = new();
    public List<Category> Categories { get; set; } = new();
    public bool CanAddProducts { get; set; }
}

public class PurchaseListViewModel
{
    public List<PurchaseBill> Bills { get; set; } = new();
    public string? Search { get; set; }
    public BillStatus? Status { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }

    /// <summary>Set by the server (never from the query string) to show a worker only their own bills.</summary>
    public string? OnlyCreatedById { get; set; }
}

// using System.ComponentModel.DataAnnotations;

// namespace ScrapManagement.Web.Models.ViewModels;

// public class PurchaseBillFormModel
// {
//     public int? Id { get; set; }

//     [Required, StringLength(150), Display(Name = "Seller name")]
//     public string SellerName { get; set; } = string.Empty;

//     [StringLength(30), Display(Name = "Seller phone")]
//     public string? SellerPhone { get; set; }

//     [StringLength(400), Display(Name = "Seller address")]
//     public string? SellerAddress { get; set; }

//     [StringLength(60), Display(Name = "Seller ID number")]
//     public string? SellerIdNumber { get; set; }

//     [StringLength(500)]
//     public string? Notes { get; set; }

//     [Required, Display(Name = "Bill date")]
//     public DateOnly? BillDate { get; set; }

//     /// <summary>Retail or Wholesale price list used to fill the rates.</summary>
//     [Display(Name = "Rate type")]
//     public RateType RateType { get; set; } = RateType.Retail;

//     public List<PurchaseItemFormModel> Items { get; set; } = new();
// }

// public class PurchaseItemFormModel
// {
//     /// <summary>0 means "create a new product from NewProductName".</summary>
//     public int? ProductId { get; set; }
//     [StringLength(100)] public string? NewProductName { get; set; }
//     public int? NewCategoryId { get; set; }
//     public decimal? QuantityKg { get; set; }
//     public decimal? RatePerKg { get; set; }
// }

// public class ProductOption
// {
//     public int Id { get; set; }
//     public string Name { get; set; } = "";
//     /// <summary>Retail rate.</summary>
//     public decimal Rate { get; set; }
//     public decimal WholesaleRate { get; set; }
//     public decimal StockKg { get; set; }
// }

// public class PurchaseFormPageModel
// {
//     public PurchaseBillFormModel Form { get; set; } = new();
//     public List<ProductOption> Products { get; set; } = new();
//     public List<Category> Categories { get; set; } = new();
//     public bool CanAddProducts { get; set; }
// }

// public class PurchaseListViewModel
// {
//     public List<PurchaseBill> Bills { get; set; } = new();
//     public string? Search { get; set; }
//     public BillStatus? Status { get; set; }
//     public DateOnly? From { get; set; }
//     public DateOnly? To { get; set; }
// }