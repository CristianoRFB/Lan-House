namespace Adrenalina.Admin;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

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

        System.Windows.Forms.Application.Run(new MainForm());
    }
}
