using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ScrapManagement.Web.Data;
using ScrapManagement.Web.Infrastructure;
using ScrapManagement.Web.Models;

namespace ScrapManagement.Web.Services;

public record CheckoutInfo(string KeyId, string OrderId, long AmountPaise, string Currency, string ShopName, string Description);

public interface IPaymentService
{
    bool Enabled { get; }
    Task<ServiceResult<CheckoutInfo>> CreateOrderAsync(int shopId, int planId, BillingPeriod period, CancellationToken ct = default);
    Task<ServiceResult> ConfirmAsync(string orderId, string paymentId, string? checkoutSignature, CancellationToken ct = default);
    Task<ServiceResult> HandleWebhookAsync(string body, string? signature, CancellationToken ct = default);
    Task<ServiceResult> SetCreditedAsync(int paymentId, decimal? amount, DateTime? creditedOnUtc, string? note, string by, CancellationToken ct = default);
}

public class PaymentService : IPaymentService
{
    private readonly ApplicationDbContext _db;
    private readonly IClock _clock;
    private readonly IHttpClientFactory _http;
    private readonly RazorpayOptions _opt;
    private readonly ILogger<PaymentService> _log;

    public PaymentService(ApplicationDbContext db, IClock clock, IHttpClientFactory http,
        IOptions<RazorpayOptions> opt, ILogger<PaymentService> log)
    {
        _db = db; _clock = clock; _http = http; _opt = opt.Value; _log = log;
    }

    public bool Enabled => _opt.Enabled;

    public async Task<ServiceResult<CheckoutInfo>> CreateOrderAsync(int shopId, int planId, BillingPeriod period, CancellationToken ct = default)
    {
        if (!Enabled) return ServiceResult<CheckoutInfo>.Fail("Online payment is not set up yet. Please contact the platform admin.");

        var shop = await _db.Shops.FirstOrDefaultAsync(s => s.Id == shopId, ct);
        if (shop is null) return ServiceResult<CheckoutInfo>.Fail("Shop not found.");
        if (shop.Status is ShopStatus.Suspended or ShopStatus.Terminated)
            return ServiceResult<CheckoutInfo>.Fail("This shop is blocked. Please contact the platform admin.");

        var plan = await _db.SubscriptionPlans.FirstOrDefaultAsync(p => p.Id == planId && p.IsActive, ct);
        if (plan is null || !plan.Offers(period) || plan.PriceFor(period) <= 0)
            return ServiceResult<CheckoutInfo>.Fail("This plan / billing option is not available.");

        var price = plan.PriceFor(period);
        var payment = new Payment { ShopId = shop.Id, PlanId = plan.Id, PlanName = plan.Name, Period = period, Amount = price };
        _db.Payments.Add(payment);
        await _db.SaveChangesAsync(ct);

        var amountPaise = (long)Math.Round(price * 100m, MidpointRounding.AwayFromZero);
        var body = new
        {
            amount = amountPaise,
            currency = "INR",
            receipt = $"pay_{payment.Id}",
            notes = new { shopId = shop.Id.ToString(), paymentId = payment.Id.ToString() }
        };

        var client = _http.CreateClient("razorpay");
        using var resp = await client.PostAsJsonAsync("orders", body, ct);
        var text = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            _log.LogError("Razorpay order failed: {Status} {Body}", (int)resp.StatusCode, text);
            payment.Status = PaymentStatus.Failed;
            await _db.SaveChangesAsync(ct);
            return ServiceResult<CheckoutInfo>.Fail("Could not start the payment. Please try again.");
        }

        using var doc = JsonDocument.Parse(text);
        var orderId = doc.RootElement.GetProperty("id").GetString()!;
        payment.RazorpayOrderId = orderId;
        await _db.SaveChangesAsync(ct);

        return ServiceResult<CheckoutInfo>.Ok(new CheckoutInfo(_opt.KeyId, orderId, amountPaise, "INR",
            shop.Name, $"{plan.Name} - {period}"));
    }

    /// <summary>Marks the payment paid and activates / extends the subscription. Safe to call twice (browser + webhook).</summary>
    public async Task<ServiceResult> ConfirmAsync(string orderId, string paymentId, string? checkoutSignature, CancellationToken ct = default)
    {
        if (checkoutSignature is not null && !HmacEquals(_opt.KeySecret, $"{orderId}|{paymentId}", checkoutSignature))
            return ServiceResult.Fail("Payment verification failed.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var payment = await _db.Payments.AsNoTracking().FirstOrDefaultAsync(p => p.RazorpayOrderId == orderId, ct);
        if (payment is null) return ServiceResult.Fail("Payment not found.");

        var now = _clock.UtcNow;
        var flipped = await _db.Payments
            .Where(p => p.Id == payment.Id && p.Status != PaymentStatus.Paid)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Status, PaymentStatus.Paid)
                .SetProperty(p => p.PaidAt, (DateTime?)now)
                .SetProperty(p => p.RazorpayPaymentId, paymentId)
                .SetProperty(p => p.RazorpaySignature, checkoutSignature), ct);

        if (flipped == 0) { await tx.RollbackAsync(ct); return ServiceResult.Ok(); } // already processed

        var shop = await _db.Shops.FirstAsync(s => s.Id == payment.ShopId, ct);
        var wasPending = shop.Status == ShopStatus.Pending;
        var (start, end) = SubscriptionRules.Renew(shop.SubscriptionEndsOn, now, payment.Period);

        shop.PlanId = payment.PlanId;
        shop.BillingPeriod = payment.Period;
        shop.SubscriptionStartsOn ??= start;
        shop.SubscriptionEndsOn = end;
        if (wasPending) shop.Status = ShopStatus.Active;

        _db.SubscriptionEvents.Add(new SubscriptionEvent
        {
            ShopId = shop.Id,
            Action = wasPending ? SubscriptionAction.Activated : SubscriptionAction.Renewed,
            PlanId = payment.PlanId,
            PlanName = payment.PlanName,
            Price = payment.Amount,
            StartsOn = start,
            EndsOn = end,
            Note = $"{payment.Period} - Razorpay {paymentId}",
            PerformedBy = "Razorpay"
        });

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> HandleWebhookAsync(string body, string? signature, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_opt.WebhookSecret) || string.IsNullOrWhiteSpace(signature)
            || !HmacEquals(_opt.WebhookSecret, body, signature))
            return ServiceResult.Fail("Invalid signature.");

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var ev = root.GetProperty("event").GetString();
        if (ev is not ("payment.captured" or "order.paid")) return ServiceResult.Ok();

        var entity = root.GetProperty("payload").GetProperty("payment").GetProperty("entity");
        var orderId = entity.GetProperty("order_id").GetString();
        var payId = entity.GetProperty("id").GetString();
        var paidPaise = entity.GetProperty("amount").GetInt64();
        if (orderId is null || payId is null) return ServiceResult.Ok();

        var payment = await _db.Payments.AsNoTracking().FirstOrDefaultAsync(p => p.RazorpayOrderId == orderId, ct);
        if (payment is null) return ServiceResult.Ok();
        if ((long)Math.Round(payment.Amount * 100m) != paidPaise) return ServiceResult.Fail("Amount mismatch.");

        return await ConfirmAsync(orderId, payId, null, ct);
    }

    public async Task<ServiceResult> SetCreditedAsync(int paymentId, decimal? amount, DateTime? creditedOnUtc, string? note, string by, CancellationToken ct = default)
    {
        var p = await _db.Payments.FirstOrDefaultAsync(x => x.Id == paymentId, ct);
        if (p is null || p.Status != PaymentStatus.Paid) return ServiceResult.Fail("Only paid payments can be marked as credited.");
        if (amount is < 0) return ServiceResult.Fail("Amount cannot be negative.");

        p.CreditedAmount = amount;
        p.CreditedOn = amount.HasValue ? (creditedOnUtc ?? _clock.UtcNow) : null;
        p.CreditedNote = note?.Trim();
        p.CreditedBy = by;
        await _db.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    private static bool HmacEquals(string secret, string payload, string signature)
    {
        using var h = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var expected = Convert.ToHexString(h.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(signature.Trim().ToLowerInvariant()));
    }
}