namespace Adrenalina.Admin;

public sealed class AdminExitDialog : Form
{
    private readonly CheckBox _shutdownForTodayCheckBox = new();

    public AdminExitDialog(bool serverRunning)
    {
        Text = "Fechar sistema";
        Width = 640;
        Height = 320;
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(620, 280);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.FromArgb(18, 23, 35);
        ForeColor = Color.White;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24),
            ColumnCount = 1,
            RowCount = 4
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        layout.Controls.Add(new Label
        {
            Text = "Deseja fechar o sistema?",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 18f, FontStyle.Bold),
            ForeColor = Color.White,
            Margin = new Padding(0, 0, 0, 10)
        });

        layout.Controls.Add(new Label
        {
            Text = serverRunning
                ? "Ao fechar, o ADMIN encerra o servidor local, libera a porta em uso e solta os arquivos do build. Se quiser continuar usando o sistema depois, basta abrir o aplicativo novamente."
                : "O servidor local ja esta parado. Fechar agora encerra o aplicativo e libera o ambiente para a proxima execucao ou compilacao.",
            AutoSize = true,
            MaximumSize = new Size(560, 0),
            Font = new Font("Segoe UI", 10f),
            ForeColor = Color.FromArgb(193, 204, 220),
            Margin = new Padding(0, 0, 0, 18)
        });

        _shutdownForTodayCheckBox.Text = "Encerrar o ambiente admin por hoje e gerar um backup final antes de sair";
        _shutdownForTodayCheckBox.AutoSize = true;
        _shutdownForTodayCheckBox.Enabled = serverRunning;
        _shutdownForTodayCheckBox.Checked = serverRunning;
        _shutdownForTodayCheckBox.ForeColor = serverRunning
            ? Color.White
            : Color.FromArgb(132, 144, 166);
        layout.Controls.Add(_shutdownForTodayCheckBox);

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };

        var exitButton = CreateButton("Fechar o sistema", Color.FromArgb(181, 76, 76));
        exitButton.Click += (_, _) =>
        {
            ExitApplication = true;
            DialogResult = DialogResult.OK;
            Close();
        };

        var cancelButton = CreateButton("Cancelar", Color.FromArgb(89, 99, 117));
        cancelButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        buttonPanel.Controls.Add(exitButton);
        buttonPanel.Controls.Add(cancelButton);
        layout.Controls.Add(buttonPanel);

        Controls.Add(layout);
    }

    public bool ExitApplication { get; private set; }

    public bool ShutdownForToday => _shutdownForTodayCheckBox.Enabled && _shutdownForTodayCheckBox.Checked;

    private static Button CreateButton(string text, Color backColor)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            Padding = new Padding(16, 8, 16, 8),
            FlatStyle = FlatStyle.Flat,
            BackColor = backColor,
            ForeColor = Color.White,
            Margin = new Padding(8, 0, 0, 0)
        };

        button.FlatAppearance.BorderSize = 0;
        return button;
    }
}
