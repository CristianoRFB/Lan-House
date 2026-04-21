using System.Runtime.InteropServices;

namespace Adrenalina.Admin;

public sealed class AdminSingleInstanceGuard : IDisposable
{
    private const string MutexName = @"Local\Adrenalina.Admin.SingleInstance";
    private const string MainWindowTitle = "Adrenalina ADMIN";
    private readonly Mutex _mutex;
    private bool _disposed;

    private AdminSingleInstanceGuard(Mutex mutex)
    {
        _mutex = mutex;
    }

    public static AdminSingleInstanceGuard? TryAcquire()
    {
        var mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        if (createdNew)
        {
            return new AdminSingleInstanceGuard(mutex);
        }

        mutex.Dispose();
        return null;
    }

    public static void TryActivateExistingWindow()
    {
        var windowHandle = FindWindow(lpClassName: null, MainWindowTitle);
        if (windowHandle == IntPtr.Zero)
        {
            return;
        }

        ShowWindow(windowHandle, SwShow);
        ShowWindow(windowHandle, SwRestore);
        SetForegroundWindow(windowHandle);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _mutex.ReleaseMutex();
        _mutex.Dispose();
        _disposed = true;
    }

    private const int SwShow = 5;
    private const int SwRestore = 9;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}
