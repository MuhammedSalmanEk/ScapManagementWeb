using System.ComponentModel.DataAnnotations;

namespace ScrapManagement.Web.Models;

public class Category : ITenantEntity
{
    public int Id { get; set; }
    public int ShopId { get; set; }

    [Required, StringLength(80)]
    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}
