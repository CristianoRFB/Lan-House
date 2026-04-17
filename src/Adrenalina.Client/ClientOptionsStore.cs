using System.Text.Json;
using System.IO;

namespace Adrenalina.Client;

public static class ClientOptionsStore
{
    public static ClientConnectionOptions LoadOrCreate()
    {
        var path = GetSettingsPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        if (!File.Exists(path))
        {
            var defaults = new ClientConnectionOptions();
            var payload = JsonSerializer.Serialize(defaults, Adrenalina.Application.JsonDefaults.Options);
            File.WriteAllText(path, payload);
            return defaults;
        }

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<ClientConnectionOptions>(json, Adrenalina.Application.JsonDefaults.Options)
               ?? new ClientConnectionOptions();
    }

    public static string GetSettingsPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Adrenalina",
            "client",
            "clientsettings.json");
    }
}
