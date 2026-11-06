namespace ScrapManagement.Web.Models;

/// <summary>Anything that belongs to one shop. A global query filter keeps shops isolated.</summary>
public interface ITenantEntity
{
    int ShopId { get; set; }
}
