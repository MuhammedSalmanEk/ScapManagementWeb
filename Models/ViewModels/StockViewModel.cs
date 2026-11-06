using Microsoft.AspNetCore.Mvc.Rendering;
using ScrapManagement.Web.Models;
public class StockListViewModel
{
    public List<Product> Products { get; set; } = new();
    public string? Search { get; set; }
    public int? CategoryId { get; set; }
    public decimal LowBelow { get; set; } = 10;
    public bool ShowInactive { get; set; }
    public string Sort { get; set; } = "low";
    public decimal TotalKg { get; set; }
    public int LowCount { get; set; }
    public int ZeroCount { get; set; }
    public SelectList Categories { get; set; } = null!;
}

public record StockMovementRow(DateTime At, StockMovementType Type, decimal QuantityKg,
    int? BillId, string? InvoiceNumber, string? SellerName, decimal? RatePerKg);

public class StockDetailsViewModel
{
    public Product Product { get; set; } = null!;
    public decimal TotalInKg { get; set; }
    public int MovementCount { get; set; }
    public List<StockMovementRow> Movements { get; set; } = new();
}