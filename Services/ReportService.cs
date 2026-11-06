using Microsoft.EntityFrameworkCore;
using ScrapManagement.Web.Data;
using ScrapManagement.Web.Infrastructure;
using ScrapManagement.Web.Models;
using ScrapManagement.Web.Models.ViewModels;

namespace ScrapManagement.Web.Services;

public record StockRow(int ProductId, string Name, string? Category, decimal StockKg, decimal DefaultRate, bool IsActive);
public record StockReport(IReadOnlyList<StockRow> Rows, decimal TotalKg);
public record DailyBillRow(int Id, string InvoiceNumber, DateTime FinalizedAtLocal, string Seller, decimal Kg, decimal Amount);
public record DailyProductRow(string Product, decimal Kg, decimal Amount, decimal AvgRate);
public record DailyPurchaseReport(DateOnly Date, IReadOnlyList<DailyBillRow> Bills, IReadOnlyList<DailyProductRow> Products, decimal TotalKg, decimal TotalAmount);

public record RangeBillRow(int Id, string InvoiceNumber, DateOnly BillDate, DateTime FinalizedAtLocal, string Seller, decimal Kg, decimal Amount);
public record DayTotalRow(DateOnly Date, int Bills, decimal Kg, decimal Amount);
public record PurchaseRangeReport(DateOnly From, DateOnly To, IReadOnlyList<RangeBillRow> Bills, IReadOnlyList<DayTotalRow> Days, IReadOnlyList<DailyProductRow> Products, decimal TotalKg, decimal TotalAmount);

public interface IReportService
{
    Task<StockReport> GetStockAsync(bool includeInactive, CancellationToken ct = default);
    Task<DailyPurchaseReport> GetDailyPurchasesAsync(DateOnly date, CancellationToken ct = default);
    Task<PurchaseRangeReport> GetPurchasesRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default);
    Task<ShopDashboardViewModel> GetDashboardAsync(CancellationToken ct = default);
}

public class ReportService : IReportService
{
    private readonly ApplicationDbContext _db;
    private readonly IClock _clock;

    public ReportService(ApplicationDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<StockReport> GetStockAsync(bool includeInactive, CancellationToken ct = default)
    {
        var query = _db.Products.AsNoTracking();
        if (!includeInactive) query = query.Where(p => p.IsActive || p.StockKg > 0);

        var rows = await query.OrderBy(p => p.Name)
            .Select(p => new StockRow(p.Id, p.Name, p.Category != null ? p.Category.Name : null, p.StockKg, p.DefaultRatePerKg, p.IsActive))
            .ToListAsync(ct);

        return new StockReport(rows, rows.Sum(r => r.StockKg));
    }

    public async Task<DailyPurchaseReport> GetDailyPurchasesAsync(DateOnly date, CancellationToken ct = default)
    {
        var bills = await _db.PurchaseBills.AsNoTracking()
            .Where(b => b.Status == BillStatus.Finalized && b.BillDate == date)
            .OrderBy(b => b.FinalizedAt)
            .Select(b => new { b.Id, b.InvoiceNumber, b.FinalizedAt, b.SellerName, b.TotalKg, b.TotalAmount })
            .ToListAsync(ct);

        var billRows = bills.Select(b => new DailyBillRow(
            b.Id, b.InvoiceNumber ?? "", _clock.ToLocal(b.FinalizedAt ?? DateTime.UtcNow), b.SellerName, b.TotalKg, b.TotalAmount)).ToList();

        var grouped = await _db.PurchaseBillItems.AsNoTracking()
            .Where(i => i.PurchaseBill!.Status == BillStatus.Finalized && i.PurchaseBill.BillDate == date)
            .GroupBy(i => i.ProductName)
            .Select(g => new { Product = g.Key, Kg = g.Sum(x => x.QuantityKg), Amount = g.Sum(x => x.LineTotal) })
            .OrderBy(x => x.Product)
            .ToListAsync(ct);

        var productRows = grouped
            .Select(g => new DailyProductRow(g.Product, g.Kg, g.Amount, g.Kg > 0 ? Math.Round(g.Amount / g.Kg, 2) : 0))
            .ToList();

        return new DailyPurchaseReport(date, billRows, productRows, billRows.Sum(b => b.Kg), billRows.Sum(b => b.Amount));
    }

    public async Task<PurchaseRangeReport> GetPurchasesRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        if (to < from) (from, to) = (to, from);
        if (to.DayNumber - from.DayNumber > 366) from = to.AddDays(-366);

        var bills = await _db.PurchaseBills.AsNoTracking()
            .Where(b => b.Status == BillStatus.Finalized && b.BillDate >= from && b.BillDate <= to)
            .OrderBy(b => b.BillDate).ThenBy(b => b.FinalizedAt)
            .Select(b => new { b.Id, b.InvoiceNumber, b.BillDate, b.FinalizedAt, b.SellerName, b.TotalKg, b.TotalAmount })
            .ToListAsync(ct);

        var billRows = bills.Select(b => new RangeBillRow(
            b.Id, b.InvoiceNumber ?? "", b.BillDate, _clock.ToLocal(b.FinalizedAt ?? DateTime.UtcNow), b.SellerName, b.TotalKg, b.TotalAmount)).ToList();

        var days = billRows.GroupBy(b => b.BillDate).OrderBy(g => g.Key)
            .Select(g => new DayTotalRow(g.Key, g.Count(), g.Sum(x => x.Kg), g.Sum(x => x.Amount))).ToList();

        var grouped = await _db.PurchaseBillItems.AsNoTracking()
            .Where(i => i.PurchaseBill!.Status == BillStatus.Finalized && i.PurchaseBill.BillDate >= from && i.PurchaseBill.BillDate <= to)
            .GroupBy(i => i.ProductName)
            .Select(g => new { Product = g.Key, Kg = g.Sum(x => x.QuantityKg), Amount = g.Sum(x => x.LineTotal) })
            .OrderBy(x => x.Product)
            .ToListAsync(ct);

        var products = grouped
            .Select(g => new DailyProductRow(g.Product, g.Kg, g.Amount, g.Kg > 0 ? Math.Round(g.Amount / g.Kg, 2) : 0))
            .ToList();

        return new PurchaseRangeReport(from, to, billRows, days, products, billRows.Sum(b => b.Kg), billRows.Sum(b => b.Amount));
    }

    public async Task<ShopDashboardViewModel> GetDashboardAsync(CancellationToken ct = default)
    {
        var today = _clock.Today;
        var vm = new ShopDashboardViewModel();

        vm.ProductCount = await _db.Products.CountAsync(p => p.IsActive, ct);
        vm.TotalStockKg = await _db.Products.SumAsync(p => (decimal?)p.StockKg, ct) ?? 0m;

        var todays = await _db.PurchaseBills.AsNoTracking()
            .Where(b => b.Status == BillStatus.Finalized && b.BillDate == today)
            .Select(b => new { b.TotalKg, b.TotalAmount })
            .ToListAsync(ct);
        vm.TodayBills = todays.Count;
        vm.TodayKg = todays.Sum(t => t.TotalKg);
        vm.TodayAmount = todays.Sum(t => t.TotalAmount);

        vm.DraftCount = await _db.PurchaseBills.CountAsync(b => b.Status == BillStatus.Draft, ct);
        vm.RecentBills = await _db.PurchaseBills.AsNoTracking()
            .OrderByDescending(b => b.CreatedAt).Take(8).ToListAsync(ct);
        vm.LowestStock = await _db.Products.AsNoTracking()
            .Where(p => p.IsActive).OrderBy(p => p.StockKg).ThenBy(p => p.Name).Take(5).ToListAsync(ct);
        return vm;
    }
}

// using Microsoft.EntityFrameworkCore;
// using ScrapManagement.Web.Data;
// using ScrapManagement.Web.Infrastructure;
// using ScrapManagement.Web.Models;
// using ScrapManagement.Web.Models.ViewModels;

// namespace ScrapManagement.Web.Services;

// public record StockRow(int ProductId, string Name, string? Category, decimal StockKg, decimal DefaultRate, bool IsActive);
// public record StockReport(IReadOnlyList<StockRow> Rows, decimal TotalKg);
// public record DailyBillRow(int Id, string InvoiceNumber, DateTime FinalizedAtLocal, string Seller, decimal Kg, decimal Amount);
// public record DailyProductRow(string Product, decimal Kg, decimal Amount, decimal AvgRate);
// public record DailyPurchaseReport(DateOnly Date, IReadOnlyList<DailyBillRow> Bills, IReadOnlyList<DailyProductRow> Products, decimal TotalKg, decimal TotalAmount);

// public interface IReportService
// {
//     Task<StockReport> GetStockAsync(bool includeInactive, CancellationToken ct = default);
//     Task<DailyPurchaseReport> GetDailyPurchasesAsync(DateOnly date, CancellationToken ct = default);
//     Task<ShopDashboardViewModel> GetDashboardAsync(CancellationToken ct = default);
// }

// public class ReportService : IReportService
// {
//     private readonly ApplicationDbContext _db;
//     private readonly IClock _clock;

//     public ReportService(ApplicationDbContext db, IClock clock)
//     {
//         _db = db;
//         _clock = clock;
//     }

//     public async Task<StockReport> GetStockAsync(bool includeInactive, CancellationToken ct = default)
//     {
//         var query = _db.Products.AsNoTracking();
//         if (!includeInactive) query = query.Where(p => p.IsActive || p.StockKg > 0);

//         var rows = await query.OrderBy(p => p.Name)
//             .Select(p => new StockRow(p.Id, p.Name, p.Category != null ? p.Category.Name : null, p.StockKg, p.DefaultRatePerKg, p.IsActive))
//             .ToListAsync(ct);

//         return new StockReport(rows, rows.Sum(r => r.StockKg));
//     }

//     public async Task<DailyPurchaseReport> GetDailyPurchasesAsync(DateOnly date, CancellationToken ct = default)
//     {
//         var bills = await _db.PurchaseBills.AsNoTracking()
//             .Where(b => b.Status == BillStatus.Finalized && b.BillDate == date)
//             .OrderBy(b => b.FinalizedAt)
//             .Select(b => new { b.Id, b.InvoiceNumber, b.FinalizedAt, b.SellerName, b.TotalKg, b.TotalAmount })
//             .ToListAsync(ct);

//         var billRows = bills.Select(b => new DailyBillRow(
//             b.Id, b.InvoiceNumber ?? "", _clock.ToLocal(b.FinalizedAt ?? DateTime.UtcNow), b.SellerName, b.TotalKg, b.TotalAmount)).ToList();

//         var grouped = await _db.PurchaseBillItems.AsNoTracking()
//             .Where(i => i.PurchaseBill!.Status == BillStatus.Finalized && i.PurchaseBill.BillDate == date)
//             .GroupBy(i => i.ProductName)
//             .Select(g => new { Product = g.Key, Kg = g.Sum(x => x.QuantityKg), Amount = g.Sum(x => x.LineTotal) })
//             .OrderBy(x => x.Product)
//             .ToListAsync(ct);

//         var productRows = grouped
//             .Select(g => new DailyProductRow(g.Product, g.Kg, g.Amount, g.Kg > 0 ? Math.Round(g.Amount / g.Kg, 2) : 0))
//             .ToList();

//         return new DailyPurchaseReport(date, billRows, productRows, billRows.Sum(b => b.Kg), billRows.Sum(b => b.Amount));
//     }

//     public async Task<ShopDashboardViewModel> GetDashboardAsync(CancellationToken ct = default)
//     {
//         var today = _clock.Today;
//         var vm = new ShopDashboardViewModel();

//         vm.ProductCount = await _db.Products.CountAsync(p => p.IsActive, ct);
//         vm.TotalStockKg = await _db.Products.SumAsync(p => (decimal?)p.StockKg, ct) ?? 0m;

//         var todays = await _db.PurchaseBills.AsNoTracking()
//             .Where(b => b.Status == BillStatus.Finalized && b.BillDate == today)
//             .Select(b => new { b.TotalKg, b.TotalAmount })
//             .ToListAsync(ct);
//         vm.TodayBills = todays.Count;
//         vm.TodayKg = todays.Sum(t => t.TotalKg);
//         vm.TodayAmount = todays.Sum(t => t.TotalAmount);

//         vm.DraftCount = await _db.PurchaseBills.CountAsync(b => b.Status == BillStatus.Draft, ct);
//         vm.RecentBills = await _db.PurchaseBills.AsNoTracking()
//             .OrderByDescending(b => b.CreatedAt).Take(8).ToListAsync(ct);
//         vm.LowestStock = await _db.Products.AsNoTracking()
//             .Where(p => p.IsActive).OrderBy(p => p.StockKg).ThenBy(p => p.Name).Take(5).ToListAsync(ct);
//         return vm;
//     }
// }
