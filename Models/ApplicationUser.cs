using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace ScrapManagement.Web.Models;

public class ApplicationUser : IdentityUser
{
    [Required, StringLength(120)]
    public string FullName { get; set; } = string.Empty;

    /// <summary>Null for the main (platform) admin.</summary>
    public int? ShopId { get; set; }
    public Shop? Shop { get; set; }

    public bool IsShopAdmin { get; set; }
    public bool IsActive { get; set; } = true;

    // Worker permissions (the shop admin always has all of them).
     public bool CanManageCategories { get; set; }
    public bool CanAddProducts { get; set; }
     public bool CanEditProducts { get; set; }
    public bool CanManagePurchases { get; set; } = true;
    public bool CanViewReports { get; set; } = true;
    /// <summary>Stock quantities, stock page and stock report. Off by default; the shop admin turns it on per worker.</summary>

     public bool CanViewStock { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
