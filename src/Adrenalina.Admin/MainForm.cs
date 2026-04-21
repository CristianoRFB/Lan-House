using System.Diagnostics;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace Adrenalina.Admin;

public sealed class MainForm : Form
{
    private readonly EmbeddedAdminServer _server = new();
    private readonly AdminDesktopOptions _desktopOptions = AdminDesktopOptionsStore.LoadOrCreate();
    private readonly WebView2 _webView = new();
    private readonly Button _primaryActionButton = new();
    private readonly Button _serverToggleButton = new();
    private readonly Button _openBrowserButton = new();
    private readonly Button _backupButton = new();
    private readonly Button _settingsButton = new();
    private readonly Button _tutorialButton = new();
    private readonly Button _copyCredentialsButton = new();
    private readonly Button _sidebarPrimaryActionButton = new();
    private readonly Button _sidebarSettingsButton = new();
    private readonly Button _sidebarTutorialButton = new();
    private readonly Label _serverStatusLabel = new();
    private readonly Label _clientsStatusLabel = new();
    private readonly Label _panelModeLabel = new();
    private readonly Label _helperStatusLabel = new();
    private readonly Label _summaryLabel = new();
    private readonly Label _urlStatusLabel = new();
    private readonly Label _clientConnectionLabel = new();
    private readonly Label _fallbackTitleLabel = new();
    private readonly Label _fallbackDescriptionLabel = new();
    private readonly Panel _webHostPanel = new();
    private readonly Panel _browserFallbackPanel = new();
    private readonly System.Windows.Forms.Timer _refreshTimer = new();
    private readonly NotifyIcon _trayIcon = new();
    private readonly ToolStripMenuItem _openMenuItem = new("Abrir painel");
    private readonly ToolStripMenuItem _stopMenuItem = new("Parar servidor");
    private readonly ToolStripMenuItem _exitMenuItem = new("Encerrar");

    private bool _allowExit;
    private bool _closeRequestInProgress;
    private bool _webViewReady;
    private bool _panelWasOpened;
    private bool _fallbackModeActive;

    public MainForm()
    {
        Text = "Adrenalina ADMIN";
        Width = 1500;
        Height = 920;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1180, 760);
        BackColor = Color.FromArgb(12, 18, 30);

        BuildLayout();
        WireEvents();
        UpdateConnectionHints();
        UpdatePrimaryActionState();
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        if (_desktopOptions.ShowTutorialOnNextLaunch)
        {
            _desktopOptions.ShowTutorialOnNextLaunch = false;
            AdminDesktopOptionsStore.Save(_desktopOptions);
            BeginInvoke(new Action(ShowTutorial));
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_allowExit)
        {
            _refreshTimer.Stop();
            _trayIcon.Visible = false;
            base.OnFormClosing(e);
            return;
        }

        if (e.CloseReason is CloseReason.WindowsShutDown or CloseReason.TaskManagerClosing)
        {
            _allowExit = true;
            _refreshTimer.Stop();
            _trayIcon.Visible = false;
            base.OnFormClosing(e);
            return;
        }

        e.Cancel = true;
        if (!_closeRequestInProgress)
        {
            _ = RequestCloseAsync();
        }
    }

    protected override async void OnFormClosed(FormClosedEventArgs e)
    {
        _webView.Dispose();
        await _server.DisposeAsync();
        _trayIcon.Dispose();
        _refreshTimer.Dispose();
        base.OnFormClosed(e);
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = BackColor
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        root.Controls.Add(BuildHeaderPanel(), 0, 0);
        root.Controls.Add(BuildContentPanel(), 0, 1);

        Controls.Add(root);

        _refreshTimer.Interval = 15_000;

        var trayMenu = new ContextMenuStrip();
        trayMenu.Items.AddRange([_openMenuItem, _stopMenuItem, _exitMenuItem]);

        _trayIcon.Icon = SystemIcons.Application;
        _trayIcon.Text = "Adrenalina ADMIN";
        _trayIcon.ContextMenuStrip = trayMenu;
        _trayIcon.Visible = false;
    }

    private Control BuildHeaderPanel()
    {
        var headerPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Height = 104,
            Padding = new Padding(20, 16, 20, 16),
            ColumnCount = 2,
            BackColor = Color.FromArgb(17, 25, 40)
        };
        headerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
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
            Font = new Font("Segoe UI Semibold", 18f, FontStyle.Bold),
            AutoSize = true
        });

        _urlStatusLabel.Text = $"Painel local: {_server.BaseAddress}";
        _urlStatusLabel.ForeColor = Color.FromArgb(173, 188, 209);
        _urlStatusLabel.Font = new Font("Segoe UI", 10f);
        _urlStatusLabel.AutoSize = true;
        titlePanel.Controls.Add(_urlStatusLabel);

        _clientConnectionLabel.ForeColor = Color.FromArgb(127, 217, 199);
        _clientConnectionLabel.Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold);
        _clientConnectionLabel.AutoSize = true;
        titlePanel.Controls.Add(_clientConnectionLabel);

        _helperStatusLabel.Text = "Abra o app e use o botao principal para iniciar o servidor local e abrir o painel quando quiser.";
        _helperStatusLabel.ForeColor = Color.FromArgb(138, 157, 183);
        _helperStatusLabel.Font = new Font("Segoe UI", 9.5f);
        _helperStatusLabel.AutoSize = true;
        titlePanel.Controls.Add(_helperStatusLabel);

        var actionPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };

        ConfigureButton(_primaryActionButton, "Iniciar servidor e abrir painel", Color.FromArgb(57, 96, 168));
        ConfigureButton(_settingsButton, "Configuracoes", Color.FromArgb(70, 84, 106));
        ConfigureButton(_tutorialButton, "Tutorial", Color.FromArgb(84, 101, 61));

        actionPanel.Controls.Add(_primaryActionButton);
        actionPanel.Controls.Add(_settingsButton);
        actionPanel.Controls.Add(_tutorialButton);

        headerPanel.Controls.Add(titlePanel, 0, 0);
        headerPanel.Controls.Add(actionPanel, 1, 0);

        return headerPanel;
    }

    private Control BuildContentPanel()
    {
        var contentPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(20, 18, 20, 20),
            BackColor = BackColor
        };
        contentPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 360));
        contentPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        contentPanel.Controls.Add(BuildSidebar(), 0, 0);
        contentPanel.Controls.Add(BuildViewerPanel(), 1, 0);

        return contentPanel;
    }

    private Control BuildSidebar()
    {
        var sidebar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(0, 0, 12, 0)
        };

        _summaryLabel.Text = "Clique em Iniciar servidor e abrir painel para validar o ambiente local e entrar no sistema com mais seguranca.";
        _summaryLabel.ForeColor = Color.FromArgb(204, 214, 228);
        _summaryLabel.Font = new Font("Segoe UI", 10f);
        _summaryLabel.MaximumSize = new Size(300, 0);
        _summaryLabel.AutoSize = true;

        var firstCard = CreateCard("Fluxo principal");
        firstCard.Controls.Add(_summaryLabel);
        firstCard.Controls.Add(CreateSpacer());
        firstCard.Controls.Add(BuildInfoChip("Sem plugin extra para usar", Color.FromArgb(41, 63, 109)));

        _serverStatusLabel.Text = "Servidor: iniciando";
        _serverStatusLabel.ForeColor = Color.White;
        _serverStatusLabel.AutoSize = true;
        _serverStatusLabel.Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold);

        _clientsStatusLabel.Text = "Clientes online: --/--";
        _clientsStatusLabel.ForeColor = Color.White;
        _clientsStatusLabel.AutoSize = true;
        _clientsStatusLabel.Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold);

        _panelModeLabel.Text = "Painel: aguardando";
        _panelModeLabel.ForeColor = Color.FromArgb(193, 204, 220);
        _panelModeLabel.AutoSize = true;
        _panelModeLabel.Font = new Font("Segoe UI", 10f);

        var statusCard = CreateCard("Status rapido");
        statusCard.Controls.Add(_serverStatusLabel);
        statusCard.Controls.Add(CreateSpacer(8));
        statusCard.Controls.Add(_clientsStatusLabel);
        statusCard.Controls.Add(CreateSpacer(8));
        statusCard.Controls.Add(_panelModeLabel);

        ConfigureButton(_copyCredentialsButton, "Copiar acesso inicial", Color.FromArgb(65, 83, 112));
        ConfigureButton(_openBrowserButton, "Abrir painel no navegador", Color.FromArgb(46, 119, 124));
        ConfigureButton(_sidebarPrimaryActionButton, "Iniciar servidor e abrir painel", Color.FromArgb(57, 96, 168));
        ConfigureButton(_sidebarSettingsButton, "Configuracoes", Color.FromArgb(70, 84, 106));
        ConfigureButton(_sidebarTutorialButton, "Tutorial", Color.FromArgb(84, 101, 61));

        var credentialsCard = CreateCard("Primeiro acesso");
        credentialsCard.Controls.Add(CreateTextLabel("Login inicial: admin"));
        credentialsCard.Controls.Add(CreateSpacer(6));
        credentialsCard.Controls.Add(CreateTextLabel("Senha inicial: adrenalina123"));
        credentialsCard.Controls.Add(CreateSpacer(6));
        credentialsCard.Controls.Add(CreateTextLabel("PIN inicial: 1234"));
        credentialsCard.Controls.Add(CreateSpacer());
        credentialsCard.Controls.Add(_copyCredentialsButton);
        credentialsCard.Controls.Add(CreateSpacer(10));
        credentialsCard.Controls.Add(_openBrowserButton);

        ConfigureButton(_serverToggleButton, "Parar servidor", Color.FromArgb(181, 76, 76));
        ConfigureButton(_backupButton, "Backup manual", Color.FromArgb(52, 132, 84));

        var toolsCard = CreateCard("Acoes");
        toolsCard.Controls.Add(_sidebarPrimaryActionButton);
        toolsCard.Controls.Add(CreateSpacer(10));
        toolsCard.Controls.Add(_serverToggleButton);
        toolsCard.Controls.Add(CreateSpacer(10));
        toolsCard.Controls.Add(_backupButton);
        toolsCard.Controls.Add(CreateSpacer(10));
        toolsCard.Controls.Add(_sidebarSettingsButton);
        toolsCard.Controls.Add(CreateSpacer(10));
        toolsCard.Controls.Add(_sidebarTutorialButton);

        sidebar.Controls.Add(firstCard);
        sidebar.Controls.Add(statusCard);
        sidebar.Controls.Add(credentialsCard);
        sidebar.Controls.Add(toolsCard);

        return sidebar;
    }

    private Control BuildViewerPanel()
    {
        var viewerContainer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(17, 24, 38),
            Padding = new Padding(12)
        };

        _webHostPanel.Dock = DockStyle.Fill;
        _webHostPanel.BackColor = Color.FromArgb(13, 19, 30);
        _webView.Dock = DockStyle.Fill;
        _webView.DefaultBackgroundColor = Color.FromArgb(13, 19, 30);
        _webHostPanel.Controls.Add(_webView);

        _browserFallbackPanel.Dock = DockStyle.Fill;
        _browserFallbackPanel.BackColor = Color.FromArgb(13, 19, 30);

        var fallbackLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(40)
        };
        fallbackLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 30));
        fallbackLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        fallbackLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        fallbackLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 70));

        _fallbackTitleLabel.Text = "Painel pronto para abrir";
        _fallbackTitleLabel.ForeColor = Color.White;
        _fallbackTitleLabel.Font = new Font("Segoe UI Semibold", 24f, FontStyle.Bold);
        _fallbackTitleLabel.AutoSize = true;
        _fallbackTitleLabel.Anchor = AnchorStyles.None;

        _fallbackDescriptionLabel.Text = "Quando o WebView2 nao estiver disponivel, o ADMIN usa o navegador padrao da maquina sem exigir instalacoes extras.";
        _fallbackDescriptionLabel.ForeColor = Color.FromArgb(193, 204, 220);
        _fallbackDescriptionLabel.Font = new Font("Segoe UI", 11f);
        _fallbackDescriptionLabel.MaximumSize = new Size(720, 0);
        _fallbackDescriptionLabel.AutoSize = true;
        _fallbackDescriptionLabel.Anchor = AnchorStyles.None;

        var fallbackOpenButton = new Button
        {
            Text = "Abrir painel agora",
            AutoSize = true,
            Padding = new Padding(18, 10, 18, 10),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(57, 96, 168),
            ForeColor = Color.White,
            Anchor = AnchorStyles.None
        };
        fallbackOpenButton.FlatAppearance.BorderSize = 0;
        fallbackOpenButton.Click += (_, _) => OpenPanelInBrowser();

        fallbackLayout.Controls.Add(new Panel(), 0, 0);
        fallbackLayout.Controls.Add(_fallbackTitleLabel, 0, 1);
        fallbackLayout.Controls.Add(_fallbackDescriptionLabel, 0, 2);
        fallbackLayout.Controls.Add(fallbackOpenButton, 0, 3);
        _browserFallbackPanel.Controls.Add(fallbackLayout);

        viewerContainer.Controls.Add(_browserFallbackPanel);
        viewerContainer.Controls.Add(_webHostPanel);

        ShowBrowserFallback(
            "Abra o servidor pelo botao principal. Depois disso, o painel pode ser aberto dentro do app ou no navegador padrao.");

        return viewerContainer;
    }

    private void WireEvents()
    {
        _primaryActionButton.Click += async (_, _) => await PrepareEnvironmentAsync(openPanel: true);
        _serverToggleButton.Click += async (_, _) => await ToggleServerAsync();
        _openBrowserButton.Click += (_, _) => OpenPanelInBrowser();
        _backupButton.Click += async (_, _) => await RunManualBackupAsync();
        _settingsButton.Click += async (_, _) => await OpenSettingsAsync();
        _tutorialButton.Click += (_, _) => ShowTutorial();
        _copyCredentialsButton.Click += (_, _) => CopyInitialAccessToClipboard();
        _sidebarPrimaryActionButton.Click += async (_, _) => await PrepareEnvironmentAsync(openPanel: true);
        _sidebarSettingsButton.Click += async (_, _) => await OpenSettingsAsync();
        _sidebarTutorialButton.Click += (_, _) => ShowTutorial();
        _refreshTimer.Tick += async (_, _) => await RefreshDashboardAsync();

        _openMenuItem.Click += async (_, _) =>
        {
            await PrepareEnvironmentAsync(openPanel: true);
        };
        _stopMenuItem.Click += async (_, _) => await ToggleServerAsync();
        _exitMenuItem.Click += (_, _) =>
        {
            Close();
        };
    }

    private async Task PrepareEnvironmentAsync(bool openPanel)
    {
        _primaryActionButton.Enabled = false;
        _sidebarPrimaryActionButton.Enabled = false;
        try
        {
            if (!_server.IsRunning)
            {
                await _server.StartAsync();
            }

            if (_server.UsedFallbackPort)
            {
                _helperStatusLabel.Text = _server.StartupMessage;
            }

            if (!_refreshTimer.Enabled)
            {
                _refreshTimer.Start();
            }

            await RefreshDashboardAsync();

            if (openPanel)
            {
                await OpenPanelAsync();
            }
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"Nao foi possivel preparar o ambiente admin.\n\n{exception.Message}",
                "Adrenalina ADMIN",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            _primaryActionButton.Enabled = true;
            _sidebarPrimaryActionButton.Enabled = true;
            UpdatePrimaryActionState();
        }
    }

    private async Task OpenPanelAsync()
    {
        if (!_server.IsRunning)
        {
            await _server.StartAsync();
        }

        _panelWasOpened = true;

        if (_desktopOptions.PreferExternalBrowser)
        {
            ShowBrowserFallback("O painel esta configurado para abrir no navegador padrao deste computador.");
            OpenPanelInBrowser();
            UpdatePrimaryActionState();
            return;
        }

        try
        {
            await EnsureWebViewReadyAsync();
            _fallbackModeActive = false;
            _browserFallbackPanel.Visible = false;
            _webHostPanel.Visible = true;
            _webView.Source = new Uri(_server.BaseAddress, "auth/login");
            _panelModeLabel.Text = "Painel: embutido no app";
            _helperStatusLabel.Text = "Servidor pronto. O login admin foi aberto dentro do proprio aplicativo.";
        }
        catch (Exception exception)
        {
            ShowBrowserFallback(
                "O painel continuou acessivel, mas sera aberto no navegador padrao porque o modo embutido nao ficou disponivel neste computador.");
            _helperStatusLabel.Text = $"Modo navegador ativado: {exception.Message}";
            OpenPanelInBrowser();
        }

        UpdatePrimaryActionState();
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
                "O WebView2 nao esta instalado. O ADMIN pode continuar usando o navegador padrao sem depender desse componente.",
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
                _refreshTimer.Stop();
                ShowBrowserFallback("Servidor parado. Clique em Iniciar servidor e abrir painel para subir tudo novamente.");
            }
            else
            {
                await PrepareEnvironmentAsync(openPanel: false);
            }

            await RefreshDashboardAsync();
        }
        finally
        {
            _serverToggleButton.Enabled = true;
            UpdatePrimaryActionState();
        }
    }

    private async Task RunManualBackupAsync()
    {
        _backupButton.Enabled = false;
        try
        {
            if (!_server.IsRunning)
            {
                await _server.StartAsync();
            }

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
        UpdateConnectionHints();

        if (!_server.IsRunning)
        {
            _serverStatusLabel.Text = "Servidor: parado";
            _clientsStatusLabel.Text = "Clientes online: --/--";
            _serverToggleButton.Text = "Iniciar servidor";
            _serverToggleButton.BackColor = Color.FromArgb(52, 132, 84);
            _stopMenuItem.Text = "Iniciar servidor";
            _panelModeLabel.Text = "Painel: aguardando";
            _summaryLabel.Text = $"O servidor local esta parado. Quando estiver ativo, os clientes devem usar {GetClientConnectionUrl()} para conectar.";
            UpdatePrimaryActionState();
            return;
        }

        try
        {
            var dashboard = await _server.TryGetDashboardAsync();
            _serverStatusLabel.Text = "Servidor: ativo";
            _clientsStatusLabel.Text = dashboard is null
                ? "Clientes online: --/--"
                : $"Clientes online: {dashboard.OnlineMachines}/{dashboard.Machines.Count}";
            _serverToggleButton.Text = "Parar servidor";
            _serverToggleButton.BackColor = Color.FromArgb(181, 76, 76);
            _stopMenuItem.Text = "Parar servidor";
            _summaryLabel.Text = dashboard is null
                ? $"Servidor ativo. Abra o painel para entrar no ambiente admin. Clientes usam {GetClientConnectionUrl()}."
                : $"{dashboard.CafeName} pronto. Clientes usam {GetClientConnectionUrl()} e existem {dashboard.PendingRequests} solicitacoes pendentes.";

            if (_server.UsedFallbackPort)
            {
                _helperStatusLabel.Text = _server.StartupMessage;
            }

            if (!_fallbackModeActive && _webHostPanel.Visible)
            {
                _panelModeLabel.Text = "Painel: embutido no app";
            }
        }
        catch (Exception exception)
        {
            _serverStatusLabel.Text = "Servidor: ativo";
            _clientsStatusLabel.Text = "Clientes online: resumo indisponivel";
            _serverToggleButton.Text = "Parar servidor";
            _serverToggleButton.BackColor = Color.FromArgb(181, 76, 76);
            _stopMenuItem.Text = "Parar servidor";
            _summaryLabel.Text = $"Servidor ativo. O painel continua acessivel em {_server.BaseAddress} e os clientes devem usar {GetClientConnectionUrl()}.";
            _helperStatusLabel.Text = $"Servidor iniciado, mas o resumo rapido falhou: {exception.Message}";

            if (!_panelWasOpened)
            {
                _panelModeLabel.Text = "Painel: pronto para abrir";
            }
        }

        UpdatePrimaryActionState();
    }

    private async Task OpenSettingsAsync()
    {
        using var dialog = new AdminSettingsDialog(_desktopOptions);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _desktopOptions.PreferExternalBrowser = dialog.ResultOptions.PreferExternalBrowser;
        _desktopOptions.ShowTutorialOnNextLaunch = dialog.ResultOptions.ShowTutorialOnNextLaunch;
        AdminDesktopOptionsStore.Save(_desktopOptions);

        if (_server.IsRunning)
        {
            if (_desktopOptions.PreferExternalBrowser)
            {
                ShowBrowserFallback("Configuracao salva. O painel sera aberto no navegador padrao.");
            }
            else if (_panelWasOpened)
            {
                await OpenPanelAsync();
            }
        }

        if (dialog.OpenTutorialNow)
        {
            ShowTutorial();
        }

        UpdatePrimaryActionState();
    }

    private void ShowTutorial()
    {
        using var dialog = new AdminTutorialForm();
        dialog.ShowDialog(this);
    }

    private void CopyInitialAccessToClipboard()
    {
        var clientUrl = GetClientConnectionUrl();
        var loginUrl = new Uri(_server.BaseAddress, "auth/login").ToString();
        var payload =
            $"Painel local do admin: {loginUrl}{Environment.NewLine}" +
            $"URL para clientes: {clientUrl}{Environment.NewLine}" +
            "Login inicial: admin" + Environment.NewLine +
            "Senha inicial: adrenalina123" + Environment.NewLine +
            "PIN inicial: 1234";

        try
        {
            Clipboard.SetText(payload);
            MessageBox.Show(
                "As informacoes de primeiro acesso foram copiadas para a area de transferencia.",
                "Acesso inicial",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"Nao foi possivel copiar o acesso inicial.\n\n{exception.Message}",
                "Acesso inicial",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private void OpenPanelInBrowser()
    {
        if (!_server.IsRunning)
        {
            MessageBox.Show(
                "Inicie o ambiente admin antes de abrir o painel no navegador.",
                "Adrenalina ADMIN",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(new Uri(_server.BaseAddress, "auth/login").ToString())
            {
                UseShellExecute = true
            });

            _panelWasOpened = true;
            _fallbackModeActive = true;
            _browserFallbackPanel.Visible = true;
            _webHostPanel.Visible = false;
            _panelModeLabel.Text = "Painel: navegador padrao";
            _helperStatusLabel.Text = "Painel aberto no navegador padrao para evitar qualquer dependencia extra nesta maquina.";
            UpdatePrimaryActionState();
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"Nao foi possivel abrir o painel no navegador.\n\n{exception.Message}",
                "Adrenalina ADMIN",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private void ShowBrowserFallback(string description)
    {
        _fallbackModeActive = true;
        _browserFallbackPanel.Visible = true;
        _webHostPanel.Visible = false;
        _fallbackTitleLabel.Text = "Painel pronto para abrir";
        _fallbackDescriptionLabel.Text = description;
        _panelModeLabel.Text = "Painel: navegador padrao";
    }

    private void HandleNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (!e.IsSuccess)
        {
            _serverStatusLabel.Text = $"Servidor: falha ({e.WebErrorStatus})";
            ShowBrowserFallback("O painel embutido encontrou uma falha. O navegador padrao continua como alternativa imediata.");
        }
    }

    private void UpdatePrimaryActionState()
    {
        if (!_server.IsRunning)
        {
            _primaryActionButton.Text = "Iniciar servidor e abrir painel";
            _sidebarPrimaryActionButton.Text = "Iniciar servidor e abrir painel";
            return;
        }

        var actionText = !_panelWasOpened
            ? "Abrir painel"
            : _fallbackModeActive || _desktopOptions.PreferExternalBrowser
                ? "Abrir painel novamente"
                : "Atualizar painel";
        _primaryActionButton.Text = actionText;
        _sidebarPrimaryActionButton.Text = actionText;
    }

    private void UpdateConnectionHints()
    {
        _urlStatusLabel.Text = $"Painel local: {_server.BaseAddress}";
        _clientConnectionLabel.Text = $"Clientes na rede: {GetClientConnectionUrl()}";
    }

    private string GetClientConnectionUrl()
    {
        return AdminNetworkLocator.GetPreferredBaseUrl(_server.Port);
    }

    private async Task RequestCloseAsync()
    {
        _closeRequestInProgress = true;
        try
        {
            using var dialog = new AdminExitDialog(_server.IsRunning);
            var result = dialog.ShowDialog(this);
            if (result != DialogResult.OK)
            {
                return;
            }

            if (!dialog.ExitApplication)
            {
                return;
            }

            await ShutdownForExitAsync(dialog.ShutdownForToday);
            _allowExit = true;
            BeginInvoke(new Action(Close));
        }
        finally
        {
            _closeRequestInProgress = false;
        }
    }

    private async Task ShutdownForExitAsync(bool shutdownForToday)
    {
        _refreshTimer.Stop();

        if (shutdownForToday && _server.IsRunning)
        {
            var backupResult = await _server.CreateManualBackupAsync();
            if (!backupResult.Success)
            {
                MessageBox.Show(
                    $"Nao foi possivel gerar o backup final antes de encerrar.\n\n{backupResult.Message}\n\nO sistema sera fechado mesmo assim.",
                    "Encerrar sistema",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        try
        {
            if (_webView.CoreWebView2 is not null)
            {
                _webView.CoreWebView2.Stop();
            }
        }
        catch
        {
            // O fechamento continua mesmo se o navegador interno ja estiver em descarte.
        }

        try
        {
            _webView.Source = new Uri("about:blank");
        }
        catch
        {
            // Ignora falhas ao limpar o painel durante o encerramento.
        }

        if (_server.IsRunning)
        {
            await _server.StopAsync();
        }

        _trayIcon.Visible = false;
    }

    private static FlowLayoutPanel CreateCard(string title)
    {
        var card = new FlowLayoutPanel
        {
            Width = 320,
            AutoSize = true,
            Padding = new Padding(18),
            Margin = new Padding(0, 0, 0, 14),
            BackColor = Color.FromArgb(19, 27, 43),
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false
        };

        card.Controls.Add(new Label
        {
            Text = title,
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 12f, FontStyle.Bold),
            AutoSize = true
        });
        card.Controls.Add(CreateSpacer(12));

        return card;
    }

    private static Label CreateTextLabel(string text)
    {
        return new Label
        {
            Text = text,
            ForeColor = Color.FromArgb(204, 214, 228),
            Font = new Font("Segoe UI", 10f),
            AutoSize = true,
            MaximumSize = new Size(280, 0)
        };
    }

    private static Control CreateSpacer(int height = 12)
    {
        return new Panel
        {
            Height = height,
            Width = 1
        };
    }

    private static Label BuildInfoChip(string text, Color backColor)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            BackColor = backColor,
            Padding = new Padding(10, 6, 10, 6)
        };
    }

    private static void ConfigureButton(Button button, string text, Color backColor)
    {
        button.Text = text;
        button.Width = 268;
        button.Height = 40;
        button.FlatStyle = FlatStyle.Flat;
        button.BackColor = backColor;
        button.ForeColor = Color.White;
        button.Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
        button.FlatAppearance.BorderSize = 0;
    }
}
