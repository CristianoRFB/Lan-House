using Adrenalina.Application;
using Adrenalina.Domain;

namespace Adrenalina.Server.ViewModels;

public sealed class ReportsPageViewModel
{
    public ReportFilterRequest Filter { get; init; } = new()
    {
        StartDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-30)),
        EndDate = DateOnly.FromDateTime(DateTime.Today),
        Format = ReportExportFormat.Txt
    };

    public IReadOnlyList<AuditLogDto> Logs { get; init; } = [];
    public IReadOnlyList<ClientRequestDto> PendingRequests { get; init; } = [];
}
