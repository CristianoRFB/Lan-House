using Adrenalina.Application;

namespace Adrenalina.Server.ViewModels;

public sealed class DashboardPageViewModel
{
    public DashboardDto Dashboard { get; init; } = new();
    public SettingsDto Settings { get; init; } = new();
}
