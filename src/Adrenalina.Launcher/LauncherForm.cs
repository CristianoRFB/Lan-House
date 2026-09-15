using System.Diagnostics;

namespace Adrenalina.Launcher;

internal sealed class LauncherForm : Form
{
    private readonly Label _status = new();

    public LauncherForm()
    {
        Text = "Adrenalina";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(620, 360);
        MinimumSize = new Size(620, 360);
        MaximizeBox = false;
        BackColor = Color.FromArgb(12, 18, 30);
        ForeColor = Color.White;

        Controls.Add(new Label { Text = "ADRENALINA", AutoSize = true, Font = new Font("Segoe UI", 26f, FontStyle.Bold), ForeColor = Color.White, Location = new Point(38, 28) });
        Controls.Add(new Label { Text = "Escolha como deseja abrir o sistema.", AutoSize = true, Font = new Font("Segoe UI", 11f), ForeColor = Color.FromArgb(183, 198, 220), Location = new Point(42, 78) });

        var adminButton = CreateRoleButton("ADMIN", "Gerenciar a Lan House", Color.FromArgb(57, 96, 168));
        adminButton.Location = new Point(42, 132);
        adminButton.Click += (_, _) => Launch("Adrenalina.Admin.exe");
        Controls.Add(adminButton);

        var clientButton = CreateRoleButton("CLIENTE", "Abrir esta estação", Color.FromArgb(52, 132, 84));
        clientButton.Location = new Point(318, 132);
        clientButton.Click += (_, _) => Launch(Path.Combine("Client", "Adrenalina.Client.exe"));
        Controls.Add(clientButton);

        _status.Text = "Selecione uma opção para continuar.";
        _status.AutoSize = true;
        _status.MaximumSize = new Size(540, 0);
        _status.Font = new Font("Segoe UI", 9.5f);
        _status.ForeColor = Color.FromArgb(164, 178, 198);
        _status.Location = new Point(42, 286);
        Controls.Add(_status);
    }

    private static Button CreateRoleButton(string title, string subtitle, Color color)
    {
        var button = new Button
        {
            Text = $"{title}{Environment.NewLine}{subtitle}",
            Size = new Size(250, 112),
            FlatStyle = FlatStyle.Flat,
            BackColor = color,
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 13f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderSize = 0;
        return button;
    }

    private void Launch(string relativePath)
    {
        var path = Path.Combine(AppContext.BaseDirectory, relativePath);
        if (!File.Exists(path))
        {
            _status.Text = $"Aplicativo não encontrado: {path}";
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            _status.Text = $"{Path.GetFileNameWithoutExtension(path)} aberto.";
        }
        catch (Exception exception)
        {
            _status.Text = $"Não foi possível abrir o aplicativo: {exception.Message}";
        }
    }
}
