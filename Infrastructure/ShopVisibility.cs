using ScrapManagement.Web.Models;

namespace ScrapManagement.Web.Infrastructure;

/// <summary>
/// What the signed-in user may see. Shop admins always see everything.
/// Workers: stock needs "Can view stock"; bills, totals and money summaries need "Can view reports".
/// </summary>
public static class ShopVisibility
{
    public static bool CanViewStock(this HttpContext ctx) =>
        ctx.User.IsInRole(AppRoles.ShopAdmin) || ctx.User.HasPermission(Permissions.ViewStock);

    /// <summary>All bills, recent-bill lists and money totals.</summary>
    public static bool CanViewAllBills(this HttpContext ctx) =>
        ctx.User.IsInRole(AppRoles.ShopAdmin) || ctx.User.HasPermission(Permissions.ViewReports);
}