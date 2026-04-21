using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Adrenalina.Server.Controllers;

[Authorize]
public sealed class TutorialController : Controller
{
    [HttpGet("/tutorial")]
    public IActionResult Index()
    {
        ViewData["Title"] = "Tutorial";
        return View();
    }
}
