using Adrenalina.Application;
using Adrenalina.Server.Infrastructure;
using Adrenalina.Server.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Adrenalina.Server.Controllers;

[Authorize]
public sealed class MachinesController(ICafeManagementService cafeService) : Controller
{
    [HttpGet("/maquinas")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        return View(new MachinesPageViewModel
        {
            Machines = await cafeService.GetMachinesAsync(cancellationToken)
        });
    }

    [HttpPost("/maquinas/comando")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Command(MachineCommandRequest request, CancellationToken cancellationToken)
    {
        var result = await cafeService.QueueMachineCommandAsync(request, User.GetActorId(), cancellationToken);
        TempData["StatusMessage"] = result.Message;
        return RedirectToAction(nameof(Index));
    }
}
