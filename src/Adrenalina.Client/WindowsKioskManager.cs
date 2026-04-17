using System.Diagnostics;
using System.Runtime.InteropServices;
using Adrenalina.Application;
using Microsoft.Win32;

namespace Adrenalina.Client;

public sealed class WindowsKioskManager : IDisposable
{
    private readonly NativeMethods.LowLevelKeyboardProc _keyboardProc;
    private readonly System.Threading.Timer _processTimer;
    private readonly HashSet<string> _alwaysAllowedProcesses =
    [
        "adrenalina.client.exe",
        "csrss.exe",
        "ctfmon.exe",
        "dllhost.exe",
        "dwm.exe",
        "fontdrvhost.exe",
        "lsass.exe",
        "registry.exe",
        "runtimebroker.exe",
        "searchhost.exe",
        "securityhealthsystray.exe",
        "services.exe",
        "sihost.exe",
        "smss.exe",
        "spoolsv.exe",
        "startmenuexperiencehost.exe",
        "svchost.exe",
        "system.exe",
        "taskhostw.exe",
        "textinputhost.exe",
        "wininit.exe",
        "winlogon.exe",
        "wmiprvse.exe"
    ];

    private readonly object _stateLock = new();
    private IntPtr _keyboardHook = IntPtr.Zero;
    private bool _isLocked;
    private HashSet<string> _blockedProcesses = [];

    public WindowsKioskManager()
    {
        _keyboardProc = KeyboardHookCallback;
        _processTimer = new System.Threading.Timer(EnforceProcessRules, null, Timeout.Infinite, Timeout.Infinite);
    }

    public void ApplyState(ClientRuntimeState state, string blockedProgramsCsv)
    {
        lock (_stateLock)
        {
            _blockedProcesses = blockedProgramsCsv
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => value.ToLowerInvariant())
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
        DisableLockdown();
        _processTimer.Dispose();
    }

    private void EnableLockdown()
    {
        InstallKeyboardHook();
        SetTaskManagerPolicy(true);
        SetTaskbarVisibility(false);
        StopExplorer();
        _processTimer.Change(TimeSpan.Zero, TimeSpan.FromSeconds(2));
    }

    private void DisableLockdown()
    {
        _processTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        RemoveKeyboardHook();
        SetTaskManagerPolicy(false);
        SetTaskbarVisibility(true);
        StartExplorer();
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

    private void EnforceProcessRules(object? _)
    {
        if (!_isLocked)
        {
            return;
        }

        foreach (var process in Process.GetProcesses())
        {
            try
            {
                var executable = $"{process.ProcessName}.exe".ToLowerInvariant();
                if (_alwaysAllowedProcesses.Contains(executable))
                {
                    continue;
                }

                if (process.Id == Environment.ProcessId)
                {
                    continue;
                }

                if (process.SessionId == 0)
                {
                    continue;
                }

                if (executable == "explorer.exe" || _blockedProcesses.Contains(executable))
                {
                    process.Kill(true);
                    continue;
                }

                // Segurança: durante o bloqueio a estação só mantém os processos essenciais.
                // Isso exige privilégios locais para ser efetivo contra todos os aplicativos.
                process.Kill(true);
            }
            catch
            {
                // Alguns processos são protegidos pelo Windows e ignoramos essas falhas.
            }
        }
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

    private static void SetTaskManagerPolicy(bool disabled)
    {
        // Limitação: HKCU protege o usuário conectado. Para endurecimento total da máquina,
        // a instalação deve aplicar a mesma política em HKLM com privilégios de administrador.
        using var policyKey = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Policies\System");
        if (policyKey is null)
        {
            return;
        }

        if (disabled)
        {
            policyKey.SetValue("DisableTaskMgr", 1, RegistryValueKind.DWord);
        }
        else
        {
            policyKey.DeleteValue("DisableTaskMgr", false);
        }
    }

    private static void SetTaskbarVisibility(bool visible)
    {
        var taskbar = NativeMethods.FindWindow("Shell_TrayWnd", null);
        if (taskbar != IntPtr.Zero)
        {
            NativeMethods.ShowWindow(taskbar, visible ? NativeMethods.SwShow : NativeMethods.SwHide);
        }
    }

    private static void StopExplorer()
    {
        foreach (var process in Process.GetProcessesByName("explorer"))
        {
            try
            {
                process.Kill(true);
            }
            catch
            {
                // Se o shell estiver protegido por política do Windows, seguimos com o restante do bloqueio.
            }
        }
    }

    private static void StartExplorer()
    {
        if (Process.GetProcessesByName("explorer").Length > 0)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe")
            {
                UseShellExecute = true
            });
        }
        catch
        {
            // Ambientes que usam shell customizado podem optar por não reabrir o Explorer.
        }
    }
}
