using System.Diagnostics;
using System.IO;

namespace Adrenalina.Client;

public static class ClientWatchdogRunner
{
    public static bool LaunchSidecar(int parentProcessId)
    {
        using var guard = SingleInstanceGuard.TryAcquire("Global\\Adrenalina.Client.Watchdog.Launch");
        if (guard is null)
        {
            return false;
        }

        var executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return false;
        }

        try
        {
            var exitMarkerPath = GetExitMarkerPath(parentProcessId);
            if (File.Exists(exitMarkerPath))
            {
                File.Delete(exitMarkerPath);
            }

            using var process = Process.Start(new ProcessStartInfo(executablePath, $"--watchdog --pid {parentProcessId}")
            {
                UseShellExecute = false,
                CreateNoWindow = true
            });
            return process is not null;
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"Nao foi possivel iniciar watchdog do CLIENTE: {exception}");
            return false;
        }
    }

    public static void MarkIntentionalExit(int processId)
    {
        var path = GetExitMarkerPath(processId);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, string.Empty);
    }

    public static async Task RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        using var instance = SingleInstanceGuard.TryAcquire("Global\\Adrenalina.Client.Watchdog");
        if (instance is null)
        {
            return;
        }

        if (!TryReadParentPid(args, out var parentProcessId))
        {
            return;
        }

        var executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return;
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);

            if (IsProcessAlive(parentProcessId))
            {
                continue;
            }

            var exitMarkerPath = GetExitMarkerPath(parentProcessId);
            if (File.Exists(exitMarkerPath))
            {
                File.Delete(exitMarkerPath);
                return;
            }

            var uiGuard = SingleInstanceGuard.TryAcquire("Global\\Adrenalina.Client.UI");
            if (uiGuard is null)
            {
                return;
            }

            uiGuard.Dispose();

            Process.Start(new ProcessStartInfo(executablePath)
            {
                UseShellExecute = true
            });
            return;
        }
    }

    private static bool TryReadParentPid(IReadOnlyList<string> args, out int parentProcessId)
    {
        parentProcessId = 0;
        for (var index = 0; index < args.Count - 1; index++)
        {
            if (args[index] == "--pid" && int.TryParse(args[index + 1], out parentProcessId))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsProcessAlive(int pid)
    {
        try
        {
            var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch
        {
            return false;
        }
    }

    private static string GetExitMarkerPath(int processId) => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Adrenalina",
        "Runtime",
        $"client-exit-{processId}.marker");
}
