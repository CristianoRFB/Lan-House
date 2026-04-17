using Adrenalina.Application;

namespace Adrenalina.Server.ViewModels;

public sealed class SessionsPageViewModel
{
    public IReadOnlyList<SessionDto> Sessions { get; init; } = [];
    public IReadOnlyList<MachineDto> Machines { get; init; } = [];
    public IReadOnlyList<UserDto> Users { get; init; } = [];
    public SessionStartRequest StartForm { get; init; } = new();
    public SessionAdjustRequest AdjustForm { get; init; } = new();
}
