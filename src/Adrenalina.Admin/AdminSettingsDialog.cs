namespace Adrenalina.Admin;

public sealed class AdminSettingsDialog : Form
{
    private readonly CheckBox _repeatTutorialCheckBox = new();
    private readonly CheckBox _preferBrowserCheckBox = new();

    public AdminSettingsDialog(AdminDesktopOptions currentOptions)
    {
        ResultOptions = new AdminDesktopOptions
        {
            ShowTutorialOnNextLaunch = currentOptions.ShowTutorialOnNextLaunch,
            PreferExternalBrowser = currentOptions.PreferExternalBrowser
        };

        Text = "Configuracoes do ADMIN";
        Width = 560;
        Height = 340;
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(520, 300);
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
            RowCount = 5
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        layout.Controls.Add(new Label
        {
            Text = "Deixe o uso do ADMIN do jeito mais simples para esse computador.",
            AutoSize = true,
            MaximumSize = new Size(470, 0),
            Font = new Font("Segoe UI", 10f),
            ForeColor = Color.FromArgb(193, 204, 220),
            Margin = new Padding(0, 0, 0, 18)
        });

        _preferBrowserCheckBox.Text = "Abrir o painel no navegador padrao";
        _preferBrowserCheckBox.AutoSize = true;
        _preferBrowserCheckBox.Checked = currentOptions.PreferExternalBrowser;
        _preferBrowserCheckBox.Margin = new Padding(0, 0, 0, 10);
        layout.Controls.Add(_preferBrowserCheckBox);

        _repeatTutorialCheckBox.Text = "Mostrar o tutorial novamente na proxima abertura";
        _repeatTutorialCheckBox.AutoSize = true;
        _repeatTutorialCheckBox.Checked = currentOptions.ShowTutorialOnNextLaunch;
        _repeatTutorialCheckBox.Margin = new Padding(0, 0, 0, 18);
        layout.Controls.Add(_repeatTutorialCheckBox);

        layout.Controls.Add(new Label
        {
            Text = "Dica: se o painel embutido nao abrir, manter o navegador padrao como modo principal evita depender do WebView2.",
            AutoSize = true,
            MaximumSize = new Size(470, 0),
            Font = new Font("Segoe UI", 9.5f),
            ForeColor = Color.FromArgb(164, 178, 198),
            Margin = new Padding(0, 0, 0, 18)
        });

        var footerPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true
        };

        var saveButton = CreateButton("Salvar", Color.FromArgb(52, 132, 84));
        saveButton.Click += (_, _) =>
        {
            ResultOptions = BuildResultOptions();
            DialogResult = DialogResult.OK;
            Close();
        };

        var tutorialButton = CreateButton("Abrir tutorial agora", Color.FromArgb(57, 96, 168));
        tutorialButton.Click += (_, _) =>
        {
            ResultOptions = BuildResultOptions();
            OpenTutorialNow = true;
            DialogResult = DialogResult.OK;
            Close();
        };

        var cancelButton = CreateButton("Cancelar", Color.FromArgb(89, 99, 117));
        cancelButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        footerPanel.Controls.Add(saveButton);
        footerPanel.Controls.Add(tutorialButton);
        footerPanel.Controls.Add(cancelButton);
        layout.Controls.Add(footerPanel);

        Controls.Add(layout);
    }

    public AdminDesktopOptions ResultOptions { get; private set; }

    public bool OpenTutorialNow { get; private set; }

    private AdminDesktopOptions BuildResultOptions()
    {
        return new AdminDesktopOptions
        {
            ShowTutorialOnNextLaunch = _repeatTutorialCheckBox.Checked,
            PreferExternalBrowser = _preferBrowserCheckBox.Checked
        };
    }

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
