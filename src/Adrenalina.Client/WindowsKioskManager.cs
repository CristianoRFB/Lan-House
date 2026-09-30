using System.Diagnostics;
using System.Runtime.InteropServices;
using Adrenalina.Application;

namespace Adrenalina.Client;

public sealed class WindowsKioskManager : IDisposable
{
    private readonly NativeMethods.LowLevelKeyboardProc _keyboardProc;
    private readonly System.Threading.Timer _processTimer;
    private readonly object _stateLock = new();
    private IntPtr _keyboardHook = IntPtr.Zero;
    private bool _isLocked;
    private HashSet<string> _blockedProcesses = [];

    public WindowsKioskManager()
    {
        _keyboardProc = KeyboardHookCallback;
        _processTimer = new System.Threading.Timer(EnforceBlockedPrograms, null, Timeout.Infinite, Timeout.Infinite);
    }

    public void ApplyState(ClientRuntimeState state, string blockedProgramsCsv)
    {
        lock (_stateLock)
        {
            _blockedProcesses = (blockedProgramsCsv ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => value.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? value.ToLowerInvariant() : value.ToLowerInvariant() + ".exe")
                .ToHashSet();

            if (_isLocked == state.IsLocked)
            {
                return;
            }

            _isLocked = state.IsLocked;
            if (_isLocked)
            {
                EnableLockdown();
            }
            else
            {
                DisableLockdown();
            }
        }
    }

    public void Dispose()
    {
        lock (_stateLock)
        {
            _isLocked = false;
            DisableLockdown();
        }
        _processTimer.Dispose();
    }

    private void EnableLockdown()
    {
        InstallKeyboardHook();
        _processTimer.Change(TimeSpan.Zero, TimeSpan.FromSeconds(2));
    }

    private void DisableLockdown()
    {
        _processTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        RemoveKeyboardHook();
    }

    private void EnforceBlockedPrograms(object? _)
    {
        HashSet<string> blocked;
        lock (_stateLock)
        {
            if (!_isLocked || _blockedProcesses.Count == 0)
            {
                return;
            }

            blocked = [.. _blockedProcesses];
        }

        try
        {
            using var currentProcess = Process.GetCurrentProcess();
            var sessionId = currentProcess.SessionId;
            foreach (var process in Process.GetProcesses())
            {
                using (process)
                {
                    try
                    {
                        var executable = process.ProcessName + ".exe";
                        if (process.Id != Environment.ProcessId &&
                            process.SessionId == sessionId &&
                            !executable.Equals("explorer.exe", StringComparison.OrdinalIgnoreCase) &&
                            blocked.Contains(executable.ToLowerInvariant()))
                        {
                            process.Kill();
                        }
                    }
                    catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
                    {
                        // Windows can deny access to protected or already exited processes.
                    }
                }
            }
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"Falha ao aplicar lista negra: {exception}");
        }
    }

    private void InstallKeyboardHook()
    {
        if (_keyboardHook != IntPtr.Zero)
        {
            return;
        }

        _keyboardHook = NativeMethods.SetWindowsHookEx(
            NativeMethods.WhKeyboardLl,
            _keyboardProc,
            NativeMethods.GetCurrentModuleHandle(),
            0);
    }

    private void RemoveKeyboardHook()
    {
        if (_keyboardHook == IntPtr.Zero)
        {
            return;
        }

        NativeMethods.UnhookWindowsHookEx(_keyboardHook);
        _keyboardHook = IntPtr.Zero;
    }

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (!_isLocked || nCode < 0)
        {
            return NativeMethods.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
        }

        var message = wParam.ToInt32();
        if (message is not (NativeMethods.WmKeyDown or NativeMethods.WmSysKeyDown))
        {
            return NativeMethods.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
        }

        var hookStruct = Marshal.PtrToStructure<NativeMethods.KbdLlHookStruct>(lParam);
        var altPressed = (NativeMethods.GetAsyncKeyState(NativeMethods.VkMenu) & 0x8000) != 0;
        var ctrlPressed = (NativeMethods.GetAsyncKeyState(NativeMethods.VkControl) & 0x8000) != 0;
        var shiftPressed = (NativeMethods.GetAsyncKeyState(NativeMethods.VkShift) & 0x8000) != 0;

        var shouldBlock =
            hookStruct.VkCode is NativeMethods.VkLWin or NativeMethods.VkRWin ||
            (altPressed && hookStruct.VkCode is NativeMethods.VkTab or NativeMethods.VkEscape or NativeMethods.VkF4) ||
            (ctrlPressed && hookStruct.VkCode == NativeMethods.VkEscape) ||
            (ctrlPressed && shiftPressed && hookStruct.VkCode == NativeMethods.VkEscape);

        return shouldBlock
            ? new IntPtr(1)
            : NativeMethods.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
    }

}
