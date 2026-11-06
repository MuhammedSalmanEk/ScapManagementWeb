using System.Security.Claims;
using ScrapManagement.Web.Models;

namespace ScrapManagement.Web.Infrastructure;

public interface ITenantProvider
{
    /// <summary>The signed-in user's shop, or null (anonymous / main admin).</summary>
    int? ShopId { get; }
}

public class TenantProvider : ITenantProvider
{
    private readonly IHttpContextAccessor _accessor;
    public TenantProvider(IHttpContextAccessor accessor) => _accessor = accessor;

    public int? ShopId =>
        int.TryParse(_accessor.HttpContext?.User.FindFirstValue(AppClaims.ShopId), out var id) ? id : null;
}
