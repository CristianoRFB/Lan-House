namespace Adrenalina.Server;

public sealed class AdrenalinaServerHostOptions
{
    public string[] Args { get; init; } = [];
    public string? ContentRootPath { get; init; }
    public string? WebRootPath { get; init; }
    public string? DataRootPath { get; init; }
    public string? Urls { get; init; }
    public bool UseHttpsRedirection { get; init; } = true;
}
