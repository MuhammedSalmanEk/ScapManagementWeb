using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ScrapManagement.Web.Data;
using ScrapManagement.Web.Infrastructure;
using ScrapManagement.Web.Models;
using ScrapManagement.Web.Models.ViewModels;
using ScrapManagement.Web.Services;

namespace ScrapManagement.Web.Controllers;

[Authorize(Policy = PolicyNames.ShopUser)]
public class PurchasesController : Controller
{
    private readonly IPurchaseService _purchases;
    private readonly ApplicationDbContext _db;
    private readonly IClock _clock;

    public PurchasesController(IPurchaseService purchases, ApplicationDbContext db, IClock clock)
    {
        _purchases = purchases;
        _db = db;
        _clock = clock;
    }

    public async Task<IActionResult> Index(PurchaseListViewModel filter, CancellationToken ct)
    {
        filter.Bills = await _purchases.ListAsync(filter, ct);
        return View(filter);
    }

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var bill = await _purchases.GetAsync(id, ct);
        if (bill is null) return NotFound();
        ViewBag.Shop = await _db.Shops.AsNoTracking().FirstAsync(s => s.Id == bill.ShopId, ct);
        return View(bill);
    }

    /// <summary>Printable bill (use the browser's Print / Save as PDF).</summary>
    public async Task<IActionResult> Print(int id, CancellationToken ct)
    {
        var bill = await _purchases.GetAsync(id, ct);
        if (bill is null) return NotFound();
        ViewBag.Shop = await _db.Shops.AsNoTracking().FirstAsync(s => s.Id == bill.ShopId, ct);
        return View(bill);
    }

    public async Task<IActionResult> Download(int id, CancellationToken ct)
    {
        var info = HttpContext.GetShopAccess();
        if (info?.Plan?.AllowReportDownloads != true) return Forbid();

        var bill = await _purchases.GetAsync(id, ct);
        if (bill is null || bill.Status != BillStatus.Finalized) return NotFound();

        var rows = new List<string[]>
        {
            new[] { "Invoice", bill.InvoiceNumber ?? "" },
            new[] { "Date", bill.BillDate.ToString("yyyy-MM-dd") },
            new[] { "Seller", bill.SellerName },
            new[] { "Phone", bill.SellerPhone ?? "" },
            new[] { "Address", bill.SellerAddress ?? "" },
            Array.Empty<string>(),
            new[] { "Product", "Quantity (kg)", "Rate per kg", "Amount" }
        };
        rows.AddRange(bill.Items.Select(i => new[] { i.ProductName, Csv.Num(i.QuantityKg, 3), Csv.Num(i.RatePerKg), Csv.Num(i.LineTotal) }));
        rows.Add(new[] { "Total", Csv.Num(bill.TotalKg, 3), "", Csv.Num(bill.TotalAmount) });

        return File(Csv.Build(rows), "text/csv", $"{bill.InvoiceNumber}.csv");
    }

    // ------------------------------------------------------------------ create / edit

    [Authorize(Policy = PolicyNames.ManagePurchases)]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        var form = new PurchaseBillFormModel { BillDate = _clock.Today, Items = { new PurchaseItemFormModel() } };
        return View("Form", await BuildPageAsync(form, ct));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = PolicyNames.ManagePurchases)]
    public Task<IActionResult> Create([Bind(Prefix = "Form")] PurchaseBillFormModel form, string submitAction, CancellationToken ct) =>
        SaveAsync(form, submitAction, ct);

    [Authorize(Policy = PolicyNames.ManagePurchases)]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var bill = await _purchases.GetAsync(id, ct);
        if (bill is null) return NotFound();
        if (bill.Status != BillStatus.Draft)
        {
            TempData["Error"] = "A finalized bill cannot be edited.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var form = new PurchaseBillFormModel
        {
            Id = bill.Id,
            BillDate = bill.BillDate,
            SellerName = bill.SellerName,
            SellerPhone = bill.SellerPhone,
            SellerAddress = bill.SellerAddress,
            SellerIdNumber = bill.SellerIdNumber,
            Notes = bill.Notes,
            RateType = bill.RateType ?? RateType.Retail,
            Items = bill.Items.Select(i => new PurchaseItemFormModel { ProductId = i.ProductId, QuantityKg = i.QuantityKg, RatePerKg = i.RatePerKg }).ToList()
        };
        if (form.Items.Count == 0) form.Items.Add(new PurchaseItemFormModel());
        return View("Form", await BuildPageAsync(form, ct));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = PolicyNames.ManagePurchases)]
    public Task<IActionResult> Edit(int id, [Bind(Prefix = "Form")] PurchaseBillFormModel form, string submitAction, CancellationToken ct)
    {
        form.Id = id;
        return SaveAsync(form, submitAction, ct);
    }

    private async Task<IActionResult> SaveAsync(PurchaseBillFormModel form, string submitAction, CancellationToken ct)
    {
        var canAdd = User.HasPermission(Permissions.AddProducts);
        ServiceResult<int> saved;
        if (!ModelState.IsValid) saved = ServiceResult<int>.Fail(); // field errors are already in ModelState
        else saved = await _purchases.SaveDraftAsync(form, User.ToCurrentUser(), canAdd, ct);

        if (!ModelState.IsValid || !saved.Succeeded)
        {
            foreach (var e in saved.Errors) ModelState.AddModelError("", e);
            if (form.Items.Count == 0) form.Items.Add(new PurchaseItemFormModel());
            return View("Form", await BuildPageAsync(form, ct));
        }

        var id = saved.Value;
        if (submitAction == "finalize")
        {
            var fin = await _purchases.FinalizeAsync(id, User.ToCurrentUser(), ct);
            if (!fin.Succeeded)
            {
                TempData["Error"] = fin.FirstError;
                return RedirectToAction(nameof(Details), new { id });
            }
            TempData["Success"] = $"Bill {fin.Value!.InvoiceNumber} finalized and stock updated.";
            return RedirectToAction(nameof(Details), new { id });
        }

        TempData["Success"] = "Draft saved. Stock changes only when you finalize the bill.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = PolicyNames.ManagePurchases)]
    public async Task<IActionResult> Finalize(int id, CancellationToken ct)
    {
        var result = await _purchases.FinalizeAsync(id, User.ToCurrentUser(), ct);
        if (result.Succeeded) TempData["Success"] = $"Bill {result.Value!.InvoiceNumber} finalized and stock updated.";
        else TempData["Error"] = result.FirstError;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = PolicyNames.ManagePurchases)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var result = await _purchases.DeleteDraftAsync(id, ct);
        if (result.Succeeded) TempData["Success"] = "Draft deleted.";
        else TempData["Error"] = result.FirstError;
        return RedirectToAction(nameof(Index));
    }

    private async Task<PurchaseFormPageModel> BuildPageAsync(PurchaseBillFormModel form, CancellationToken ct) => new()
    {
        Form = form,
        CanAddProducts = User.HasPermission(Permissions.AddProducts),
        Products = await _db.Products.AsNoTracking().Where(p => p.IsActive).OrderBy(p => p.Name)
            // .Select(p => new ProductOption { Id = p.Id, Name = p.Name, Rate = p.DefaultRatePerKg, StockKg = p.StockKg }).ToListAsync(ct),
            .Select(p => new ProductOption { Id = p.Id, Name = p.Name, Rate = p.DefaultRatePerKg, WholesaleRate = p.WholesaleRatePerKg, StockKg = p.StockKg }).ToListAsync(ct),
        Categories = await _db.Categories.AsNoTracking().Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync(ct)
    };

    public async Task<IActionResult> Receipt(int id, int? width = null, bool? auto = null, CancellationToken ct = default)
    {
        var bill = await _purchases.GetAsync(id, ct);
        if (bill is null) return NotFound();
        var shop = await _db.Shops.AsNoTracking().FirstAsync(s => s.Id == bill.ShopId, ct);
        ViewBag.Shop = shop;
        ViewBag.Width = (width ?? shop.ReceiptPaperWidthMm) == 58 ? 58 : 80;
        ViewBag.Auto = auto ?? shop.AutoPrintReceipt;
        return View(bill);
    }
}
