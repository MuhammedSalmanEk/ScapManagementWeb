using System.Security.Claims;
using ScrapManagement.Web.Models;

namespace ScrapManagement.Web.Infrastructure;

public record CurrentUser(string Id, string Name);

public static class ClaimsExtensions
{
    public static string GetUserId(this ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
    public static string GetFullName(this ClaimsPrincipal user) => user.FindFirstValue(AppClaims.FullName) ?? user.Identity?.Name ?? "";
    public static CurrentUser ToCurrentUser(this ClaimsPrincipal user) => new(user.GetUserId(), user.GetFullName());
    public static bool HasPermission(this ClaimsPrincipal user, string permission) =>
        user.IsInRole(AppRoles.ShopAdmin) || user.HasClaim(AppClaims.Permission, permission);
}

public static class HttpContextExtensions
{
    /// <summary>Subscription state of the signed-in shop user (null for anonymous users and the main admin).</summary>
    public static ShopAccessInfo? GetShopAccess(this HttpContext ctx) =>
        ctx.Items.TryGetValue(ShopAccessMiddleware.ItemKey, out var v) ? v as ShopAccessInfo : null;
}
