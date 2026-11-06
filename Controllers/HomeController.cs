using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ScrapManagement.Web.Models;

namespace ScrapManagement.Web.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        if (User.Identity?.IsAuthenticated != true) return View();
        if (User.IsInRole(AppRoles.MainAdmin)) return RedirectToAction("Dashboard", "AdminShops");
        return RedirectToAction("Index", "Dashboard");
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
