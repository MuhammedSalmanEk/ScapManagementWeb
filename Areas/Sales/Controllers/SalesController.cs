using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScrapManagement.Web.Infrastructure;
using ScrapManagement.Web.Models;

namespace ScrapManagement.Web.Areas.Sales.Controllers;

/// <summary>
/// Placeholder for the future Sales module. It lives in its own MVC Area so it can be built without touching
/// the purchase code. See Modules/AppModules.cs and docs/ADDING-THE-SALES-MODULE.md.
/// </summary>
[Area("Sales")]
[Route("sales")]
[Authorize(Policy = PolicyNames.ShopUser)]
public class SalesController : Controller
{
    [HttpGet("")]
    public IActionResult Index()
    {
        var info = HttpContext.GetShopAccess();
        ViewBag.EnabledForPlan = info?.Plan?.SalesModuleEnabled == true;
        return View();
    }
}
