using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace Adrenalina.Admin;

public sealed class MainForm : Form
{
    private readonly EmbeddedAdminServer _server = new();
    private readonly WebView2 _webView = new();
    private readonly Button _serverToggleButton = new();
    private readonly Button _backupButton = new();
    private readonly Label _serverStatusLabel = new();
    private readonly Label _clientsStatusLabel = new();
    private readonly Label _urlStatusLabel = new();
    private readonly System.Windows.Forms.Timer _refreshTimer = new();
    private readonly NotifyIcon _trayIcon = new();
    private readonly ToolStripMenuItem _openMenuItem = new("Abrir painel");
    private readonly ToolStripMenuItem _stopMenuItem = new("Parar servidor");
    private readonly ToolStripMenuItem _exitMenuItem = new("Encerrar");

    private bool _allowExit;
    private bool _webViewReady;

    public MainForm()
    {
        Text = "Adrenalina ADMIN";
        Width = 1500;
        Height = 920;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1100, 720);

        BuildLayout();
        WireEvents();
    }

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        await StartServerAndNavigateAsync();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);

        if (WindowState == FormWindowState.Minimized)
        {
            HideToTray();
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_allowExit)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }

        _refreshTimer.Stop();
        _trayIcon.Visible = false;
        base.OnFormClosing(e);
    }

    protected override async void OnFormClosed(FormClosedEventArgs e)
    {
        await _server.DisposeAsync();
        _trayIcon.Dispose();
        _refreshTimer.Dispose();
        base.OnFormClosed(e);
    }

    private void BuildLayout()
    {
        BackColor = Color.FromArgb(18, 22, 31);

        var headerPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 90,
            Padding = new Padding(18, 14, 18, 14),
            ColumnCount = 5,
            BackColor = Color.FromArgb(25, 31, 44)
        };
        headerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
        headerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        headerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        headerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        headerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var titlePanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true
        };

        titlePanel.Controls.Add(new Label
        {
            Text = "ADRENALINA ADMIN",
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 15f, FontStyle.Bold),
            AutoSize = true
        });

        _urlStatusLabel.Text = _server.BaseAddress.ToString();
        _urlStatusLabel.ForeColor = Color.FromArgb(183, 196, 214);
        _urlStatusLabel.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
        _urlStatusLabel.AutoSize = true;
        titlePanel.Controls.Add(_urlStatusLabel);

        _serverStatusLabel.Text = "Servidor: inicializando";
        _serverStatusLabel.ForeColor = Color.White;
        _serverStatusLabel.Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold);
        _serverStatusLabel.AutoSize = true;

        _clientsStatusLabel.Text = "Clientes online: --/--";
        _clientsStatusLabel.ForeColor = Color.White;
        _clientsStatusLabel.Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold);
        _clientsStatusLabel.AutoSize = true;

        _serverToggleButton.Text = "Parar servidor";
        _serverToggleButton.Width = 140;
        _serverToggleButton.Height = 38;
        _serverToggleButton.FlatStyle = FlatStyle.Flat;
        _serverToggleButton.BackColor = Color.FromArgb(217, 76, 76);
        _serverToggleButton.ForeColor = Color.White;

        _backupButton.Text = "Backup manual";
        _backupButton.Width = 140;
        _backupButton.Height = 38;
        _backupButton.FlatStyle = FlatStyle.Flat;
        _backupButton.BackColor = Color.FromArgb(51, 140, 93);
        _backupButton.ForeColor = Color.White;

        headerPanel.Controls.Add(titlePanel, 0, 0);
        headerPanel.Controls.Add(_serverStatusLabel, 1, 0);
        headerPanel.Controls.Add(_clientsStatusLabel, 2, 0);
        headerPanel.Controls.Add(_serverToggleButton, 3, 0);
        headerPanel.Controls.Add(_backupButton, 4, 0);

        _webView.Dock = DockStyle.Fill;
        _webView.DefaultBackgroundColor = Color.FromArgb(14, 18, 26);

        Controls.Add(_webView);
        Controls.Add(headerPanel);

        _refreshTimer.Interval = 15_000;

        var trayMenu = new ContextMenuStrip();
        trayMenu.Items.AddRange([_openMenuItem, _stopMenuItem, _exitMenuItem]);

        _trayIcon.Icon = SystemIcons.Application;
        _trayIcon.Text = "Adrenalina ADMIN";
        _trayIcon.ContextMenuStrip = trayMenu;
        _trayIcon.Visible = true;
    }

    private void WireEvents()
    {
        _serverToggleButton.Click += async (_, _) => await ToggleServerAsync();
        _backupButton.Click += async (_, _) => await RunManualBackupAsync();
        _refreshTimer.Tick += async (_, _) => await RefreshDashboardAsync();

        _trayIcon.DoubleClick += (_, _) => RestoreFromTray();
        _openMenuItem.Click += (_, _) => RestoreFromTray();
        _stopMenuItem.Click += async (_, _) => await ToggleServerAsync();
        _exitMenuItem.Click += (_, _) =>
        {
            _allowExit = true;
            Close();
        };
    }

    private async Task StartServerAndNavigateAsync()
    {
        try
        {
            await _server.StartAsync();
            await EnsureWebViewReadyAsync();
            _webView.Source = new Uri(_server.BaseAddress, "auth/login");
            _refreshTimer.Start();
            await RefreshDashboardAsync();
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"Não foi possível iniciar o servidor ADMIN.\n\n{exception.Message}",
                "Adrenalina ADMIN",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private async Task EnsureWebViewReadyAsync()
    {
        if (_webViewReady)
        {
            return;
        }

        try
        {
            await _webView.EnsureCoreWebView2Async();
            _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            _webView.NavigationCompleted += HandleNavigationCompleted;
            _webViewReady = true;
        }
        catch (WebView2RuntimeNotFoundException exception)
        {
            throw new InvalidOperationException(
                "O WebView2 Runtime não está instalado nesta máquina. Instale o runtime da Microsoft para usar o painel nativo.",
                exception);
        }
    }

    private async Task ToggleServerAsync()
    {
        _serverToggleButton.Enabled = false;
        try
        {
            if (_server.IsRunning)
            {
                await _server.StopAsync();
                _webView.Source = new Uri("about:blank");
            }
            else
            {
                await _server.StartAsync();
                await EnsureWebViewReadyAsync();
                _webView.Source = new Uri(_server.BaseAddress, "auth/login");
            }

            await RefreshDashboardAsync();
        }
        finally
        {
            _serverToggleButton.Enabled = true;
        }
    }

    private async Task RunManualBackupAsync()
    {
        _backupButton.Enabled = false;
        try
        {
            var result = await _server.CreateManualBackupAsync();
            MessageBox.Show(
                result.Message,
                "Backup manual",
                MessageBoxButtons.OK,
                result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }
        finally
        {
            _backupButton.Enabled = true;
            await RefreshDashboardAsync();
        }
    }

    private async Task RefreshDashboardAsync()
    {
        if (!_server.IsRunning)
        {
            _serverStatusLabel.Text = "Servidor: parado";
            _clientsStatusLabel.Text = "Clientes online: --/--";
            _serverToggleButton.Text = "Iniciar servidor";
            _serverToggleButton.BackColor = Color.FromArgb(51, 140, 93);
            _stopMenuItem.Text = "Iniciar servidor";
            return;
        }

        var dashboard = await _server.TryGetDashboardAsync();
        _serverStatusLabel.Text = "Servidor: ativo";
        _clientsStatusLabel.Text = dashboard is null
            ? "Clientes online: --/--"
            : $"Clientes online: {dashboard.OnlineMachines}/{dashboard.Machines.Count}";
        _serverToggleButton.Text = "Parar servidor";
        _serverToggleButton.BackColor = Color.FromArgb(217, 76, 76);
        _stopMenuItem.Text = "Parar servidor";
    }

    private void HandleNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (!e.IsSuccess)
        {
            _serverStatusLabel.Text = $"Servidor: falha ({e.WebErrorStatus})";
        }
    }

    private void HideToTray()
    {
        Hide();
        _trayIcon.ShowBalloonTip(1500, "Adrenalina ADMIN", "O painel continua ativo na bandeja do sistema.", ToolTipIcon.Info);
    }

    private void RestoreFromTray()
    {
        Show();
        WindowState = FormWindowState.Normal;
        BringToFront();
        Activate();
    }
}
