using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScrapManagement.Web.Services;

namespace ScrapManagement.Web.Controllers;

[IgnoreAntiforgeryToken]
[ApiController, AllowAnonymous, Route("webhooks/razorpay")]
public class RazorpayWebhookController : ControllerBase
{
    private readonly IPaymentService _payments;
    public RazorpayWebhookController(IPaymentService payments) => _payments = payments;

    [HttpPost]
    public async Task<IActionResult> Receive(CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync(ct);
        var signature = Request.Headers["X-Razorpay-Signature"].FirstOrDefault();
        var result = await _payments.HandleWebhookAsync(body, signature, ct);
        return result.Succeeded ? Ok() : BadRequest();
    }
}