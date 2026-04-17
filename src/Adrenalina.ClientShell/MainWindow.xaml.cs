using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Adrenalina.Application;
using Adrenalina.Domain;
using Adrenalina.Infrastructure;

namespace Adrenalina.ClientShell;

public partial class MainWindow : Window
{
    private readonly JsonClientRuntimeStore _store;
    private readonly DispatcherTimer _timer;
    private bool _localHideTimer;

    public MainWindow()
    {
        InitializeComponent();

        var runtimeRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Adrenalina", "runtime", Environment.MachineName);
        _store = new JsonClientRuntimeStore(new LocalClientStoragePaths
        {
            StateFilePath = Path.Combine(runtimeRoot, "client-state.json"),
            RequestQueueFilePath = Path.Combine(runtimeRoot, "client-requests.json")
        });

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _timer.Tick += async (_, _) => await RefreshAsync();

        Loaded += async (_, _) =>
        {
            _timer.Start();
            await RefreshAsync();
        };
    }

    private async Task RefreshAsync()
    {
        var state = await _store.LoadStateAsync();
        ApplyTheme(state.Theme);

        MachineTitleText.Text = state.MachineName;
        SessionStatusText.Text = state.SessionMessage;
        CurrentUserText.Text = $"Usuário: {state.CurrentUserName}";
        ProfileText.Text = $"Perfil: {state.CurrentUserProfile}";
        RemainingTimeText.Text = state.ShowRemainingTime && !_localHideTimer ? $"{state.RemainingMinutes} min" : "Oculto";
        BalanceText.Text = $"R$ {state.CurrentBalance:N2}";
        AnnotationText.Text = $"R$ {state.PendingAnnotations:N2}";
        NotesText.Text = string.IsNullOrWhiteSpace(state.CurrentUserNotes) ? "Sem observações." : state.CurrentUserNotes;
        LockMessageText.Text = string.IsNullOrWhiteSpace(state.LockMessage) ? "Procure o administrador para continuar." : state.LockMessage;

        NotificationsList.ItemsSource = state.Notifications.Select(item => $"{item.Title}: {item.Message}").ToList();
        ApplyLockState(state.IsLocked);
    }

    private async void RequestLogin_Click(object sender, RoutedEventArgs e)
    {
        await QueueRequestAsync(ClientRequestType.Login);
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
        await _store.EnqueueRequestAsync(new ClientShellRequest
        {
            Type = type,
            Login = LoginTextBox.Text,
            Pin = PinBox.Password,
            DisplayName = DisplayNameTextBox.Text,
            Message = MessageTextBox.Text,
            OccurredAtUtc = DateTime.UtcNow
        });

        MessageTextBox.Text = string.Empty;
        await RefreshAsync();
    }

    private void ApplyLockState(bool isLocked)
    {
        LockOverlay.Visibility = isLocked ? Visibility.Visible : Visibility.Collapsed;
        Topmost = isLocked;

        if (isLocked)
        {
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            WindowState = WindowState.Maximized;
        }
        else
        {
            WindowStyle = WindowStyle.SingleBorderWindow;
            ResizeMode = ResizeMode.CanResize;
            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
            }
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
            Background = new SolidColorBrush(Color.FromRgb(16, 19, 25));
            Foreground = Brushes.White;
        }
    }
}
