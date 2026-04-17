using Adrenalina.Application;
using Adrenalina.Server.Infrastructure;
using Adrenalina.Server.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Adrenalina.Server.Controllers;

[Authorize]
public sealed class SessionsController(ICafeManagementService cafeService) : Controller
{
    [HttpGet("/sessoes")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        return View(new SessionsPageViewModel
        {
            Sessions = await cafeService.GetSessionsAsync(cancellationToken),
            Machines = await cafeService.GetMachinesAsync(cancellationToken),
            Users = await cafeService.GetUsersAsync(cancellationToken)
        });
    }

    [HttpPost("/sessoes/iniciar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(SessionStartRequest request, CancellationToken cancellationToken)
    {
        var result = await cafeService.StartSessionAsync(request, User.GetActorId(), cancellationToken);
        TempData["StatusMessage"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("/sessoes/ajustar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Adjust(SessionAdjustRequest request, CancellationToken cancellationToken)
    {
        var result = await cafeService.AdjustSessionAsync(request, User.GetActorId(), cancellationToken);
        TempData["StatusMessage"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("/sessoes/encerrar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> End(Guid sessionId, string reason, CancellationToken cancellationToken)
    {
        var result = await cafeService.EndSessionAsync(sessionId, reason, User.GetActorId(), cancellationToken);
        TempData["StatusMessage"] = result.Message;
        return RedirectToAction(nameof(Index));
    }
}
