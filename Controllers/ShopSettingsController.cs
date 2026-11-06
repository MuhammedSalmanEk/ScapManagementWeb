using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ScrapManagement.Web.Data;
using ScrapManagement.Web.Infrastructure;
using ScrapManagement.Web.Models;

namespace ScrapManagement.Web.Controllers;

[Authorize(Policy = PolicyNames.ShopAdmin)]
public class ShopSettingsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ITenantProvider _tenant;

    public ShopSettingsController(ApplicationDbContext db, ITenantProvider tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var shop = await _db.Shops.AsNoTracking().FirstOrDefaultAsync(s => s.Id == _tenant.ShopId);
        return shop is null ? NotFound() : View(shop);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(Shop model)
    {
        var shop = await _db.Shops.FirstOrDefaultAsync(s => s.Id == _tenant.ShopId);
        if (shop is null) return NotFound();

        // Only these fields are editable by the shop; status/plan/dates belong to the main admin.
        ModelState.Remove(nameof(Shop.Plan));
        if (!ModelState.IsValid)
        {
            model.Status = shop.Status;
            return View(model);
        }

        shop.Name = model.Name.Trim();
        shop.OwnerName = model.OwnerName.Trim();
        shop.Phone = model.Phone?.Trim();
        shop.Address = model.Address?.Trim();
        shop.TaxNumber = model.TaxNumber?.Trim();
        shop.InvoiceFooter = model.InvoiceFooter?.Trim();
        shop.ReceiptPaperWidthMm = model.ReceiptPaperWidthMm is 58 or 80 ? model.ReceiptPaperWidthMm : 0;
        shop.AutoPrintReceipt = model.AutoPrintReceipt;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Shop details saved.";
        return RedirectToAction(nameof(Index));
    }
}
