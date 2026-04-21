namespace Adrenalina.Application;

public static class AdrenalinaPaths
{
    public static string GetAdminDataRoot()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Adrenalina",
            "Admin");
    }

    public static string GetLegacyAdminDataRoot()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Adrenalina",
            "admin-data");
    }

    public static string GetClientSettingsRoot()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Adrenalina",
            "Client");
    }

    public static string GetLegacyClientSettingsRoot()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Adrenalina",
            "client");
    }

    public static string GetClientRuntimeRoot()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Adrenalina",
            "Runtime");
    }

    public static string GetLegacyClientRuntimeRoot()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Adrenalina",
            "runtime");
    }
}
