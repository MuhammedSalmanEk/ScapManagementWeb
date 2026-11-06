using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ScrapManagement.Web.Data;
using ScrapManagement.Web.Infrastructure;
using ScrapManagement.Web.Models;

namespace ScrapManagement.Web.Controllers;

[Authorize(Policy = PolicyNames.ShopUser)]
public class ProductsController : Controller
{
    private readonly ApplicationDbContext _db;
    public ProductsController(ApplicationDbContext db) => _db = db;

    public async Task<IActionResult> Index(string? search, int? categoryId, bool showInactive = false)
    {
        var q = _db.Products.AsNoTracking().Include(p => p.Category).AsQueryable();
        if (!showInactive) q = q.Where(p => p.IsActive);
        if (categoryId.HasValue) q = q.Where(p => p.CategoryId == categoryId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var t = search.Trim().ToLower();
            q = q.Where(p => p.Name.ToLower().Contains(t));
        }
        ViewBag.Search = search;
        ViewBag.CategoryId = categoryId;
        ViewBag.ShowInactive = showInactive;
        ViewBag.Categories = new SelectList(await _db.Categories.AsNoTracking().OrderBy(c => c.Name).ToListAsync(), "Id", "Name", categoryId);
        return View(await q.OrderBy(p => p.Name).ToListAsync());
    }

    // Workers with the "add products" permission can add products (with both rates).
    [Authorize(Policy = PolicyNames.AddProducts)]
    public async Task<IActionResult> Create()
    {
        await LoadCategoriesAsync(null);
        return View("Form", new Product());
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = PolicyNames.AddProducts)]
    public async Task<IActionResult> Create(Product model)
    {
        ModelState.Remove(nameof(Product.Category));
        await ValidateAsync(model, null);
        if (!ModelState.IsValid) { await LoadCategoriesAsync(model.CategoryId); return View("Form", model); }

        _db.Products.Add(new Product
        {
            Name = model.Name.Trim(),
            Description = model.Description?.Trim(),
            CategoryId = model.CategoryId,
            DefaultRatePerKg = model.DefaultRatePerKg,
            WholesaleRatePerKg = model.WholesaleRatePerKg,
            IsActive = true
        });
        await _db.SaveChangesAsync();
        TempData["Success"] = "Product added.";
        return RedirectToAction(nameof(Index));
    }

    // Shop admin always; a worker only when the shop admin ticked "Can edit products" for them.
    public async Task<IActionResult> Edit(int id)
    {
        if (!User.HasPermission(Permissions.EditProducts)) return Forbid();
        var p = await _db.Products.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return NotFound();
        await LoadCategoriesAsync(p.CategoryId);
        return View("Form", p);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Product model)
    {
        if (!User.HasPermission(Permissions.EditProducts)) return Forbid();
        var p = await _db.Products.FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return NotFound();

        ModelState.Remove(nameof(Product.Category));
        await ValidateAsync(model, id);
        if (!ModelState.IsValid) { model.Id = id; model.StockKg = p.StockKg; await LoadCategoriesAsync(model.CategoryId); return View("Form", model); }

        // Stock is never edited here; it only changes through finalized bills. Active/inactive stays with the shop admin.
        p.Name = model.Name.Trim();
        p.Description = model.Description?.Trim();
        p.CategoryId = model.CategoryId;
        p.DefaultRatePerKg = model.DefaultRatePerKg;
        p.WholesaleRatePerKg = model.WholesaleRatePerKg;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Product updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = PolicyNames.ShopAdmin)]
    public async Task<IActionResult> Toggle(int id)
    {
        var p = await _db.Products.FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return NotFound();
        p.IsActive = !p.IsActive;
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private async Task ValidateAsync(Product model, int? id)
    {
        var name = (model.Name ?? "").Trim();
        if (name.Length > 0)
        {
            var lower = name.ToLower();
            if (await _db.Products.AnyAsync(p => p.Id != id && p.Name.ToLower() == lower))
                ModelState.AddModelError(nameof(Product.Name), "A product with this name already exists.");
        }
        if (model.CategoryId.HasValue && !await _db.Categories.AnyAsync(c => c.Id == model.CategoryId))
            ModelState.AddModelError(nameof(Product.CategoryId), "Invalid category.");
    }

    private async Task LoadCategoriesAsync(int? selected) =>
        ViewBag.Categories = new SelectList(await _db.Categories.AsNoTracking().Where(c => c.IsActive || c.Id == selected).OrderBy(c => c.Name).ToListAsync(), "Id", "Name", selected);
}