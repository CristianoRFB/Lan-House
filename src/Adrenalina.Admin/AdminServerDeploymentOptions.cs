using System.Text.Json;
using Adrenalina.Application;

namespace Adrenalina.Admin;

public sealed class AdminServerDeploymentOptions
{
    public bool ListenOnLocalNetwork { get; init; }
    public bool UseHttps { get; init; }
    public string? CertificateThumbprint { get; init; }
}

public static class AdminServerDeploymentOptionsStore
{
    public static AdminServerDeploymentOptions Load()
    {
        var path = AdrenalinaPaths.GetDeploymentSettingsPath();
        if (!File.Exists(path))
        {
            return new AdminServerDeploymentOptions();
        }

        try
        {
            return JsonSerializer.Deserialize<AdminServerDeploymentOptions>(
                       File.ReadAllText(path),
                       JsonDefaults.Options)
                   ?? new AdminServerDeploymentOptions();
        }
        catch (JsonException)
        {
            return new AdminServerDeploymentOptions();
        }
    }
}
