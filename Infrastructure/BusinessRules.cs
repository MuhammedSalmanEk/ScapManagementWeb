using ScrapManagement.Web.Models;

namespace ScrapManagement.Web.Infrastructure;

/// <summary>Small pure functions so the important rules are easy to unit test.</summary>
public static class PurchaseCalculator
{
    public static decimal RoundQty(decimal kg) => Math.Round(kg, 3, MidpointRounding.AwayFromZero);
    public static decimal RoundRate(decimal rate) => Math.Round(rate, 2, MidpointRounding.AwayFromZero);
    public static decimal LineTotal(decimal qtyKg, decimal ratePerKg) =>
        Math.Round(RoundQty(qtyKg) * RoundRate(ratePerKg), 2, MidpointRounding.AwayFromZero);
}

public static class InvoiceNumbers
{
    public static string Format(int year, int number) => $"PUR-{year}-{number:D6}";
}

public static class SubscriptionRules
{
    public static ShopAccess Evaluate(ShopStatus status, DateTime? endsOnUtc, DateTime nowUtc) => status switch
    {
        ShopStatus.Pending => ShopAccess.Pending,
        ShopStatus.Suspended or ShopStatus.Terminated => ShopAccess.Blocked,
        // ShopStatus.Suspended => ShopAccess.Blocked,
        _ => endsOnUtc.HasValue && endsOnUtc.Value >= nowUtc ? ShopAccess.Full : ShopAccess.ReadOnly
    };

    /// <summary>Renewal always adds one year to the later of "now" and the current end date.</summary>
    // public static (DateTime Start, DateTime End) Renew(DateTime? currentEndUtc, DateTime nowUtc)
    // {
    //     var start = currentEndUtc.HasValue && currentEndUtc.Value > nowUtc ? currentEndUtc.Value : nowUtc;
    //     return (start, start.AddYears(1));
    // }

    public static (DateTime Start, DateTime End) Renew(DateTime? currentEndUtc, DateTime nowUtc,
    BillingPeriod period = BillingPeriod.Yearly)
    {
        var start = currentEndUtc.HasValue && currentEndUtc.Value > nowUtc ? currentEndUtc.Value : nowUtc;
        return (start, period == BillingPeriod.Monthly ? start.AddMonths(1) : start.AddYears(1));
    }
}

/// <summary>What the layout and middleware know about the signed-in user's shop for this request.</summary>
public class ShopAccessInfo
{
    public Shop Shop { get; init; } = null!;
    public ShopAccess Access { get; init; }
    public int? DaysLeft { get; init; }
    public SubscriptionPlan? Plan => Shop.Plan;
}
