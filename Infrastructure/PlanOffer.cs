using ScrapManagement.Web.Models;

namespace ScrapManagement.Web.Infrastructure;

/// <summary>One purchasable option: a plan with a billing period and its price.</summary>
public record PlanOffer(int PlanId, string PlanName, BillingPeriod Period, decimal Price, int MaxWorkers)
{
    /// <summary>Value used in the radio button, for example "1:Monthly".</summary>
    public string Key => $"{PlanId}:{Period}";

    public static bool TryParse(string? key, out int planId, out BillingPeriod period)
    {
        planId = 0; period = BillingPeriod.Yearly;
        if (string.IsNullOrWhiteSpace(key)) return false;
        var parts = key.Split(':');
        return parts.Length == 2
            && int.TryParse(parts[0], out planId)
            && Enum.TryParse(parts[1], out period)
            && Enum.IsDefined(period);
    }
}