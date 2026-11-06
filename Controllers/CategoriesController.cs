using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ScrapManagement.Web.Data;
using ScrapManagement.Web.Infrastructure;
using ScrapManagement.Web.Models;

namespace ScrapManagement.Web.Controllers;

/// <summary>Shop admin always; a worker only when the shop admin ticked "Can manage categories" for them.</summary>
[Authorize(Policy = PolicyNames.ShopUser)]
public class CategoriesController : Controller
{
    private readonly ApplicationDbContext _db;
    public CategoriesController(ApplicationDbContext db) => _db = db;

    private bool CanManage => User.HasPermission(Permissions.ManageCategories);

    public async Task<IActionResult> Index()
    {
        if (!CanManage) return Forbid();
        return View(await _db.Categories.AsNoTracking().OrderBy(c => c.Name).ToListAsync());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string name)
    {
        if (!CanManage) return Forbid();
        name = (name ?? "").Trim();
        if (name.Length is 0 or > 80) { TempData["Error"] = "Enter a category name (max 80 characters)."; return RedirectToAction(nameof(Index)); }

        var lower = name.ToLower();
        if (await _db.Categories.AnyAsync(c => c.Name.ToLower() == lower))
        {
            TempData["Error"] = "This category already exists.";
            return RedirectToAction(nameof(Index));
        }
        _db.Categories.Add(new Category { Name = name });
        await _db.SaveChangesAsync();
        TempData["Success"] = "Category added.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Rename(int id, string name)
    {
        if (!CanManage) return Forbid();
        name = (name ?? "").Trim();
        var cat = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id);
        if (cat is null) return NotFound();
        if (name.Length is 0 or > 80) { TempData["Error"] = "Enter a valid name."; return RedirectToAction(nameof(Index)); }

        var lower = name.ToLower();
        if (await _db.Categories.AnyAsync(c => c.Id != id && c.Name.ToLower() == lower))
        {
            TempData["Error"] = "Another category already uses this name.";
            return RedirectToAction(nameof(Index));
        }
        cat.Name = name;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Category renamed.";
        return RedirectToAction(nameof(Index));
    }

    // Activate / deactivate stays with the shop admin.
    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = PolicyNames.ShopAdmin)]
    public async Task<IActionResult> Toggle(int id)
    {
        var cat = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id);
        if (cat is null) return NotFound();
        cat.IsActive = !cat.IsActive;
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
}