using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ScrapManagement.Web.Data;
using ScrapManagement.Web.Models;

namespace ScrapManagement.Web.Controllers;

/// <summary>Main admin: subscription prices, worker limits and features.</summary>
[Authorize(Policy = PolicyNames.MainAdmin)]
[Route("admin/plans")]
public class AdminPlansController : Controller
{
    private readonly ApplicationDbContext _db;
    public AdminPlansController(ApplicationDbContext db) => _db = db;

    [HttpGet("")]
    public async Task<IActionResult> Index() =>
        View(await _db.SubscriptionPlans.AsNoTracking().OrderBy(p => p.Name).ToListAsync());

    [HttpGet("create")]
    public IActionResult Create() => View("Form", new SubscriptionPlan());

    [HttpPost("create"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SubscriptionPlan model)
    {
        ValidateOffers(model);
        if (!ModelState.IsValid) return View("Form", model);
        model.Id = 0;
        model.Name = model.Name.Trim();
        _db.SubscriptionPlans.Add(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Plan created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("{id:int}/edit")]
    public async Task<IActionResult> Edit(int id)
    {
        var plan = await _db.SubscriptionPlans.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        return plan is null ? NotFound() : View("Form", plan);
    }

    [HttpPost("{id:int}/edit"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, SubscriptionPlan model)
    {
        var plan = await _db.SubscriptionPlans.FirstOrDefaultAsync(p => p.Id == id);
        if (plan is null) return NotFound();

        ValidateOffers(model);
        if (!ModelState.IsValid) { model.Id = id; return View("Form", model); }

        plan.Name = model.Name.Trim();
        plan.Description = model.Description?.Trim();
        plan.OfferMonthly = model.OfferMonthly;
        plan.MonthlyPrice = model.MonthlyPrice;
        plan.OfferYearly = model.OfferYearly;
        plan.YearlyPrice = model.YearlyPrice;
        plan.MaxWorkers = model.MaxWorkers;
        plan.AllowReportDownloads = model.AllowReportDownloads;
        plan.SalesModuleEnabled = model.SalesModuleEnabled;
        plan.IsActive = model.IsActive;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Plan updated. Changes apply to all shops on this plan immediately.";
        return RedirectToAction(nameof(Index));
    }

    private void ValidateOffers(SubscriptionPlan m)
    {
        if (!m.OfferMonthly && !m.OfferYearly)
            ModelState.AddModelError("", "Enable at least one billing option (monthly or yearly).");
        if (m.OfferMonthly && m.MonthlyPrice <= 0)
            ModelState.AddModelError(nameof(SubscriptionPlan.MonthlyPrice), "Enter the monthly price.");
        if (m.OfferYearly && m.YearlyPrice <= 0)
            ModelState.AddModelError(nameof(SubscriptionPlan.YearlyPrice), "Enter the yearly price.");
    }
}