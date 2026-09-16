using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace Adrenalina.Agent;

public sealed class WindowsPolicyEnforcer
{
    private static readonly (string Path, string Name)[] PolicyValues =
    [
        (@"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer", "NoRun"),
        (@"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer", "NoViewContextMenu"),
        (@"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer", "NoTrayContextMenu"),
        (@"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer", "NoDesktop"),
        (@"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer", "NoWinKeys"),
        (@"Software\Microsoft\Windows\CurrentVersion\Policies\System", "DisableTaskMgr")
    ];

    private static readonly string BackupPath = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "Adrenalina", "Agent", "policy-backup.json");

    private readonly object _sync = new();
    private bool _maintenance;
    private bool _sessionActive;
    public bool SessionActive => _sessionActive;
    public bool InMaintenance => _maintenance;

    public void SetSessionActive(bool active)
    {
        lock (_sync)
        {
            _sessionActive = active;
            if (active)
            {
                RestoreNormal();
            }
            else if (!_maintenance)
            {
                ApplyBlocked();
            }
        }
    }

    public bool ApplyBlocked()
    {
        lock (_sync)
        {
            if (_maintenance)
            {
                return true;
            }

            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(BackupPath)!);
            if (!File.Exists(BackupPath))
            {
                var backup = PolicyValues.Select(value => new RegistryBackup
                {
                    Path = value.Path,
                    Name = value.Name,
                    Value = ReadValue(value.Path, value.Name)
                }).ToList();
                File.WriteAllText(BackupPath, System.Text.Json.JsonSerializer.Serialize(backup));
            }

            foreach (var value in PolicyValues)
            {
                using var key = Registry.LocalMachine.CreateSubKey(value.Path, writable: true);
                key?.SetValue(value.Name, 1, RegistryValueKind.DWord);
            }

            NotifyShell();
            return true;
        }
    }

    public bool RestoreNormal()
    {
        lock (_sync)
        {
            if (!File.Exists(BackupPath))
            {
                return true;
            }

            var backup = System.Text.Json.JsonSerializer.Deserialize<List<RegistryBackup>>(File.ReadAllText(BackupPath)) ?? [];
            foreach (var item in backup)
            {
                using var key = Registry.LocalMachine.CreateSubKey(item.Path, writable: true);
                if (key is null)
                {
                    continue;
                }

                if (item.Value is null)
                {
                    key.DeleteValue(item.Name, throwOnMissingValue: false);
                }
                else
                {
                    var value = item.Value is System.Text.Json.JsonElement jsonElement && jsonElement.ValueKind == System.Text.Json.JsonValueKind.Number
                        ? jsonElement.GetInt32()
                        : item.Value;
                    key.SetValue(item.Name, value, RegistryValueKind.DWord);
                }
            }

            File.Delete(BackupPath);
            NotifyShell();
            return true;
        }
    }

    public void EnterMaintenance()
    {
        lock (_sync)
        {
            _maintenance = true;
            _sessionActive = false;
            RestoreNormal();
        }
    }

    public void ExitMaintenance()
    {
        lock (_sync)
        {
            _maintenance = false;
            _sessionActive = false;
            ApplyBlocked();
        }
    }

    public bool Restart() => ExitWindowsEx(0x00000002, 0);
    public bool Shutdown() => ExitWindowsEx(0x00000001, 0);
    public bool Logoff() => ExitWindowsEx(0, 0);

    public static void RestoreNormalOnDisk()
    {
        var enforcer = new WindowsPolicyEnforcer();
        enforcer.RestoreNormal();
    }

    private static object? ReadValue(string path, string name)
    {
        using var key = Registry.LocalMachine.OpenSubKey(path, writable: false);
        return key?.GetValue(name);
    }

    private static void NotifyShell() => SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ExitWindowsEx(uint flags, uint reason);

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(uint eventId, uint flags, IntPtr item1, IntPtr item2);

    private sealed class RegistryBackup
    {
        public string Path { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public object? Value { get; init; }
    }
}
