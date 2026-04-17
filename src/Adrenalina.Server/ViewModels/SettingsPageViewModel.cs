using Adrenalina.Application;

namespace Adrenalina.Server.ViewModels;

public sealed class SettingsPageViewModel
{
    public SettingsUpdateRequest Form { get; init; } = new();
}
