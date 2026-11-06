using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ScrapManagement.Web.Data;
using ScrapManagement.Web.Infrastructure;
using ScrapManagement.Web.Models;
using ScrapManagement.Web.Models.ViewModels;

namespace ScrapManagement.Web.Services;

/// <summary>Registration of new shops (anonymous) and everything the main admin does to shops.</summary>
public interface IShopManagementService
{
    Task<ServiceResult> RegisterShopAsync(RegisterShopViewModel model, CancellationToken ct = default);
    Task<List<PlanOffer>> GetOffersAsync(CancellationToken ct = default);
    Task<List<ShopUsageRow>> GetUsageAsync(string? search = null, CancellationToken ct = default);
    Task<ShopUsageRow?> GetUsageAsync(int shopId, CancellationToken ct = default);
    Task<ServiceResult> ActivateAsync(int shopId, int planId, BillingPeriod period, string by, CancellationToken ct = default);
    Task<ServiceResult> RenewAsync(int shopId, BillingPeriod period, string by, CancellationToken ct = default);
    Task<ServiceResult> SuspendAsync(int shopId, string? note, string by, CancellationToken ct = default);
    Task<ServiceResult> ReactivateAsync(int shopId, string by, CancellationToken ct = default);
    Task<ServiceResult> TerminateAsync(int shopId, string? note, string by, CancellationToken ct = default);
    Task<ServiceResult> UpdateLimitsAsync(int shopId, int planId, int? workerLimitOverride, string by, CancellationToken ct = default);
}

public class ShopManagementService : IShopManagementService
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IClock _clock;

    public ShopManagementService(ApplicationDbContext db, UserManager<ApplicationUser> users, IClock clock)
    {
        _db = db;
        _users = users;
        _clock = clock;
    }

    // ------------------------------------------------------------------ plans offered to shops

    /// <summary>Every active plan x billing period that the main admin enabled, with its price.</summary>
    public async Task<List<PlanOffer>> GetOffersAsync(CancellationToken ct = default)
    {
        var plans = await _db.SubscriptionPlans.AsNoTracking().Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync(ct);
        var list = new List<PlanOffer>();
        foreach (var p in plans)
        {
            if (p.OfferMonthly && p.MonthlyPrice > 0)
                list.Add(new PlanOffer(p.Id, p.Name, BillingPeriod.Monthly, p.MonthlyPrice, p.MaxWorkers));
            if (p.OfferYearly && p.YearlyPrice > 0)
                list.Add(new PlanOffer(p.Id, p.Name, BillingPeriod.Yearly, p.YearlyPrice, p.MaxWorkers));
        }
        return list;
    }

    // ------------------------------------------------------------------ registration

    /// <summary>
    /// Step 1: creates the shop (Pending) and its admin account. The shop chooses its real plan and billing
    /// period on the next screen (Subscription/Status) when it pays. A placeholder plan is stored until then.
    /// </summary>
    public async Task<ServiceResult> RegisterShopAsync(RegisterShopViewModel m, CancellationToken ct = default)
    {
        var email = m.Email.Trim();
        if (await _users.FindByEmailAsync(email) is not null)
            return ServiceResult.Fail("An account with this email already exists.");

        var plan = await _db.SubscriptionPlans.Where(p => p.IsActive).OrderBy(p => p.Id).FirstOrDefaultAsync(ct);
        if (plan is null) return ServiceResult.Fail("Registration is temporarily unavailable (no subscription plan).");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var shop = new Shop
        {
            Name = m.ShopName.Trim(),
            OwnerName = m.OwnerName.Trim(),
            Phone = m.Phone?.Trim(),
            Address = m.Address?.Trim(),
            PlanId = plan.Id,
            BillingPeriod = plan.OfferYearly ? BillingPeriod.Yearly : BillingPeriod.Monthly,
            Status = ShopStatus.Pending
        };
        _db.Shops.Add(shop);
        await _db.SaveChangesAsync(ct);

        _db.SubscriptionEvents.Add(new SubscriptionEvent
        {
            ShopId = shop.Id,
            Action = SubscriptionAction.Registered,
            PlanId = plan.Id,
            PlanName = plan.Name,
            Note = "Shop registered, waiting for plan selection and payment",
            PerformedBy = m.OwnerName
        });

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = m.OwnerName.Trim(),
            PhoneNumber = m.Phone?.Trim(),
            ShopId = shop.Id,
            IsShopAdmin = true,
            CanAddProducts = true,
            CanManagePurchases = true,
            CanViewReports = true
        };
        var created = await _users.CreateAsync(user, m.Password);
        if (!created.Succeeded)
        {
            await tx.RollbackAsync(ct);
            return ServiceResult.Fail(created.Errors.Select(e => e.Description));
        }
        await _users.AddToRoleAsync(user, AppRoles.ShopAdmin);
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ServiceResult.Ok();
    }

    // ------------------------------------------------------------------ monitoring

    public async Task<List<ShopUsageRow>> GetUsageAsync(string? search = null, CancellationToken ct = default)
    {
        var shopsQuery = _db.Shops.AsNoTracking().Include(s => s.Plan).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            shopsQuery = shopsQuery.Where(s => s.Name.ToLower().Contains(term) || s.OwnerName.ToLower().Contains(term)
                                               || (s.Phone != null && s.Phone.Contains(term)));
        }
        var shops = await shopsQuery.OrderBy(s => s.Name).ToListAsync(ct);
        return await BuildRowsAsync(shops, ct);
    }

    public async Task<ShopUsageRow?> GetUsageAsync(int shopId, CancellationToken ct = default)
    {
        var shop = await _db.Shops.AsNoTracking().Include(s => s.Plan).FirstOrDefaultAsync(s => s.Id == shopId, ct);
        if (shop is null) return null;
        return (await BuildRowsAsync(new List<Shop> { shop }, ct)).Single();
    }

    private async Task<List<ShopUsageRow>> BuildRowsAsync(List<Shop> shops, CancellationToken ct)
    {
        var ids = shops.Select(s => s.Id).ToList();
        var now = _clock.UtcNow;

        var workers = await _db.Users.Where(u => u.ShopId != null && ids.Contains(u.ShopId!.Value) && !u.IsShopAdmin && u.IsActive)
            .GroupBy(u => u.ShopId!.Value).Select(g => new { ShopId = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.ShopId, x => x.Count, ct);

        // Admin-side statistics read across shops, so the tenant filter is bypassed on purpose.
        var products = await _db.Products.IgnoreQueryFilters().Where(p => ids.Contains(p.ShopId))
            .GroupBy(p => p.ShopId).Select(g => new { ShopId = g.Key, Count = g.Count(), Kg = g.Sum(p => p.StockKg) })
            .ToDictionaryAsync(x => x.ShopId, ct);

        var bills = await _db.PurchaseBills.IgnoreQueryFilters().Where(b => ids.Contains(b.ShopId) && b.Status == BillStatus.Finalized)
            .GroupBy(b => b.ShopId).Select(g => new { ShopId = g.Key, Count = g.Count(), Amount = g.Sum(b => b.TotalAmount), Last = g.Max(b => b.FinalizedAt) })
            .ToDictionaryAsync(x => x.ShopId, ct);

        return shops.Select(s =>
        {
            products.TryGetValue(s.Id, out var p);
            bills.TryGetValue(s.Id, out var b);
            workers.TryGetValue(s.Id, out var w);
            return new ShopUsageRow
            {
                Shop = s,
                Access = SubscriptionRules.Evaluate(s.Status, s.SubscriptionEndsOn, now),
                DaysLeft = s.SubscriptionEndsOn.HasValue ? (int)Math.Ceiling((s.SubscriptionEndsOn.Value - now).TotalDays) : null,
                WorkerCount = w,
                WorkerLimit = s.WorkerLimitOverride ?? s.Plan?.MaxWorkers ?? 0,
                ProductCount = p?.Count ?? 0,
                TotalStockKg = p?.Kg ?? 0,
                FinalizedBillCount = b?.Count ?? 0,
                TotalPurchaseAmount = b?.Amount ?? 0,
                LastBillAt = b?.Last
            };
        }).ToList();
    }

    // ------------------------------------------------------------------ subscription actions (manual, by main admin)
    // Normal activation / renewal happens through PaymentService after a successful Razorpay payment.

    /// <summary>Manual activation (no payment): Pending or Terminated shops start fresh; others are extended.</summary>
    public async Task<ServiceResult> ActivateAsync(int shopId, int planId, BillingPeriod period, string by, CancellationToken ct = default)
    {
        var shop = await _db.Shops.FirstOrDefaultAsync(s => s.Id == shopId, ct);
        var plan = await _db.SubscriptionPlans.FirstOrDefaultAsync(p => p.Id == planId, ct);
        if (shop is null || plan is null) return ServiceResult.Fail("Shop or plan not found.");

        var (start, end) = SubscriptionRules.Renew(shop.SubscriptionEndsOn, _clock.UtcNow, period);
        var restart = shop.Status is ShopStatus.Pending or ShopStatus.Terminated;

        shop.PlanId = plan.Id;
        shop.BillingPeriod = period;
        shop.Status = ShopStatus.Active;
        shop.SubscriptionStartsOn = restart ? start : (shop.SubscriptionStartsOn ?? start);
        shop.SubscriptionEndsOn = end;

        Log(shop, plan, restart ? SubscriptionAction.Activated : SubscriptionAction.Renewed, by, start, end, "(manual, no payment)", period, recordPrice: false);
        await _db.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> RenewAsync(int shopId, BillingPeriod period, string by, CancellationToken ct = default)
    {
        var shop = await _db.Shops.Include(s => s.Plan).FirstOrDefaultAsync(s => s.Id == shopId, ct);
        if (shop?.Plan is null) return ServiceResult.Fail("Shop not found.");
        if (shop.Status is ShopStatus.Pending or ShopStatus.Terminated)
            return ServiceResult.Fail("Use Activate for this shop.");

        var (start, end) = SubscriptionRules.Renew(shop.SubscriptionEndsOn, _clock.UtcNow, period);
        shop.SubscriptionEndsOn = end;
        shop.BillingPeriod = period;
        if (shop.Status == ShopStatus.Suspended) shop.Status = ShopStatus.Active;

        Log(shop, shop.Plan, SubscriptionAction.Renewed, by, start, end, "(manual, no payment)", period, recordPrice: false);
        await _db.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> SuspendAsync(int shopId, string? note, string by, CancellationToken ct = default)
    {
        var shop = await _db.Shops.Include(s => s.Plan).FirstOrDefaultAsync(s => s.Id == shopId, ct);
        if (shop is null) return ServiceResult.Fail("Shop not found.");
        shop.Status = ShopStatus.Suspended;
        Log(shop, shop.Plan, SubscriptionAction.Suspended, by, shop.SubscriptionStartsOn, shop.SubscriptionEndsOn, note);
        await _db.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> ReactivateAsync(int shopId, string by, CancellationToken ct = default)
    {
        var shop = await _db.Shops.Include(s => s.Plan).FirstOrDefaultAsync(s => s.Id == shopId, ct);
        if (shop is null) return ServiceResult.Fail("Shop not found.");
        if (shop.Status != ShopStatus.Suspended) return ServiceResult.Fail("Shop is not suspended.");
        shop.Status = ShopStatus.Active;
        Log(shop, shop.Plan, SubscriptionAction.Reactivated, by, shop.SubscriptionStartsOn, shop.SubscriptionEndsOn);
        await _db.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    /// <summary>Ends access immediately and closes the subscription. Only a new activation restarts it.</summary>
    public async Task<ServiceResult> TerminateAsync(int shopId, string? note, string by, CancellationToken ct = default)
    {
        var shop = await _db.Shops.Include(s => s.Plan).FirstOrDefaultAsync(s => s.Id == shopId, ct);
        if (shop is null) return ServiceResult.Fail("Shop not found.");
        shop.Status = ShopStatus.Terminated;
        shop.SubscriptionEndsOn = _clock.UtcNow;
        Log(shop, shop.Plan, SubscriptionAction.Terminated, by, shop.SubscriptionStartsOn, shop.SubscriptionEndsOn, note);
        await _db.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> UpdateLimitsAsync(int shopId, int planId, int? workerLimitOverride, string by, CancellationToken ct = default)
    {
        var shop = await _db.Shops.FirstOrDefaultAsync(s => s.Id == shopId, ct);
        var plan = await _db.SubscriptionPlans.FirstOrDefaultAsync(p => p.Id == planId, ct);
        if (shop is null || plan is null) return ServiceResult.Fail("Shop or plan not found.");
        if (workerLimitOverride is < 0 or > 100) return ServiceResult.Fail("Worker limit must be between 0 and 100.");

        var planChanged = shop.PlanId != plan.Id;
        shop.PlanId = plan.Id;
        shop.WorkerLimitOverride = workerLimitOverride;
        Log(shop, plan, SubscriptionAction.PlanChanged, by, shop.SubscriptionStartsOn, shop.SubscriptionEndsOn,
            $"{(planChanged ? "Plan changed. " : "")}Worker limit override: {(workerLimitOverride?.ToString() ?? "none")}");
        await _db.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    private void Log(Shop shop, SubscriptionPlan? plan, SubscriptionAction action, string by,
        DateTime? start, DateTime? end, string? note = null, BillingPeriod? period = null, bool recordPrice = true)
    {
        _db.SubscriptionEvents.Add(new SubscriptionEvent
        {
            ShopId = shop.Id,
            Action = action,
            PlanId = plan?.Id,
            PlanName = plan?.Name,
            // manual (no payment) activations record no price, so history never shows money that was not received
            Price = recordPrice && period.HasValue ? plan?.PriceFor(period.Value) : null,
            StartsOn = start,
            EndsOn = end,
            Note = period.HasValue ? $"{period} {note}".Trim() : note,
            PerformedBy = by
        });
    }
}