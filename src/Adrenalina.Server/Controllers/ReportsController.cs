using Adrenalina.Application;
using Adrenalina.Server.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Adrenalina.Server.Controllers;

[Authorize]
public sealed class ReportsController(ICafeManagementService cafeService) : Controller
{
    [HttpGet("/relatorios")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        return View(new ReportsPageViewModel
        {
            Logs = await cafeService.GetRecentLogsAsync(80, cancellationToken),
            PendingRequests = await cafeService.GetPendingRequestsAsync(cancellationToken)
        });
    }

    [HttpPost("/relatorios/exportar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Export(ReportFilterRequest request, CancellationToken cancellationToken)
    {
        var file = await cafeService.ExportReportAsync(request, cancellationToken);
        if (file is null)
        {
            TempData["StatusMessage"] = "Formato de exportação inválido.";
            return RedirectToAction(nameof(Index));
        }

        return File(file.Content, file.ContentType, file.FileName);
    }
}
