using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ScrapManagement.Web.Data;
using ScrapManagement.Web.Infrastructure;
using ScrapManagement.Web.Models;
using ScrapManagement.Web.Services;

namespace ScrapManagement.Web.Controllers;

[Authorize(Policy = PolicyNames.ShopUser)]
public class ReportsController : Controller
{
    private readonly IReportService _reports;
    private readonly IClock _clock;
    private readonly ApplicationDbContext _db;

    public ReportsController(IReportService reports, IClock clock, ApplicationDbContext db)
    {
        _reports = reports;
        _clock = clock;
        _db = db;
    }

    private bool CanDownload() => HttpContext.GetShopAccess()?.Plan?.AllowReportDownloads == true;

    // ---------- Stock ----------
    public async Task<IActionResult> Stock(bool showInactive = false, CancellationToken ct = default)
    {
        if (!HttpContext.CanViewStock()) return Forbid();
        ViewBag.ShowInactive = showInactive;
        return View(await _reports.GetStockAsync(showInactive, ct));
    }

    public async Task<IActionResult> StockPrint(string paper = "80", CancellationToken ct = default)
    {
        if (!HttpContext.CanViewStock()) return Forbid();
        ViewBag.Shop = await CurrentShopAsync(ct);
        ViewBag.Paper = NormalizePaper(paper);
        return View(await _reports.GetStockAsync(false, ct));
    }

    public async Task<IActionResult> StockCsv(CancellationToken ct)
    {
        if (!HttpContext.CanViewStock() || !CanDownload()) return Forbid();
        var report = await _reports.GetStockAsync(false, ct);
        var rows = new List<string[]> { new[] { "Product", "Category", "Stock (kg)" } };
        rows.AddRange(report.Rows.Select(r => new[] { r.Name, r.Category ?? "", Csv.Num(r.StockKg, 3) }));
        rows.Add(new[] { "Total", "", Csv.Num(report.TotalKg, 3) });
        return File(Csv.Build(rows), "text/csv", $"stock-{_clock.Today:yyyy-MM-dd}.csv");
    }

    // ---------- Purchases (single date or range) ----------
    public async Task<IActionResult> DailyPurchases(DateOnly? from, DateOnly? to, DateOnly? date, CancellationToken ct = default)
    {
        if (!HttpContext.CanViewAllBills()) return Forbid();
        var f = from ?? date ?? _clock.Today;
        var t = to ?? date ?? f;
        return View(await _reports.GetPurchasesRangeAsync(f, t, ct));
    }

    public async Task<IActionResult> PurchasesPrint(DateOnly? from, DateOnly? to, string paper = "80", CancellationToken ct = default)
    {
        if (!HttpContext.CanViewAllBills()) return Forbid();
        var f = from ?? _clock.Today;
        var t = to ?? f;
        ViewBag.Shop = await CurrentShopAsync(ct);
        ViewBag.Paper = NormalizePaper(paper);
        return View(await _reports.GetPurchasesRangeAsync(f, t, ct));
    }

    public async Task<IActionResult> DailyPurchasesCsv(DateOnly? from, DateOnly? to, DateOnly? date, CancellationToken ct)
    {
        if (!HttpContext.CanViewAllBills() || !CanDownload()) return Forbid();
        var f = from ?? date ?? _clock.Today;
        var t = to ?? date ?? f;
        var r = await _reports.GetPurchasesRangeAsync(f, t, ct);

        var rows = new List<string[]> { new[] { "Date", "Invoice", "Time", "Seller", "Kg", "Amount" } };
        rows.AddRange(r.Bills.Select(b => new[] { b.BillDate.ToString("yyyy-MM-dd"), b.InvoiceNumber, b.FinalizedAtLocal.ToString("HH:mm"), b.Seller, Csv.Num(b.Kg, 3), Csv.Num(b.Amount) }));
        rows.Add(new[] { "Total", "", "", "", Csv.Num(r.TotalKg, 3), Csv.Num(r.TotalAmount) });
        rows.Add(Array.Empty<string>());
        rows.Add(new[] { "Day", "Bills", "Kg", "Amount" });
        rows.AddRange(r.Days.Select(d => new[] { d.Date.ToString("yyyy-MM-dd"), d.Bills.ToString(), Csv.Num(d.Kg, 3), Csv.Num(d.Amount) }));
        rows.Add(Array.Empty<string>());
        rows.Add(new[] { "Product", "Kg", "Avg rate", "Amount" });
        rows.AddRange(r.Products.Select(p => new[] { p.Product, Csv.Num(p.Kg, 3), Csv.Num(p.AvgRate), Csv.Num(p.Amount) }));
        var name = f == t ? $"purchases-{f:yyyy-MM-dd}.csv" : $"purchases-{f:yyyy-MM-dd}_to_{t:yyyy-MM-dd}.csv";
        return File(Csv.Build(rows), "text/csv", name);
    }

    private static string NormalizePaper(string? p) => p switch { "58" => "58", "a4" => "a4", _ => "80" };

    private async Task<Shop?> CurrentShopAsync(CancellationToken ct)
    {
        var id = int.TryParse(User.FindFirst(AppClaims.ShopId)?.Value, out var v) ? v : 0;
        return await _db.Shops.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);
    }
}
// using Microsoft.AspNetCore.Authorization;
// using Microsoft.AspNetCore.Mvc;
// using ScrapManagement.Web.Infrastructure;
// using ScrapManagement.Web.Models;
// using ScrapManagement.Web.Services;

// namespace ScrapManagement.Web.Controllers;

// [Authorize(Policy = PolicyNames.ViewReports)]
// public class ReportsController : Controller
// {
//     private readonly IReportService _reports;
//     private readonly IClock _clock;

//     public ReportsController(IReportService reports, IClock clock)
//     {
//         _reports = reports;
//         _clock = clock;
//     }

//     public async Task<IActionResult> Stock(bool showInactive = false, CancellationToken ct = default)
//     {
//         ViewBag.ShowInactive = showInactive;
//         return View(await _reports.GetStockAsync(showInactive, ct));
//     }

//     public async Task<IActionResult> DailyPurchases(DateOnly? date, CancellationToken ct = default) =>
//         View(await _reports.GetDailyPurchasesAsync(date ?? _clock.Today, ct));

//     public async Task<IActionResult> StockCsv(CancellationToken ct)
//     {
//         if (!CanDownload()) return Forbid();
//         var report = await _reports.GetStockAsync(false, ct);
//         var rows = new List<string[]> { new[] { "Product", "Category", "Stock (kg)" } };
//         rows.AddRange(report.Rows.Select(r => new[] { r.Name, r.Category ?? "", Csv.Num(r.StockKg, 3) }));
//         rows.Add(new[] { "Total", "", Csv.Num(report.TotalKg, 3) });
//         return File(Csv.Build(rows), "text/csv", $"stock-{_clock.Today:yyyy-MM-dd}.csv");
//     }

//     public async Task<IActionResult> DailyPurchasesCsv(DateOnly? date, CancellationToken ct)
//     {
//         if (!CanDownload()) return Forbid();
//         var d = date ?? _clock.Today;
//         var r = await _reports.GetDailyPurchasesAsync(d, ct);

//         var rows = new List<string[]> { new[] { "Invoice", "Time", "Seller", "Kg", "Amount" } };
//         rows.AddRange(r.Bills.Select(b => new[] { b.InvoiceNumber, b.FinalizedAtLocal.ToString("HH:mm"), b.Seller, Csv.Num(b.Kg, 3), Csv.Num(b.Amount) }));
//         rows.Add(new[] { "Total", "", "", Csv.Num(r.TotalKg, 3), Csv.Num(r.TotalAmount) });
//         rows.Add(Array.Empty<string>());
//         rows.Add(new[] { "Product", "Kg", "Avg rate", "Amount" });
//         rows.AddRange(r.Products.Select(p => new[] { p.Product, Csv.Num(p.Kg, 3), Csv.Num(p.AvgRate), Csv.Num(p.Amount) }));
//         return File(Csv.Build(rows), "text/csv", $"purchases-{d:yyyy-MM-dd}.csv");
//     }

//     private bool CanDownload() => HttpContext.GetShopAccess()?.Plan?.AllowReportDownloads == true;
// }
