using System.Text.Json;
using System.IO;

namespace Adrenalina.Client;

public static class ClientOptionsStore
{
    public static ClientConnectionOptions LoadOrCreate(string? settingsPath = null)
    {
        var path = settingsPath ?? GetSettingsPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        if (!File.Exists(path))
        {
            var defaults = new ClientConnectionOptions
            {
                SetupCompleted = false,
                ShowTutorialOnNextLaunch = true
            };

            Save(defaults, path);
            return defaults;
        }

        ClientConnectionOptions options;
        try
        {
            var json = File.ReadAllText(path);
            options = JsonSerializer.Deserialize<ClientConnectionOptions>(json, Adrenalina.Application.JsonDefaults.Options)
                      ?? new ClientConnectionOptions();
        }
        catch (JsonException)
        {
            File.Move(path, $"{path}.corrupt-{DateTime.UtcNow:yyyyMMddHHmmssfff}");
            options = new ClientConnectionOptions
            {
                SetupCompleted = false,
                ShowTutorialOnNextLaunch = true
            };
            Save(options, path);
        }

        NormalizeForInteractiveSetup(options);
        return options;
    }

    public static void Save(ClientConnectionOptions options, string? settingsPath = null)
    {
        var path = settingsPath ?? GetSettingsPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var payload = JsonSerializer.Serialize(options, Adrenalina.Application.JsonDefaults.Options);
        var temporaryPath = path + ".tmp";
        File.WriteAllText(temporaryPath, payload);
        File.Move(temporaryPath, path, overwrite: true);
    }

    public static string GetSettingsPath()
    {
        var path = Path.Combine(
            Adrenalina.Application.AdrenalinaPaths.GetClientSettingsRoot(),
            "clientsettings.json");
        var legacyPath = Path.Combine(
            Adrenalina.Application.AdrenalinaPaths.GetLegacyClientSettingsRoot(),
            "clientsettings.json");

        if (!File.Exists(path) && File.Exists(legacyPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.Copy(legacyPath, path);
        }

        return path;
    }

    private static void NormalizeForInteractiveSetup(ClientConnectionOptions options)
    {
        if (options.SetupCompleted)
        {
            return;
        }

        if (string.Equals(options.ServerBaseUrl, "http://127.0.0.1:5076/", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(options.ServerBaseUrl, "http://localhost:5076/", StringComparison.OrdinalIgnoreCase))
        {
            options.ServerBaseUrl = string.Empty;
        }
    }
}
