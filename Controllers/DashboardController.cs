using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScrapManagement.Web.Models;
using ScrapManagement.Web.Services;

namespace ScrapManagement.Web.Controllers;

[Authorize(Policy = PolicyNames.ShopUser)]
public class DashboardController : Controller
{
    private readonly IReportService _reports;
    public DashboardController(IReportService reports) => _reports = reports;

    public async Task<IActionResult> Index(CancellationToken ct) => View(await _reports.GetDashboardAsync(ct));
}
