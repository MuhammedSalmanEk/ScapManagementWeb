using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ScrapManagement.Web.Data;
using ScrapManagement.Web.Infrastructure;
using ScrapManagement.Web.Models;
using ScrapManagement.Web.Services;

namespace ScrapManagement.Web.Controllers;

public class SubscriptionStatusViewModel
{
    public ShopAccessInfo Info { get; set; } = null!;
    public List<PlanOffer> Offers { get; set; } = new();
    public List<Payment> Payments { get; set; } = new();
    public bool PaymentsEnabled { get; set; }
}

[Authorize]
public class SubscriptionController : Controller
{
    private readonly IShopManagementService _shops;
    private readonly IPaymentService _payments;
    private readonly ApplicationDbContext _db;

    public SubscriptionController(IShopManagementService shops, IPaymentService payments, ApplicationDbContext db)
    {
        _shops = shops; _payments = payments; _db = db;
    }

    public async Task<IActionResult> Status(CancellationToken ct)
    {
        if (User.IsInRole(AppRoles.MainAdmin)) return RedirectToAction("Dashboard", "AdminShops");
        var info = HttpContext.GetShopAccess();
        if (info is null) return RedirectToAction("Index", "Home");

        return View(new SubscriptionStatusViewModel
        {
            Info = info,
            Offers = await _shops.GetOffersAsync(ct),
            Payments = await _db.Payments.AsNoTracking().Where(p => p.ShopId == info.Shop.Id)
                .OrderByDescending(p => p.CreatedAt).Take(20).ToListAsync(ct),
            PaymentsEnabled = _payments.Enabled
        });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = PolicyNames.ShopAdmin)]
    public async Task<IActionResult> CreateOrder(string offer, CancellationToken ct)
    {
        var info = HttpContext.GetShopAccess();
        if (info is null) return Forbid();
        if (!PlanOffer.TryParse(offer, out var planId, out var period)) return BadRequest(new { error = "Please choose a plan." });

        var result = await _payments.CreateOrderAsync(info.Shop.Id, planId, period, ct);
        if (!result.Succeeded) return BadRequest(new { error = result.FirstError });
        return Json(result.Value);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = PolicyNames.ShopAdmin)]
    public async Task<IActionResult> Verify(string razorpay_order_id, string razorpay_payment_id, string razorpay_signature, CancellationToken ct)
    {
        var info = HttpContext.GetShopAccess();
        if (info is null) return Forbid();
        if (!await _db.Payments.AnyAsync(p => p.RazorpayOrderId == razorpay_order_id && p.ShopId == info.Shop.Id, ct))
            return Forbid();

        var result = await _payments.ConfirmAsync(razorpay_order_id, razorpay_payment_id, razorpay_signature, ct);
        if (!result.Succeeded) return BadRequest(new { error = result.FirstError });
        return Json(new { ok = true });
    }
}


// using Microsoft.AspNetCore.Authorization;
// using Microsoft.AspNetCore.Mvc;
// using ScrapManagement.Web.Infrastructure;
// using ScrapManagement.Web.Models;

// namespace ScrapManagement.Web.Controllers;

// [Authorize]
// public class SubscriptionController : Controller
// {
//     /// <summary>Shown to shop users when the shop is pending, suspended or expired.</summary>
//     public IActionResult Status()
//     {
//         if (User.IsInRole(AppRoles.MainAdmin)) return RedirectToAction("Dashboard", "AdminShops");
//         var info = HttpContext.GetShopAccess();
//         if (info is null) return RedirectToAction("Index", "Home");
//         return View(info);
//     }
// }
