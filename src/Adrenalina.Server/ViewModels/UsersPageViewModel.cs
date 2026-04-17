using Adrenalina.Application;

namespace Adrenalina.Server.ViewModels;

public sealed class UsersPageViewModel
{
    public IReadOnlyList<UserDto> Users { get; init; } = [];
    public UserUpsertRequest Form { get; init; } = new();
    public LedgerEntryRequest LedgerForm { get; init; } = new();
}
