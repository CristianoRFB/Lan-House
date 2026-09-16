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
    public async Task<IActionResult> Index(Guid? editId, Guid? pairingId, CancellationToken cancellationToken)
    {
        var machines = await cafeService.GetMachinesAsync(cancellationToken);
        var selected = editId.HasValue ? machines.FirstOrDefault(machine => machine.Id == editId.Value) : null;
        var pairing = pairingId.HasValue ? await cafeService.GetMachinePairingAsync(pairingId.Value, cancellationToken) : null;
        if (pairing is not null && TempData.TryGetValue("PairingCode", out var code) && code is string pairingCode)
        {
            pairing.Code = pairingCode;
        }

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
            },
            Pairing = pairing
        });
    }

    [HttpPost("/maquinas/iniciar-configuracao")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StartPairing(Guid machineId, CancellationToken cancellationToken)
    {
        var pairing = await cafeService.StartMachinePairingAsync(new MachinePairingStartRequest { MachineId = machineId }, User.GetActorId(), cancellationToken);
        if (pairing is not null)
        {
            TempData["PairingCode"] = pairing.Code;
        }
        TempData["StatusMessage"] = pairing is null ? "Não foi possível iniciar o pareamento." : "Código de pareamento criado e válido por 10 minutos.";
        return RedirectToAction(nameof(Index), pairing is null ? null : new { pairingId = pairing.Id });
    }

    [HttpPost("/maquinas/aprovar-pareamento")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApprovePairing(Guid pairingId, CancellationToken cancellationToken)
    {
        TempData["StatusMessage"] = (await cafeService.ApproveMachinePairingAsync(pairingId, User.GetActorId(), cancellationToken)).Message;
        return RedirectToAction(nameof(Index), new { pairingId });
    }

    [HttpPost("/maquinas/revogar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Revoke(Guid machineId, CancellationToken cancellationToken)
    {
        TempData["StatusMessage"] = (await cafeService.RevokeMachineAsync(machineId, User.GetActorId(), cancellationToken)).Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("/maquinas/salvar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save([Bind(Prefix = "Form")] MachineUpsertRequest request, CancellationToken cancellationToken)
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
