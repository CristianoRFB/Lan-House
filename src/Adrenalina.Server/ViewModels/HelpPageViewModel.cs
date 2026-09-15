namespace Adrenalina.Server.ViewModels;

public sealed class HelpPageViewModel
{
    public int Version { get; init; }
    public IReadOnlyList<string> Categories { get; init; } = [];
    public IReadOnlyList<HelpArticleViewModel> Articles { get; init; } = [];
    public IReadOnlyList<HelpChecklistItemViewModel> Checklist { get; init; } = [];
}

public sealed record HelpArticleViewModel(
    string Id,
    string Category,
    string Title,
    string Summary,
    IReadOnlyList<string> Steps,
    IReadOnlyList<string> Keywords,
    IReadOnlyList<string> Related,
    bool AdminOnly = false);

public sealed record HelpChecklistItemViewModel(
    string Id,
    string Label,
    string ArticleId,
    bool AdminOnly = false);
