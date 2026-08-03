using Adrenalina.Application;
using Adrenalina.Server.Infrastructure;
using Adrenalina.Server.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Adrenalina.Server.Controllers;

[Authorize(Roles = "Admin")]
public sealed class UsersController(ICafeManagementService cafeService) : Controller
{
    [HttpGet("/usuarios")]
    public async Task<IActionResult> Index(Guid? editId, CancellationToken cancellationToken)
    {
        var users = await cafeService.GetUsersAsync(cancellationToken);
        var selected = editId.HasValue ? users.FirstOrDefault(user => user.Id == editId.Value) : null;
        return View(new UsersPageViewModel
        {
            Users = users,
            Form = selected is null ? new UserUpsertRequest() : new UserUpsertRequest
            {
                Id = selected.Id,
                DisplayName = selected.DisplayName,
                Login = selected.Login,
                ProfileType = selected.ProfileType,
                Balance = selected.Balance,
                AnnotationLimit = selected.AnnotationLimit,
                IsTemporary = selected.IsTemporary,
                TemporaryUntilUtc = selected.TemporaryUntilUtc,
                Notes = selected.Notes,
                IsBlocked = selected.IsBlocked
            }
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
