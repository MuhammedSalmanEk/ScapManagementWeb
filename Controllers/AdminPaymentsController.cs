using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ScrapManagement.Web.Data;
using ScrapManagement.Web.Models;
using ScrapManagement.Web.Models.ViewModels;
using ScrapManagement.Web.Services;

namespace ScrapManagement.Web.Controllers;

[Authorize(Policy = PolicyNames.MainAdmin)]
[Route("admin/payments")]
public class AdminPaymentsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IPaymentService _payments;
    public AdminPaymentsController(ApplicationDbContext db, IPaymentService payments) { _db = db; _payments = payments; }

    [HttpGet("")]
    public async Task<IActionResult> Index(string? status, int? shopId, DateOnly? fromDate, DateOnly? toDate, CancellationToken ct)
    {
        status ??= "paid";
        var q = _db.Payments.AsNoTracking().Include(p => p.Shop).AsQueryable();
        if (shopId.HasValue) q = q.Where(p => p.ShopId == shopId);
        if (fromDate.HasValue)
        {
            var f = fromDate.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            q = q.Where(p => p.CreatedAt >= f);
        }
        if (toDate.HasValue)
        {
            var t = toDate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            q = q.Where(p => p.CreatedAt < t);
        }

        var paid = q.Where(p => p.Status == PaymentStatus.Paid);
        var totalPaid = await paid.SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;
        var totalCredited = await paid.SumAsync(p => p.CreditedAmount, ct) ?? 0m;

        q = status switch
        {
            "paid" => q.Where(p => p.Status == PaymentStatus.Paid),
            "created" => q.Where(p => p.Status == PaymentStatus.Created),
            "failed" => q.Where(p => p.Status == PaymentStatus.Failed),
            _ => q
        };

        return View(new AdminPaymentsViewModel
        {
            Rows = await q.OrderByDescending(p => p.CreatedAt).Take(300).ToListAsync(ct),
            Status = status, ShopId = shopId, From = fromDate, To = toDate,
            TotalPaid = totalPaid, TotalCredited = totalCredited
        });
    }

    [HttpPost("{id:int}/credit"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Credit(int id, decimal? amount, DateOnly? creditedOn, string? note, CancellationToken ct)
    {
        var r = await _payments.SetCreditedAsync(id, amount,
            creditedOn?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), note, User.Identity?.Name ?? "main admin", ct);
        if (r.Succeeded) TempData["Success"] = "Credited amount saved.";
        else TempData["Error"] = r.FirstError;
        return RedirectToAction(nameof(Index));
    }
}