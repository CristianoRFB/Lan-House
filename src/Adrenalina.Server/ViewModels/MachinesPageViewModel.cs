using Adrenalina.Application;

namespace Adrenalina.Server.ViewModels;

public sealed class MachinesPageViewModel
{
    public IReadOnlyList<MachineDto> Machines { get; init; } = [];
}
