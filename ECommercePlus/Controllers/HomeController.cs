using System.Diagnostics;
using ECommercePlus.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace ECommercePlus.Controllers;

public class HomeController : Controller
{
    public IActionResult Index() => RedirectToAction("Index", "Shop");

    [Route("/error/{statusCode:int?}")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error(int? statusCode)
    {
        if (statusCode is not null)
            Response.StatusCode = statusCode.Value;
        return View(new ErrorViewModel(Activity.Current?.Id ?? HttpContext.TraceIdentifier, statusCode));
    }
}
