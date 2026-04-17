using Adrenalina.Application;
using Adrenalina.Server.Infrastructure;
using Adrenalina.Server.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Adrenalina.Server.Controllers;

[Authorize]
public sealed class SettingsController(ICafeManagementService cafeService) : Controller
{
    [HttpGet("/configuracoes")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var settings = await cafeService.GetSettingsAsync(cancellationToken);
        return View(new SettingsPageViewModel
        {
            Form = new SettingsUpdateRequest
            {
                Id = settings.Id,
                CafeName = settings.CafeName,
                DefaultTheme = settings.DefaultTheme,
                UpdateMode = settings.UpdateMode,
                BackupCutoffLocalTime = settings.BackupCutoffLocalTime,
                BackupRetentionDays = settings.BackupRetentionDays,
                WelcomeMessage = settings.WelcomeMessage,
                GoodbyeMessage = settings.GoodbyeMessage,
                LockMessage = settings.LockMessage,
                AllowedProgramsCsv = settings.AllowedProgramsCsv,
                BlockedProgramsCsv = settings.BlockedProgramsCsv,
                LimitBandwidthEnabledByDefault = settings.LimitBandwidthEnabledByDefault,
                OfflineSyncEnabled = settings.OfflineSyncEnabled,
                ShowRemainingTimeByDefault = settings.ShowRemainingTimeByDefault,
                DefaultCommonAnnotationLimit = settings.DefaultCommonAnnotationLimit,
                DefaultPcHourlyRate = settings.DefaultPcHourlyRate,
                DefaultConsoleHourlyRate = settings.DefaultConsoleHourlyRate,
                DemoModeEnabled = settings.DemoModeEnabled,
                BrandLogoPath = settings.BrandLogoPath,
                AlertSoundPath = settings.AlertSoundPath
            }
        });
    }

    [HttpPost("/configuracoes/salvar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(SettingsUpdateRequest request, CancellationToken cancellationToken)
    {
        var result = await cafeService.SaveSettingsAsync(request, User.GetActorId(), cancellationToken);
        TempData["StatusMessage"] = result.Message;
        return RedirectToAction(nameof(Index));
    }
}
