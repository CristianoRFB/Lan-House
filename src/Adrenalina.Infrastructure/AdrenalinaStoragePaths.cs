namespace Adrenalina.Infrastructure;

public sealed class AdrenalinaStoragePaths
{
    public string DatabaseFilePath { get; init; } = string.Empty;
    public string BackupDirectory { get; init; } = string.Empty;
    public string LogDirectory { get; init; } = string.Empty;
    public string ClientRuntimeDirectory { get; init; } = string.Empty;
}
