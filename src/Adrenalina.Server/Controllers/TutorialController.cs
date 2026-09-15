using Adrenalina.Server.Help;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Adrenalina.Server.Controllers;

[Authorize]
public sealed class TutorialController : Controller
{
    [HttpGet("/ajuda")]
    [HttpGet("/tutorial")]
    public IActionResult Index()
    {
        ViewData["Title"] = "Ajuda";
        return View(HelpContent.Build(User.IsInRole("Admin")));
    }
}
