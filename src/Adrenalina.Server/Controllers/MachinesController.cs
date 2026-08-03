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
    public async Task<IActionResult> Index(Guid? editId, CancellationToken cancellationToken)
    {
        var machines = await cafeService.GetMachinesAsync(cancellationToken);
        var selected = editId.HasValue ? machines.FirstOrDefault(machine => machine.Id == editId.Value) : null;
        return View(new MachinesPageViewModel
        {
            Machines = machines,
            Form = selected is null ? new MachineUpsertRequest() : new MachineUpsertRequest
            {
                Id = selected.Id,
                MachineKey = selected.MachineKey,
                Name = selected.Name,
                Kind = selected.Kind,
                GroupName = selected.GroupName,
                Observations = selected.Observations
            }
        });
    }

    [HttpPost("/maquinas/salvar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(MachineUpsertRequest request, CancellationToken cancellationToken)
    {
        var result = await cafeService.UpsertMachineAsync(request, User.GetActorId(), cancellationToken);
        TempData["StatusMessage"] = result.Message;
        return RedirectToAction(nameof(Index));
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
