using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ScrapManagement.Web.Data;
using ScrapManagement.Web.Models;
using ScrapManagement.Web.Models.ViewModels;
using ScrapManagement.Web.Services;

namespace ScrapManagement.Web.Controllers;

/// <summary>Main admin: monitor shops and manage their subscriptions.</summary>
[Authorize(Policy = PolicyNames.MainAdmin)]
[Route("admin")]
public class AdminShopsController : Controller
{
    private readonly IShopManagementService _shops;
    private readonly ApplicationDbContext _db;

    public AdminShopsController(IShopManagementService shops, ApplicationDbContext db)
    {
        _shops = shops;
        _db = db;
    }

    private string Me => User.Identity?.Name ?? "main admin";

    [HttpGet("")]
    public async Task<IActionResult> Dashboard(CancellationToken ct)
    {
        var rows = await _shops.GetUsageAsync(null, ct);
        return View(new AdminDashboardViewModel
        {
            Pending = rows.Count(r => r.Access == ShopAccess.Pending),
            Active = rows.Count(r => r.Access == ShopAccess.Full),
            Expired = rows.Count(r => r.Access == ShopAccess.ReadOnly),
            Suspended = rows.Count(r => r.Access == ShopAccess.Blocked),
            PendingShops = rows.Where(r => r.Access == ShopAccess.Pending).ToList(),
            ExpiringSoon = rows.Where(r => r.Access == ShopAccess.Full && r.DaysLeft <= 30).OrderBy(r => r.DaysLeft).ToList()
        });
    }

    [HttpGet("shops")]
    public async Task<IActionResult> Index(string? search, string? filter, CancellationToken ct)
    {
        var rows = await _shops.GetUsageAsync(search, ct);
        rows = filter switch
        {
            "pending" => rows.Where(r => r.Access == ShopAccess.Pending).ToList(),
            "active" => rows.Where(r => r.Access == ShopAccess.Full).ToList(),
            "expired" => rows.Where(r => r.Access == ShopAccess.ReadOnly).ToList(),
            "suspended" => rows.Where(r => r.Access == ShopAccess.Blocked).ToList(),
            _ => rows
        };
        return View(new AdminShopsIndexViewModel { Rows = rows, Search = search, Filter = filter });
    }

    [HttpGet("shops/{id:int}")]
    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var usage = await _shops.GetUsageAsync(id, ct);
        if (usage is null) return NotFound();
        return View(new AdminShopDetailsViewModel
        {
            Usage = usage,
            Plans = await _db.SubscriptionPlans.AsNoTracking().OrderBy(p => p.Name).ToListAsync(ct),
            History = await _db.SubscriptionEvents.AsNoTracking().Where(e => e.ShopId == id).OrderByDescending(e => e.CreatedAt).Take(30).ToListAsync(ct),
            Users = await _db.Users.AsNoTracking().Where(u => u.ShopId == id).OrderByDescending(u => u.IsShopAdmin).ThenBy(u => u.FullName).ToListAsync(ct)
        });
    }

    [HttpPost("shops/{id:int}/terminate"), ValidateAntiForgeryToken]
    public Task<IActionResult> Terminate(int id, string? note, CancellationToken ct) =>
    Act(id, () => _shops.TerminateAsync(id, note, Me, ct), "Shop terminated. Access ended immediately.");
    
    // [HttpPost("shops/{id:int}/activate"), ValidateAntiForgeryToken]
    // public Task<IActionResult> Activate(int id, int planId, CancellationToken ct) =>
    //     Act(id, () => _shops.ActivateAsync(id, planId, Me, ct), "Subscription activated for one year.");

    // [HttpPost("shops/{id:int}/renew"), ValidateAntiForgeryToken]
    // public Task<IActionResult> Renew(int id, CancellationToken ct) =>
    //     Act(id, () => _shops.RenewAsync(id, Me, ct), "Subscription renewed for one more year.");
[   HttpPost("shops/{id:int}/activate"), ValidateAntiForgeryToken]
    public Task<IActionResult> Activate(int id, int planId, BillingPeriod period, CancellationToken ct) =>
    Act(id, () => _shops.ActivateAsync(id, planId, period, Me, ct), $"Subscription activated ({period}).");

    [HttpPost("shops/{id:int}/renew"), ValidateAntiForgeryToken]
    public Task<IActionResult> Renew(int id, BillingPeriod period, CancellationToken ct) =>
    Act(id, () => _shops.RenewAsync(id, period, Me, ct), $"Subscription renewed ({period}).");


    [HttpPost("shops/{id:int}/suspend"), ValidateAntiForgeryToken]
    public Task<IActionResult> Suspend(int id, string? note, CancellationToken ct) =>
        Act(id, () => _shops.SuspendAsync(id, note, Me, ct), "Shop suspended. Its users are blocked immediately.");

    [HttpPost("shops/{id:int}/reactivate"), ValidateAntiForgeryToken]
    public Task<IActionResult> Reactivate(int id, CancellationToken ct) =>
        Act(id, () => _shops.ReactivateAsync(id, Me, ct), "Shop reactivated.");

    [HttpPost("shops/{id:int}/limits"), ValidateAntiForgeryToken]
    public Task<IActionResult> Limits(int id, int planId, int? workerLimitOverride, CancellationToken ct) =>
        Act(id, () => _shops.UpdateLimitsAsync(id, planId, workerLimitOverride, Me, ct), "Plan and limits updated.");

    private async Task<IActionResult> Act(int id, Func<Task<ServiceResult>> action, string success)
    {
        var result = await action();
        if (result.Succeeded) TempData["Success"] = success;
        else TempData["Error"] = result.FirstError;
        return RedirectToAction(nameof(Details), new { id });
    }
}
