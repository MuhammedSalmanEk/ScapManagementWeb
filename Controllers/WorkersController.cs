using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScrapManagement.Web.Infrastructure;
using ScrapManagement.Web.Models;
using ScrapManagement.Web.Models.ViewModels;
using ScrapManagement.Web.Services;

namespace ScrapManagement.Web.Controllers;

[Authorize(Policy = PolicyNames.ShopAdmin)]
public class WorkersController : Controller
{
    private readonly IWorkerService _workers;
    private readonly ITenantProvider _tenant;

    public WorkersController(IWorkerService workers, ITenantProvider tenant)
    {
        _workers = workers;
        _tenant = tenant;
    }

    private int ShopId => _tenant.ShopId ?? throw new InvalidOperationException("No shop");

    public async Task<IActionResult> Index() => View(await _workers.ListAsync(ShopId));

    public IActionResult Create() => View("Form", new WorkerFormViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(WorkerFormViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Password)) ModelState.AddModelError(nameof(model.Password), "Password is required.");
        if (!ModelState.IsValid) return View("Form", model);

        var result = await _workers.CreateAsync(ShopId, model);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors) ModelState.AddModelError("", e);
            return View("Form", model);
        }
        TempData["Success"] = "Worker account created.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(string id)
    {
        var u = await _workers.GetAsync(ShopId, id);
        if (u is null) return NotFound();
        return View("Form", new WorkerFormViewModel
        {
            Id = u.Id, FullName = u.FullName, Email = u.Email ?? "", IsActive = u.IsActive,
            CanAddProducts = u.CanAddProducts, CanEditProducts = u.CanEditProducts, CanManageCategories = u.CanManageCategories,
            CanManagePurchases = u.CanManagePurchases, CanViewReports = u.CanViewReports, CanViewStock = u.CanViewStock
            // CanAddProducts = u.CanAddProducts,CanEditProducts = u.CanEditProducts,
            //  CanManagePurchases = u.CanManagePurchases,
            // CanViewReports = u.CanViewReports, CanViewStock = u.CanViewStock
       
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(WorkerFormViewModel model)
    {
        if (!ModelState.IsValid) return View("Form", model);
        var result = await _workers.UpdateAsync(ShopId, model);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors) ModelState.AddModelError("", e);
            return View("Form", model);
        }
        TempData["Success"] = "Worker updated.";
        return RedirectToAction(nameof(Index));
    }
}