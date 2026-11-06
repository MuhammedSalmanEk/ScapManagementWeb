using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ScrapManagement.Web.Data;
using ScrapManagement.Web.Models;

namespace ScrapManagement.Web.Infrastructure;

/// <summary>
/// Enforces subscription rules for every shop user on every request:
///  - Pending / Suspended: only the status page and logout are reachable.
///  - Expired: read-only (GET pages work, anything that changes data is refused).
/// </summary>
public class ShopAccessMiddleware
{
    public const string ItemKey = "ShopAccessInfo";
    private static readonly string[] AlwaysAllowed =
        { "/Account/Logout", "/Subscription", "/css", "/js", "/lib", "/Home/Error", "/_framework" };

    private readonly RequestDelegate _next;
    public ShopAccessMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext ctx, ApplicationDbContext db, IClock clock)
    {
        var user = ctx.User;
        if (user.Identity?.IsAuthenticated == true
            && !user.IsInRole(AppRoles.MainAdmin)
            && int.TryParse(user.FindFirstValue(AppClaims.ShopId), out var shopId))
        {
            var shop = await db.Shops.AsNoTracking().Include(s => s.Plan).FirstOrDefaultAsync(s => s.Id == shopId);
            if (shop is not null)
            {
                var now = clock.UtcNow;
                var access = SubscriptionRules.Evaluate(shop.Status, shop.SubscriptionEndsOn, now);
                int? daysLeft = shop.SubscriptionEndsOn.HasValue
                    ? (int)Math.Ceiling((shop.SubscriptionEndsOn.Value - now).TotalDays)
                    : null;
                ctx.Items[ItemKey] = new ShopAccessInfo { Shop = shop, Access = access, DaysLeft = daysLeft };

                var path = ctx.Request.Path;
                var allowed = Path.HasExtension(path.Value) // static files (css, js, ico, scoped styles)
                              || AlwaysAllowed.Any(p => path.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase));
                var isRead = HttpMethods.IsGet(ctx.Request.Method) || HttpMethods.IsHead(ctx.Request.Method);
                var isApi = path.StartsWithSegments("/api");

                var denied = !allowed && (access is ShopAccess.Blocked or ShopAccess.Pending
                                          || (access == ShopAccess.ReadOnly && !isRead));
                if (denied)
                {
                    if (isApi)
                    {
                        ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                        await ctx.Response.WriteAsJsonAsync(new { error = "Subscription does not allow this action.", access = access.ToString() });
                    }
                    else
                    {
                        ctx.Response.Redirect("/Subscription/Status");
                    }
                    return;
                }
            }
        }

        await _next(ctx);
    }
}
