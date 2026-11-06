using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ScrapManagement.Web.Data;
using ScrapManagement.Web.Infrastructure;
using ScrapManagement.Web.Models;

namespace ScrapManagement.Web.Controllers;


/// <summary>Stock overview and per-product stock history. Visible to the shop admin and to workers allowed to view reports.</summary>
[Authorize(Policy = PolicyNames.ShopUser)]
public class StockController : Controller
{
    private readonly ApplicationDbContext _db;
    public StockController(ApplicationDbContext db) => _db = db;

    public async Task<IActionResult> Index(string? search, int? categoryId, decimal lowBelow = 10,
        bool showInactive = false, string sort = "low", CancellationToken ct = default)
    {
        if (!HttpContext.CanViewStock()) return Forbid();
        if (lowBelow < 0) lowBelow = 0;

        var q = _db.Products.AsNoTracking().Include(p => p.Category).AsQueryable();
        if (!showInactive) q = q.Where(p => p.IsActive);
        if (categoryId.HasValue) q = q.Where(p => p.CategoryId == categoryId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var t = search.Trim().ToLower();
            q = q.Where(p => p.Name.ToLower().Contains(t));
        }

        q = sort switch
        {
            "high" => q.OrderByDescending(p => p.StockKg).ThenBy(p => p.Name),
            "name" => q.OrderBy(p => p.Name),
            _ => q.OrderBy(p => p.StockKg).ThenBy(p => p.Name)
        };
        var products = await q.ToListAsync(ct);

        return View(new StockListViewModel
        {
            Products = products,
            Search = search,
            CategoryId = categoryId,
            LowBelow = lowBelow,
            ShowInactive = showInactive,
            Sort = sort,
            TotalKg = products.Sum(p => p.StockKg),
            LowCount = products.Count(p => p.StockKg > 0 && p.StockKg < lowBelow),
            ZeroCount = products.Count(p => p.StockKg <= 0),
            Categories = new SelectList(await _db.Categories.AsNoTracking().OrderBy(c => c.Name).ToListAsync(ct), "Id", "Name", categoryId)
        });
    }

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        if (!HttpContext.CanViewStock()) return Forbid();

        var product = await _db.Products.AsNoTracking().Include(p => p.Category).FirstOrDefaultAsync(p => p.Id == id, ct);
        if (product is null) return NotFound();

        var movements = await (
            from m in _db.StockMovements.AsNoTracking()
            where m.ProductId == id
            join b in _db.PurchaseBills.AsNoTracking() on m.PurchaseBillId equals b.Id into bj
            from b in bj.DefaultIfEmpty()
            join i in _db.PurchaseBillItems.AsNoTracking() on m.PurchaseBillItemId equals i.Id into ij
            from i in ij.DefaultIfEmpty()
            orderby m.CreatedAt descending, m.Id descending
            select new StockMovementRow(
                m.CreatedAt, m.Type, m.QuantityKg,
                b != null ? b.Id : (int?)null,
                b != null ? b.InvoiceNumber : null,
                b != null ? b.SellerName : null,
                i != null ? i.RatePerKg : (decimal?)null)
        ).Take(300).ToListAsync(ct);

        var totalIn = await _db.StockMovements.Where(m => m.ProductId == id && m.QuantityKg > 0).SumAsync(m => m.QuantityKg, ct);
        var count = await _db.StockMovements.CountAsync(m => m.ProductId == id, ct);

        return View(new StockDetailsViewModel { Product = product, TotalInKg = totalIn, MovementCount = count, Movements = movements });
    }
}