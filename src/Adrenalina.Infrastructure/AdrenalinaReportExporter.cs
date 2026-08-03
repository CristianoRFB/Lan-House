using System.Text;
using Adrenalina.Application;
using Adrenalina.Domain;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace Adrenalina.Infrastructure;

public sealed class AdrenalinaReportExporter
{
    public FileExportResult? Export(
        string cafeName,
        ReportFilterRequest request,
        IReadOnlyList<SessionRecord> sessions,
        IReadOnlyList<LedgerEntry> ledger,
        IReadOnlyDictionary<Guid, Machine> machines,
        IReadOnlyDictionary<Guid, UserAccount> users)
    {
        if (!Enum.IsDefined(request.Format) || request.StartDate > request.EndDate ||
            request.EndDate.DayNumber - request.StartDate.DayNumber > 366)
        {
            return null;
        }

        var summaryLines = BuildSummaryLines(cafeName, request, sessions, ledger, machines, users);
        return request.Format switch
        {
            ReportExportFormat.Txt => new FileExportResult(
                $"relatorio-{request.StartDate:yyyyMMdd}-{request.EndDate:yyyyMMdd}.txt",
                "text/plain; charset=utf-8",
                Encoding.UTF8.GetBytes(string.Join(Environment.NewLine, summaryLines))),
            ReportExportFormat.Excel => new FileExportResult(
                $"relatorio-{request.StartDate:yyyyMMdd}-{request.EndDate:yyyyMMdd}.xlsx",
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                BuildExcel(summaryLines, sessions, ledger, machines, users)),
            ReportExportFormat.Pdf => new FileExportResult(
                $"relatorio-{request.StartDate:yyyyMMdd}-{request.EndDate:yyyyMMdd}.pdf",
                "application/pdf",
                BuildPdf(summaryLines, sessions, ledger, machines, users)),
            _ => null
        };
    }

    private static IReadOnlyList<string> BuildSummaryLines(
        string cafeName,
        ReportFilterRequest request,
        IReadOnlyList<SessionRecord> sessions,
        IReadOnlyList<LedgerEntry> ledger,
        IReadOnlyDictionary<Guid, Machine> machines,
        IReadOnlyDictionary<Guid, UserAccount> users)
    {
        var pcSessions = sessions.Where(entry => entry.MachineKind == MachineKind.Pc).ToList();
        var consoleSessions = sessions.Where(entry => entry.MachineKind == MachineKind.Console).ToList();

        return
        [
            cafeName,
            $"Período: {request.StartDate:dd/MM/yyyy} a {request.EndDate:dd/MM/yyyy}",
            $"Sessões de PC: {pcSessions.Count}",
            $"Sessões de console: {consoleSessions.Count}",
            $"Tempo total PCs: {pcSessions.Sum(entry => entry.ConsumedMinutes)} min",
            $"Tempo total consoles: {consoleSessions.Sum(entry => entry.ConsumedMinutes)} min",
            $"Valor anotado: R$ {ledger.Where(entry => entry.Type == LedgerEntryType.Annotation).Sum(entry => entry.Amount):N2}",
            $"Pagamentos prometidos: R$ {ledger.Where(entry => entry.Type == LedgerEntryType.PaymentPromise).Sum(entry => entry.Amount):N2}",
            $"Usuários atendidos: {sessions.Select(entry => entry.UserAccountId).Where(entry => entry.HasValue).Distinct().Count()}",
            "",
            "Sessões",
            .. sessions.Select(entry =>
            {
                var machineName = machines.TryGetValue(entry.MachineId, out var machine) ? machine.Name : "Desconhecida";
                var userName = entry.UserAccountId.HasValue && users.TryGetValue(entry.UserAccountId.Value, out var user)
                    ? user.DisplayName
                    : entry.UserDisplayName;
                return $"- {machineName} | {userName} | {entry.MachineKind} | {entry.ConsumedMinutes} min | R$ {entry.TotalSpent:N2}";
            }),
            "",
            "Financeiro",
            .. ledger.Select(entry =>
            {
                var userName = users.TryGetValue(entry.UserAccountId, out var user) ? user.DisplayName : "Desconhecido";
                var dueDate = entry.PromisedPaymentDateUtc.HasValue ? $" | vence {entry.PromisedPaymentDateUtc.Value:dd/MM/yyyy}" : string.Empty;
                return $"- {entry.Type} | {userName} | R$ {entry.Amount:N2}{dueDate} | {entry.Description}";
            })
        ];
    }

    private static byte[] BuildExcel(
        IReadOnlyList<string> summaryLines,
        IReadOnlyList<SessionRecord> sessions,
        IReadOnlyList<LedgerEntry> ledger,
        IReadOnlyDictionary<Guid, Machine> machines,
        IReadOnlyDictionary<Guid, UserAccount> users)
    {
        using var workbook = new XLWorkbook();
        var summary = workbook.Worksheets.Add("Resumo");
        for (var index = 0; index < summaryLines.Count; index++)
        {
            summary.Cell(index + 1, 1).Value = summaryLines[index];
        }
        summary.Column(1).AdjustToContents();

        var sessionsSheet = workbook.Worksheets.Add("Sessoes");
        var sessionHeaders = new[] { "Máquina", "Usuário", "Tipo", "Minutos", "Valor" };
        for (var column = 0; column < sessionHeaders.Length; column++)
        {
            sessionsSheet.Cell(1, column + 1).Value = sessionHeaders[column];
        }
        for (var row = 0; row < sessions.Count; row++)
        {
            var session = sessions[row];
            sessionsSheet.Cell(row + 2, 1).Value = machines.TryGetValue(session.MachineId, out var machine) ? machine.Name : "Desconhecida";
            sessionsSheet.Cell(row + 2, 2).Value = session.UserAccountId.HasValue && users.TryGetValue(session.UserAccountId.Value, out var user) ? user.DisplayName : session.UserDisplayName;
            sessionsSheet.Cell(row + 2, 3).Value = session.MachineKind.ToString();
            sessionsSheet.Cell(row + 2, 4).Value = session.ConsumedMinutes;
            sessionsSheet.Cell(row + 2, 5).Value = session.TotalSpent;
        }
        sessionsSheet.Columns().AdjustToContents();

        var ledgerSheet = workbook.Worksheets.Add("Financeiro");
        var ledgerHeaders = new[] { "Usuário", "Tipo", "Valor", "Descrição", "Promessa" };
        for (var column = 0; column < ledgerHeaders.Length; column++)
        {
            ledgerSheet.Cell(1, column + 1).Value = ledgerHeaders[column];
        }
        for (var row = 0; row < ledger.Count; row++)
        {
            var item = ledger[row];
            ledgerSheet.Cell(row + 2, 1).Value = users.TryGetValue(item.UserAccountId, out var user) ? user.DisplayName : "Desconhecido";
            ledgerSheet.Cell(row + 2, 2).Value = item.Type.ToString();
            ledgerSheet.Cell(row + 2, 3).Value = item.Amount;
            ledgerSheet.Cell(row + 2, 4).Value = item.Description;
            ledgerSheet.Cell(row + 2, 5).Value = item.PromisedPaymentDateUtc?.ToString("dd/MM/yyyy") ?? string.Empty;
        }
        ledgerSheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static byte[] BuildPdf(
        IReadOnlyList<string> summaryLines,
        IReadOnlyList<SessionRecord> sessions,
        IReadOnlyList<LedgerEntry> ledger,
        IReadOnlyDictionary<Guid, Machine> machines,
        IReadOnlyDictionary<Guid, UserAccount> users)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        return Document.Create(container => container.Page(page =>
        {
            page.Margin(20);
            page.DefaultTextStyle(text => text.FontSize(10));
            page.Header().Text("Relatório Adrenalina").SemiBold().FontSize(18);
            page.Content().Column(column =>
            {
                column.Spacing(10);
                column.Item().Text(string.Join(Environment.NewLine, summaryLines.Take(10)));
                column.Item().Text("Sessões").SemiBold();
                foreach (var session in sessions)
                {
                    var machine = machines.TryGetValue(session.MachineId, out var machineEntry) ? machineEntry.Name : "Desconhecida";
                    var user = session.UserAccountId.HasValue && users.TryGetValue(session.UserAccountId.Value, out var userEntry) ? userEntry.DisplayName : session.UserDisplayName;
                    column.Item().Text($"{machine} | {user} | {session.ConsumedMinutes} min | R$ {session.TotalSpent:N2}");
                }
                column.Item().Text("Financeiro").SemiBold();
                foreach (var item in ledger)
                {
                    var user = users.TryGetValue(item.UserAccountId, out var userEntry) ? userEntry.DisplayName : "Desconhecido";
                    column.Item().Text($"{item.Type} | {user} | R$ {item.Amount:N2} | {item.Description}");
                }
            });
            page.Footer().AlignRight().Text(text =>
            {
                text.Span("Página ");
                text.CurrentPageNumber();
            });
        })).GeneratePdf();
    }
}
