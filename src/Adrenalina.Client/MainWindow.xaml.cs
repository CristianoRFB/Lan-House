using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Adrenalina.Application;
using Adrenalina.Domain;

namespace Adrenalina.Client;

public partial class MainWindow : Window
{
    private readonly ClientConnectionOptions _options;
    private readonly IClientRuntimeStore _runtimeStore;
    private readonly ClientServerGateway _gateway;
    private readonly WindowsKioskManager _kioskManager;
    private readonly DispatcherTimer _refreshTimer;

    private ClientRuntimeState _lastKnownState = new();
    private bool _localHideTimer;
    private bool _watchdogStarted;

    public MainWindow(
        ClientConnectionOptions options,
        IClientRuntimeStore runtimeStore,
        ClientServerGateway gateway,
        WindowsKioskManager kioskManager)
    {
        _options = options;
        _runtimeStore = runtimeStore;
        _gateway = gateway;
        _kioskManager = kioskManager;

        InitializeComponent();

        SetupMachineKindComboBox.ItemsSource = Enum.GetValues<MachineKind>();
        SettingsMachineKindComboBox.ItemsSource = Enum.GetValues<MachineKind>();

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _refreshTimer.Tick += async (_, _) => await RefreshAsync();

        Loaded += HandleLoaded;
        Closing += HandleClosing;
        PinBox.KeyDown += HandlePinBoxKeyDown;
    }

    private async void HandleLoaded(object sender, RoutedEventArgs e)
    {
        PopulateSetupFields();
        PopulateSettingsFields();

        EnsureWatchdogStarted();

        _refreshTimer.Start();
        await RefreshAsync();

        if (!_options.SetupCompleted)
        {
            ShowSetupOverlay();
            return;
        }

        if (_options.ShowTutorialOnNextLaunch)
        {
            ShowTutorialOverlay();
        }

        LoginTextBox.Focus();
    }

    private void HandleClosing(object? sender, CancelEventArgs e)
    {
        if (!_options.SetupCompleted)
        {
            _kioskManager.ApplyState(new ClientRuntimeState { IsLocked = false }, string.Empty);
            return;
        }

        if (_lastKnownState.IsLocked)
        {
            e.Cancel = true;
            MessageBox.Show(
                "Desbloqueie ou encerre a sessao atual antes de fechar o app do cliente.",
                "Fechar cliente",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        _kioskManager.ApplyState(new ClientRuntimeState { IsLocked = false }, string.Empty);
    }

    private async Task RefreshAsync()
    {
        var state = await _runtimeStore.LoadStateAsync();
        _lastKnownState = state;
        var setupPending = !_options.SetupCompleted;

        if (setupPending)
        {
            _kioskManager.ApplyState(new ClientRuntimeState { IsLocked = false }, string.Empty);
        }
        else
        {
            _kioskManager.ApplyState(state, _gateway.CurrentBlockedProgramsCsv);
        }

        ApplyTheme(state.Theme);
        ApplyWindowMode(setupPending ? false : state.IsLocked, setupPending);

        MachineTitleText.Text = setupPending
            ? string.IsNullOrWhiteSpace(_options.MachineName) ? Environment.MachineName : _options.MachineName
            : string.IsNullOrWhiteSpace(state.MachineName) ? _options.MachineName : state.MachineName;
        SessionStatusText.Text = setupPending
            ? "Informe o IP do ADMIN para conectar esta maquina ao sistema."
            : state.SessionMessage;
        ConnectivityText.Text = setupPending
            ? "Use o endereco mostrado no app do administrador. Exemplo: http://192.168.0.10:5076/"
            : _gateway.ConnectionStatusText;
        ConnectivityText.Foreground = _gateway.IsServerOnline
            ? new SolidColorBrush(Color.FromRgb(127, 217, 199))
            : new SolidColorBrush(setupPending ? Color.FromRgb(143, 178, 216) : Color.FromRgb(248, 182, 91));

        var currentUserName = string.IsNullOrWhiteSpace(state.CurrentUserName)
            ? setupPending ? "aguardando configuracao" : "aguardando login"
            : state.CurrentUserName;
        CurrentUserText.Text = $"Usuario: {currentUserName}";
        ProfileText.Text = $"Perfil: {ResolveProfileLabel(state, setupPending)}";
        RemainingTimeText.Text = state.ShowRemainingTime && !_localHideTimer ? FormatRemainingTime(state) : "Oculto";
        BalanceText.Text = $"R$ {state.CurrentBalance:N2}";
        AnnotationText.Text = $"R$ {state.PendingAnnotations:N2}";
        NotesText.Text = string.IsNullOrWhiteSpace(state.CurrentUserNotes) ? "Sem observacoes." : state.CurrentUserNotes;
        NotificationsList.ItemsSource = state.Notifications
            .Select(item => $"{item.Title}: {item.Message}")
            .ToList();

        LoginCard.Visibility = !setupPending && state.IsLocked ? Visibility.Visible : Visibility.Collapsed;
        Grid.SetColumnSpan(SessionCard, !setupPending && state.IsLocked ? 1 : 2);
        SessionCard.Margin = !setupPending && state.IsLocked ? new Thickness(0, 0, 14, 0) : new Thickness(0);
        LoginButton.Content = !setupPending && state.IsLocked ? "Entrar" : "Atualizar sessao";
        SettingsButton.Content = setupPending ? "Preparar cliente" : "Configuracoes";

        if (setupPending)
        {
            SetupOverlay.Visibility = Visibility.Visible;
        }
    }

    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_options.SetupCompleted)
        {
            ShowSetupOverlay();
            return;
        }

        if (string.IsNullOrWhiteSpace(LoginTextBox.Text) || string.IsNullOrWhiteSpace(PinBox.Password))
        {
            MessageBox.Show(
                "Preencha usuario e PIN para entrar.",
                "Entrar",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var response = await _gateway.LoginAsync(LoginTextBox.Text.Trim(), PinBox.Password.Trim());
        MessageBox.Show(
            response.Message,
            response.Success ? "Sessao iniciada" : "Acesso negado",
            MessageBoxButton.OK,
            response.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);

        if (response.Success)
        {
            LoginTextBox.Text = string.Empty;
            PinBox.Password = string.Empty;
            MessageTextBox.Text = string.Empty;
            DisplayNameTextBox.Text = string.Empty;
        }

        await RefreshAsync();
    }

    private async void RequestRegistration_Click(object sender, RoutedEventArgs e)
    {
        if (!_options.SetupCompleted)
        {
            ShowSetupOverlay();
            return;
        }

        if (string.IsNullOrWhiteSpace(DisplayNameTextBox.Text) && string.IsNullOrWhiteSpace(LoginTextBox.Text))
        {
            MessageBox.Show(
                "Informe pelo menos o nome para cadastro ou o usuario que sera usado no pedido.",
                "Solicitar cadastro",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        await QueueRequestAsync(ClientRequestType.Registration);
    }

    private async void RequestMoreTime_Click(object sender, RoutedEventArgs e)
    {
        if (!_options.SetupCompleted)
        {
            ShowSetupOverlay();
            return;
        }

        await QueueRequestAsync(ClientRequestType.MoreTime);
    }

    private void ToggleTimer_Click(object sender, RoutedEventArgs e)
    {
        _localHideTimer = !_localHideTimer;
    }

    private async Task QueueRequestAsync(ClientRequestType type)
    {
        await _gateway.QueueRequestAsync(
            new ClientShellRequest
            {
                Type = type,
                Login = LoginTextBox.Text.Trim(),
                Pin = PinBox.Password.Trim(),
                DisplayName = DisplayNameTextBox.Text.Trim(),
                Message = MessageTextBox.Text.Trim(),
                OccurredAtUtc = DateTime.UtcNow
            });

        MessageBox.Show("Solicitacao registrada e aguardando sincronizacao.", "Solicitacao", MessageBoxButton.OK, MessageBoxImage.Information);
        MessageTextBox.Text = string.Empty;
        await RefreshAsync();
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_options.SetupCompleted)
        {
            ShowSetupOverlay();
            return;
        }

        PopulateSettingsFields();
        SettingsOverlay.Visibility = Visibility.Visible;
    }

    private void TutorialButton_Click(object sender, RoutedEventArgs e)
    {
        ShowTutorialOverlay();
    }

    private async void SaveSetupButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryApplyOptionsFromControls(
                SetupServerUrlTextBox.Text,
                SetupMachineNameTextBox.Text,
                SetupMachineKeyTextBox.Text,
                SetupMachineKindComboBox.SelectedItem,
                _options.SyncIntervalSeconds,
                markSetupCompleted: true,
                out var message))
        {
            MessageBox.Show(message, "Preparar cliente", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SetupOverlay.Visibility = Visibility.Collapsed;
        PopulateSettingsFields();
        EnsureWatchdogStarted();

        MessageBox.Show(
            "Cliente configurado. O app vai sincronizar sozinho a partir de agora.",
            "Preparar cliente",
            MessageBoxButton.OK,
            MessageBoxImage.Information);

        await RefreshAsync();

        if (_options.ShowTutorialOnNextLaunch)
        {
            ShowTutorialOverlay();
        }
    }

    private void CloseSetupButton_Click(object sender, RoutedEventArgs e)
    {
        if (_options.SetupCompleted)
        {
            SetupOverlay.Visibility = Visibility.Collapsed;
        }
        else
        {
            Close();
        }
    }

    private async void SaveSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryApplyOptionsFromControls(
                SettingsServerUrlTextBox.Text,
                SettingsMachineNameTextBox.Text,
                SettingsMachineKeyTextBox.Text,
                SettingsMachineKindComboBox.SelectedItem,
                SettingsSyncIntervalTextBox.Text,
                markSetupCompleted: _options.SetupCompleted,
                out var message))
        {
            MessageBox.Show(message, "Configuracoes", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _options.ShowTutorialOnNextLaunch = RepeatTutorialCheckBox.IsChecked == true;
        ClientOptionsStore.Save(_options);
        SettingsOverlay.Visibility = Visibility.Collapsed;

        MessageBox.Show(
            "Configuracoes salvas.",
            "Configuracoes",
            MessageBoxButton.OK,
            MessageBoxImage.Information);

        await RefreshAsync();
    }

    private void CloseSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        SettingsOverlay.Visibility = Visibility.Collapsed;
    }

    private void OpenTutorialFromSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        ShowTutorialOverlay();
    }

    private void CloseTutorialButton_Click(object sender, RoutedEventArgs e)
    {
        TutorialOverlay.Visibility = Visibility.Collapsed;

        if (_options.ShowTutorialOnNextLaunch)
        {
            _options.ShowTutorialOnNextLaunch = false;
            ClientOptionsStore.Save(_options);
            PopulateSettingsFields();
        }
    }

    private void HandlePinBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;
        LoginButton_Click(sender, new RoutedEventArgs());
    }

    private void ShowSetupOverlay()
    {
        PopulateSetupFields();
        SetupOverlay.Visibility = Visibility.Visible;
        SettingsOverlay.Visibility = Visibility.Collapsed;
        TutorialOverlay.Visibility = Visibility.Collapsed;
    }

    private void ShowTutorialOverlay()
    {
        SetupOverlay.Visibility = Visibility.Collapsed;
        TutorialOverlay.Visibility = Visibility.Visible;
        SettingsOverlay.Visibility = Visibility.Collapsed;
    }

    private void PopulateSetupFields()
    {
        SetupServerUrlTextBox.Text = ShouldHideDefaultServerUrl() ? string.Empty : _options.ServerBaseUrl;
        SetupMachineNameTextBox.Text = _options.MachineName;
        SetupMachineKeyTextBox.Text = _options.MachineKey;
        SetupMachineKindComboBox.SelectedItem = _options.MachineKind;
    }

    private void PopulateSettingsFields()
    {
        SettingsServerUrlTextBox.Text = _options.ServerBaseUrl;
        SettingsMachineNameTextBox.Text = _options.MachineName;
        SettingsMachineKeyTextBox.Text = _options.MachineKey;
        SettingsMachineKindComboBox.SelectedItem = _options.MachineKind;
        SettingsSyncIntervalTextBox.Text = Math.Max(3, _options.SyncIntervalSeconds).ToString();
        RepeatTutorialCheckBox.IsChecked = _options.ShowTutorialOnNextLaunch;
    }

    private void EnsureWatchdogStarted()
    {
        if (_watchdogStarted || !_options.SetupCompleted || !_options.LaunchLocalWatchdog)
        {
            return;
        }

        ClientWatchdogRunner.LaunchSidecar(Environment.ProcessId);
        _watchdogStarted = true;
    }

    private bool TryApplyOptionsFromControls(
        string serverUrlInput,
        string machineNameInput,
        string machineKeyInput,
        object? machineKindValue,
        int syncIntervalSeconds,
        bool markSetupCompleted,
        out string message)
    {
        if (!TryNormalizeServerUrl(serverUrlInput, out var serverUrl))
        {
            message = "Informe uma URL valida para o servidor do ADMIN. Exemplo: http://192.168.0.10:5076/";
            return false;
        }

        var machineName = string.IsNullOrWhiteSpace(machineNameInput)
            ? Environment.MachineName
            : machineNameInput.Trim();
        var machineKey = string.IsNullOrWhiteSpace(machineKeyInput)
            ? machineName.ToLowerInvariant().Replace(' ', '-')
            : machineKeyInput.Trim().ToLowerInvariant();

        if (machineKindValue is not MachineKind machineKind)
        {
            message = "Escolha o tipo desta maquina.";
            return false;
        }

        _options.ServerBaseUrl = serverUrl;
        _options.MachineName = machineName;
        _options.MachineKey = machineKey;
        _options.MachineKind = machineKind;
        _options.SyncIntervalSeconds = Math.Max(3, syncIntervalSeconds);
        _options.SetupCompleted = markSetupCompleted;
        ClientOptionsStore.Save(_options);

        message = "OK";
        return true;
    }

    private bool TryApplyOptionsFromControls(
        string serverUrlInput,
        string machineNameInput,
        string machineKeyInput,
        object? machineKindValue,
        string syncIntervalInput,
        bool markSetupCompleted,
        out string message)
    {
        if (!int.TryParse(syncIntervalInput, out var syncInterval))
        {
            message = "Informe um intervalo de sincronizacao em segundos.";
            return false;
        }

        return TryApplyOptionsFromControls(
            serverUrlInput,
            machineNameInput,
            machineKeyInput,
            machineKindValue,
            syncInterval,
            markSetupCompleted,
            out message);
    }

    private static bool TryNormalizeServerUrl(string input, out string normalizedUrl)
    {
        normalizedUrl = string.Empty;
        var trimmed = input?.Trim() ?? string.Empty;
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return false;
        }

        normalizedUrl = uri.AbsoluteUri.EndsWith('/') ? uri.AbsoluteUri : $"{uri.AbsoluteUri}/";
        return true;
    }

    private bool ShouldHideDefaultServerUrl()
    {
        if (_options.SetupCompleted)
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(_options.ServerBaseUrl) ||
               string.Equals(_options.ServerBaseUrl, "http://127.0.0.1:5076/", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(_options.ServerBaseUrl, "http://localhost:5076/", StringComparison.OrdinalIgnoreCase);
    }

    private void ApplyWindowMode(bool isLocked, bool setupPending)
    {
        if (setupPending)
        {
            Topmost = false;
            ShowInTaskbar = true;
            ResizeMode = ResizeMode.CanResize;
            WindowStyle = WindowStyle.SingleBorderWindow;
            WindowState = WindowState.Normal;
            Width = 1160;
            Height = 780;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            return;
        }

        Topmost = true;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;
        WindowStyle = WindowStyle.None;

        if (isLocked)
        {
            WindowState = WindowState.Maximized;
            Left = 0;
            Top = 0;
            Width = SystemParameters.PrimaryScreenWidth;
            Height = SystemParameters.PrimaryScreenHeight;
        }
        else
        {
            WindowState = WindowState.Normal;
            Width = 680;
            Height = 460;
            Left = Math.Max(12, SystemParameters.WorkArea.Right - Width - 16);
            Top = Math.Max(12, SystemParameters.WorkArea.Top + 16);
        }
    }

    private void ApplyTheme(Adrenalina.Domain.ThemeMode theme)
    {
        if (theme == Adrenalina.Domain.ThemeMode.Light)
        {
            Background = Brushes.WhiteSmoke;
            Foreground = Brushes.Black;
        }
        else
        {
            Background = new SolidColorBrush(Color.FromRgb(11, 16, 32));
            Foreground = Brushes.White;
        }
    }

    private static string DescribeProfile(UserProfileType profile) => profile switch
    {
        UserProfileType.Admin => "Administrador",
        UserProfileType.Special => "Especial",
        UserProfileType.Ghost => "Ghost",
        _ => "Comum"
    };

    private static string ResolveProfileLabel(ClientRuntimeState state, bool setupPending)
    {
        if (setupPending)
        {
            return "Configuracao inicial";
        }

        return string.IsNullOrWhiteSpace(state.CurrentUserName)
            ? "Bloqueado"
            : DescribeProfile(state.CurrentUserProfile);
    }

    private static string FormatRemainingTime(ClientRuntimeState state)
    {
        if (state.CurrentUserProfile is UserProfileType.Admin or UserProfileType.Special or UserProfileType.Ghost)
        {
            return "Livre";
        }

        return $"{state.RemainingMinutes} min";
    }
}
