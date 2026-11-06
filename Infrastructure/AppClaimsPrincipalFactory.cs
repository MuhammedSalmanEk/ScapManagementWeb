using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using ScrapManagement.Web.Models;

namespace ScrapManagement.Web.Infrastructure;

/// <summary>Puts shop id, display name and worker permissions into the auth cookie.</summary>
public class AppClaimsPrincipalFactory : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>
{
    public AppClaimsPrincipalFactory(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IOptions<IdentityOptions> options)
        : base(userManager, roleManager, options)
    {
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        identity.AddClaim(new Claim(AppClaims.FullName, user.FullName));

        if (user.ShopId.HasValue)
            identity.AddClaim(new Claim(AppClaims.ShopId, user.ShopId.Value.ToString()));

        if (user.IsShopAdmin || user.CanAddProducts)
            identity.AddClaim(new Claim(AppClaims.Permission, Permissions.AddProducts));
        if (user.IsShopAdmin || user.CanEditProducts)
            identity.AddClaim(new Claim(AppClaims.Permission, Permissions.EditProducts));
        if (user.IsShopAdmin || user.CanManageCategories)
            identity.AddClaim(new Claim(AppClaims.Permission, Permissions.ManageCategories));
        if (user.IsShopAdmin || user.CanManagePurchases)
            identity.AddClaim(new Claim(AppClaims.Permission, Permissions.ManagePurchases));
        if (user.IsShopAdmin || user.CanViewReports)
            identity.AddClaim(new Claim(AppClaims.Permission, Permissions.ViewReports));
        if (user.IsShopAdmin || user.CanViewStock)
            identity.AddClaim(new Claim(AppClaims.Permission, Permissions.ViewStock));

        return identity;
    }
}