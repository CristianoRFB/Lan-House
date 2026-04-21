using System.Text.Json;
using Adrenalina.Application;

namespace Adrenalina.Admin;

public static class AdminDesktopOptionsStore
{
    public static AdminDesktopOptions LoadOrCreate()
    {
        var path = GetSettingsPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        if (!File.Exists(path))
        {
            var defaults = new AdminDesktopOptions
            {
                ShowTutorialOnNextLaunch = true
            };

            Save(defaults);
            return defaults;
        }

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<AdminDesktopOptions>(json, JsonDefaults.Options)
               ?? new AdminDesktopOptions();
    }

    public static void Save(AdminDesktopOptions options)
    {
        var path = GetSettingsPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var payload = JsonSerializer.Serialize(options, JsonDefaults.Options);
        File.WriteAllText(path, payload);
    }

    public static string GetSettingsPath()
    {
        var path = Path.Combine(
            Adrenalina.Application.AdrenalinaPaths.GetAdminDataRoot(),
            "admin-app.json");
        var legacyPath = Path.Combine(
            Adrenalina.Application.AdrenalinaPaths.GetLegacyAdminDataRoot(),
            "admin-app.json");

        if (!File.Exists(path) && File.Exists(legacyPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.Copy(legacyPath, path);
        }

        return path;
    }
}
