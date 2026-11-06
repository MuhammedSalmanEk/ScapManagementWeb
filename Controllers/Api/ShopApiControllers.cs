using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ScrapManagement.Web.Data;
using ScrapManagement.Web.Infrastructure;
using ScrapManagement.Web.Models;
using ScrapManagement.Web.Services;

namespace ScrapManagement.Web.Controllers.Api;

// JSON endpoints for the same shop data (cookie authentication, same tenant isolation as the MVC pages).
// They are what a future mobile app or the Sales module can build on.

[ApiController]
[Authorize(Policy = PolicyNames.ShopUser)]
[Route("api/products")]
public class ProductsApiController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    public ProductsApiController(ApplicationDbContext db) => _db = db;

    /// <summary>GET /api/products?term=iron&amp;take=20</summary>
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string? term, [FromQuery] int take = 50, CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 200);
        var q = _db.Products.AsNoTracking().Where(p => p.IsActive);
        if (!string.IsNullOrWhiteSpace(term))
        {
            var t = term.Trim().ToLower();
            q = q.Where(p => p.Name.ToLower().Contains(t));
        }
        var items = await q.OrderBy(p => p.Name).Take(take)
            .Select(p => new { p.Id, p.Name, Category = p.Category != null ? p.Category.Name : null, p.DefaultRatePerKg, p.StockKg })
            .ToListAsync(ct);
        return Ok(items);
    }
}

[ApiController]
[Authorize(Policy = PolicyNames.ShopUser)]
[Route("api/stock")]
public class StockApiController : ControllerBase
{
    private readonly IReportService _reports;
    public StockApiController(IReportService reports) => _reports = reports;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) => Ok(await _reports.GetStockAsync(false, ct));
}

[ApiController]
[Authorize(Policy = PolicyNames.ShopUser)]
[Route("api/purchases")]
public class PurchasesApiController : ControllerBase
{
    private readonly IPurchaseService _purchases;
    private readonly IReportService _reports;
    private readonly IClock _clock;

    public PurchasesApiController(IPurchaseService purchases, IReportService reports, IClock clock)
    {
        _purchases = purchases;
        _reports = reports;
        _clock = clock;
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var bill = await _purchases.GetAsync(id, ct);
        if (bill is null) return NotFound();
        return Ok(new
        {
            bill.Id, bill.InvoiceNumber, Status = bill.Status.ToString(), bill.BillDate, bill.SellerName, bill.SellerPhone,
            bill.TotalKg, bill.TotalAmount, bill.FinalizedAt,
            Items = bill.Items.Select(i => new { i.ProductId, i.ProductName, i.QuantityKg, i.RatePerKg, i.LineTotal })
        });
    }

    /// <summary>GET /api/purchases/daily?date=2026-10-23 (needs the "view reports" permission)</summary>
    [HttpGet("daily")]
    [Authorize(Policy = PolicyNames.ViewReports)]
    public async Task<IActionResult> Daily([FromQuery] DateOnly? date, CancellationToken ct) =>
        Ok(await _reports.GetDailyPurchasesAsync(date ?? _clock.Today, ct));
}
