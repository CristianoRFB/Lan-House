using Adrenalina.Application;
using Adrenalina.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Adrenalina.Admin;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        var logPath = Path.Combine(AdrenalinaPaths.GetAdminDataRoot(), "logs", "Admin.log");
        AdrenalinaFileLog.Write(logPath, LogLevel.Information, nameof(Program), "Aplicação administrativa iniciada.");
        System.Windows.Forms.Application.ThreadException += (_, eventArgs) =>
            AdrenalinaFileLog.Write(logPath, LogLevel.Error, "UI", "Erro não tratado na interface administrativa.", eventArgs.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
            AdrenalinaFileLog.Write(logPath, LogLevel.Critical, "Runtime", "Erro fatal na aplicação administrativa.", eventArgs.ExceptionObject as Exception);

        using var singleInstance = AdminSingleInstanceGuard.TryAcquire();
        if (singleInstance is null)
        {
            AdminSingleInstanceGuard.TryActivateExistingWindow();
            MessageBox.Show(
                "O Adrenalina ADMIN ja esta aberto. Volte para a janela que ja esta em execucao.",
                "Adrenalina ADMIN",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        try
        {
            System.Windows.Forms.Application.Run(new MainForm());
        }
        finally
        {
            AdrenalinaFileLog.Write(logPath, LogLevel.Information, nameof(Program), "Aplicação administrativa encerrada.");
        }
    }
}
