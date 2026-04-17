using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
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

    private bool _localHideTimer;

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

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _refreshTimer.Tick += async (_, _) => await RefreshAsync();

        Loaded += HandleLoaded;
        Closing += HandleClosing;
    }

    private async void HandleLoaded(object sender, RoutedEventArgs e)
    {
        if (_options.LaunchLocalWatchdog)
        {
            ClientWatchdogRunner.LaunchSidecar(Environment.ProcessId);
        }

        _refreshTimer.Start();
        await RefreshAsync();
    }

    private void HandleClosing(object? sender, CancelEventArgs e)
    {
        // Segurança: o cliente não pode ser encerrado pelo usuário comum.
        e.Cancel = true;
    }

    private async Task RefreshAsync()
    {
        var state = await _runtimeStore.LoadStateAsync();
        _kioskManager.ApplyState(state, _gateway.CurrentBlockedProgramsCsv);

        ApplyTheme(state.Theme);
        ApplyWindowMode(state.IsLocked);

        MachineTitleText.Text = state.MachineName;
        SessionStatusText.Text = state.SessionMessage;
        CurrentUserText.Text = $"Usuário: {state.CurrentUserName}";
        ProfileText.Text = $"Perfil: {DescribeProfile(state.CurrentUserProfile)}";
        RemainingTimeText.Text = state.ShowRemainingTime && !_localHideTimer ? FormatRemainingTime(state) : "Oculto";
        BalanceText.Text = $"R$ {state.CurrentBalance:N2}";
        AnnotationText.Text = $"R$ {state.PendingAnnotations:N2}";
        NotesText.Text = string.IsNullOrWhiteSpace(state.CurrentUserNotes) ? "Sem observações." : state.CurrentUserNotes;
        NotificationsList.ItemsSource = state.Notifications
            .Select(item => $"{item.Title}: {item.Message}")
            .ToList();

        LoginCard.Visibility = state.IsLocked ? Visibility.Visible : Visibility.Collapsed;
        Grid.SetColumnSpan(SessionCard, state.IsLocked ? 1 : 2);
        SessionCard.Margin = state.IsLocked ? new Thickness(0, 0, 14, 0) : new Thickness(0);
        LoginButton.Content = state.IsLocked ? "Entrar" : "Atualizar sessão";
    }

    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        var response = await _gateway.LoginAsync(LoginTextBox.Text, PinBox.Password);
        MessageBox.Show(
            response.Message,
            response.Success ? "Sessão iniciada" : "Acesso negado",
            MessageBoxButton.OK,
            response.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);

        if (response.Success)
        {
            LoginTextBox.Text = string.Empty;
            PinBox.Password = string.Empty;
            MessageTextBox.Text = string.Empty;
        }

        await RefreshAsync();
    }

    private async void RequestRegistration_Click(object sender, RoutedEventArgs e)
    {
        await QueueRequestAsync(ClientRequestType.Registration);
    }

    private async void RequestMoreTime_Click(object sender, RoutedEventArgs e)
    {
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
                Login = LoginTextBox.Text,
                Pin = PinBox.Password,
                DisplayName = DisplayNameTextBox.Text,
                Message = MessageTextBox.Text,
                OccurredAtUtc = DateTime.UtcNow
            });

        MessageBox.Show("Solicitação registrada e aguardando sincronização.", "Solicitação", MessageBoxButton.OK, MessageBoxImage.Information);
        MessageTextBox.Text = string.Empty;
        await RefreshAsync();
    }

    private void ApplyWindowMode(bool isLocked)
    {
        Topmost = true;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;

        if (isLocked)
        {
            WindowStyle = WindowStyle.None;
            WindowState = WindowState.Maximized;
            Left = 0;
            Top = 0;
            Width = SystemParameters.PrimaryScreenWidth;
            Height = SystemParameters.PrimaryScreenHeight;
        }
        else
        {
            WindowStyle = WindowStyle.None;
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

    private static string FormatRemainingTime(ClientRuntimeState state)
    {
        if (state.CurrentUserProfile is UserProfileType.Admin or UserProfileType.Special or UserProfileType.Ghost)
        {
            return "Livre";
        }

        return $"{state.RemainingMinutes} min";
    }
}
