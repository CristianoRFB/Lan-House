using System.Diagnostics;
using Adrenalina.Server.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Adrenalina.Server.Controllers;

[AllowAnonymous]
public sealed class ErrorController : Controller
{
    [Route("/error")]
    public IActionResult Index()
    {
        return View("~/Views/Shared/Error.cshtml", new ErrorViewModel
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
        });
    }
}
