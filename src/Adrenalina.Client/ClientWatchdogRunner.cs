using System.Diagnostics;

namespace Adrenalina.Client;

public static class ClientWatchdogRunner
{
    public static void LaunchSidecar(int parentProcessId)
    {
        using var guard = SingleInstanceGuard.TryAcquire("Global\\Adrenalina.Client.Watchdog.Launch");
        if (guard is null)
        {
            return;
        }

        var executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return;
        }

        Process.Start(new ProcessStartInfo(executablePath, $"--watchdog --pid {parentProcessId}")
        {
            UseShellExecute = false,
            CreateNoWindow = true
        });
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
}
