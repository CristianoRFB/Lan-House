using Adrenalina.Application;
using Adrenalina.Server.Infrastructure;
using Adrenalina.Server.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Adrenalina.Server.Controllers;

[Authorize]
public sealed class UsersController(ICafeManagementService cafeService) : Controller
{
    [HttpGet("/usuarios")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        return View(new UsersPageViewModel
        {
            Users = await cafeService.GetUsersAsync(cancellationToken)
        });
    }

    [HttpPost("/usuarios/salvar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(UserUpsertRequest request, CancellationToken cancellationToken)
    {
        var result = await cafeService.UpsertUserAsync(request, User.GetActorId(), cancellationToken);
        TempData["StatusMessage"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("/usuarios/financeiro")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Ledger(LedgerEntryRequest request, CancellationToken cancellationToken)
    {
        var result = await cafeService.AddLedgerEntryAsync(request, User.GetActorId(), cancellationToken);
        TempData["StatusMessage"] = result.Message;
        return RedirectToAction(nameof(Index));
    }
}
