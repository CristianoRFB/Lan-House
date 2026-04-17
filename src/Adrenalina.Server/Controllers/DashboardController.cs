using Adrenalina.Application;
using Adrenalina.Server.Infrastructure;
using Adrenalina.Server.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Adrenalina.Server.Controllers;

[Authorize]
public sealed class DashboardController(ICafeManagementService cafeService) : Controller
{
    [HttpGet("/")]
    [HttpGet("/dashboard")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var viewModel = new DashboardPageViewModel
        {
            Dashboard = await cafeService.GetDashboardAsync(cancellationToken),
            Settings = await cafeService.GetSettingsAsync(cancellationToken)
        };

        return View(viewModel);
    }

    [HttpPost("/dashboard/request")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResolveRequest(ClientRequestResolution request, CancellationToken cancellationToken)
    {
        var result = await cafeService.ResolveClientRequestAsync(request, User.GetActorId(), cancellationToken);
        TempData["StatusMessage"] = result.Message;
        return RedirectToAction(nameof(Index));
    }
}
