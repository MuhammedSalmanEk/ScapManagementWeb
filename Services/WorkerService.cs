using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ScrapManagement.Web.Data;
using ScrapManagement.Web.Models;
using ScrapManagement.Web.Models.ViewModels;

namespace ScrapManagement.Web.Services;

public interface IWorkerService
{
    Task<WorkersIndexViewModel> ListAsync(int shopId, CancellationToken ct = default);
    Task<ApplicationUser?> GetAsync(int shopId, string userId, CancellationToken ct = default);
    Task<ServiceResult> CreateAsync(int shopId, WorkerFormViewModel model, CancellationToken ct = default);
    Task<ServiceResult> UpdateAsync(int shopId, WorkerFormViewModel model, CancellationToken ct = default);
}

public class WorkerService : IWorkerService
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public WorkerService(ApplicationDbContext db, UserManager<ApplicationUser> users)
    {
        _db = db;
        _users = users;
    }

    private async Task<(int Limit, int Active)> LimitsAsync(int shopId, string? excludeUserId, CancellationToken ct)
    {
        var shop = await _db.Shops.AsNoTracking().Include(s => s.Plan).FirstAsync(s => s.Id == shopId, ct);
        var limit = shop.WorkerLimitOverride ?? shop.Plan?.MaxWorkers ?? 0;
        var active = await _db.Users.CountAsync(u => u.ShopId == shopId && !u.IsShopAdmin && u.IsActive && u.Id != excludeUserId, ct);
        return (limit, active);
    }

    public async Task<WorkersIndexViewModel> ListAsync(int shopId, CancellationToken ct = default)
    {
        var workers = await _db.Users.AsNoTracking().Where(u => u.ShopId == shopId && !u.IsShopAdmin).OrderBy(u => u.FullName).ToListAsync(ct);
        var (limit, active) = await LimitsAsync(shopId, null, ct);
        return new WorkersIndexViewModel { Workers = workers, Limit = limit, ActiveCount = active };
    }

    public Task<ApplicationUser?> GetAsync(int shopId, string userId, CancellationToken ct = default) =>
        _db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.ShopId == shopId && !u.IsShopAdmin, ct);

    public async Task<ServiceResult> CreateAsync(int shopId, WorkerFormViewModel m, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(m.Password)) return ServiceResult.Fail("Password is required for a new worker.");

        var (limit, active) = await LimitsAsync(shopId, null, ct);
        if (active >= limit) return ServiceResult.Fail($"Your plan allows {limit} worker account(s). Deactivate one or contact the platform admin.");

        var email = m.Email.Trim();
        if (await _users.FindByEmailAsync(email) is not null) return ServiceResult.Fail("This email is already used by another account.");

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = m.FullName.Trim(),
            ShopId = shopId,
            IsShopAdmin = false,
            IsActive = true,
            CanAddProducts = m.CanAddProducts,
            CanEditProducts = m.CanEditProducts,
            CanManagePurchases = m.CanManagePurchases,
            CanViewReports = m.CanViewReports,
            CanViewStock = m.CanViewStock,
            CanManageCategories = m.CanManageCategories,
            
        };
        var result = await _users.CreateAsync(user, m.Password);
        if (!result.Succeeded) return ServiceResult.Fail(result.Errors.Select(e => e.Description));
        await _users.AddToRoleAsync(user, AppRoles.Worker);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> UpdateAsync(int shopId, WorkerFormViewModel m, CancellationToken ct = default)
    {
        var user = m.Id is null ? null : await GetAsync(shopId, m.Id, ct);
        if (user is null) return ServiceResult.Fail("Worker not found.");

        if (m.IsActive && !user.IsActive)
        {
            var (limit, active) = await LimitsAsync(shopId, user.Id, ct);
            if (active >= limit) return ServiceResult.Fail($"Your plan allows {limit} active worker(s).");
        }

        var email = m.Email.Trim();
        if (!string.Equals(email, user.Email, StringComparison.OrdinalIgnoreCase))
        {
            var other = await _users.FindByEmailAsync(email);
            if (other is not null && other.Id != user.Id) return ServiceResult.Fail("This email is already used by another account.");
            user.Email = email;
            user.UserName = email;
        }

        user.FullName = m.FullName.Trim();
        user.IsActive = m.IsActive;
        user.CanAddProducts = m.CanAddProducts;
        user.CanEditProducts = m.CanEditProducts;
        user.CanManagePurchases = m.CanManagePurchases;
        user.CanViewReports = m.CanViewReports;
        user.CanViewStock = m.CanViewStock;
        user.CanManageCategories = m.CanManageCategories;

        var update = await _users.UpdateAsync(user);
        if (!update.Succeeded) return ServiceResult.Fail(update.Errors.Select(e => e.Description));

        if (!string.IsNullOrWhiteSpace(m.Password))
        {
            var token = await _users.GeneratePasswordResetTokenAsync(user);
            var reset = await _users.ResetPasswordAsync(user, token, m.Password);
            if (!reset.Succeeded) return ServiceResult.Fail(reset.Errors.Select(e => e.Description));
        }

        // Forces the worker's existing cookie to refresh, so permission changes / deactivation apply immediately.
        await _users.UpdateSecurityStampAsync(user);
        return ServiceResult.Ok();
    }
}