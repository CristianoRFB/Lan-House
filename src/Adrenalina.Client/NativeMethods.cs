using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Adrenalina.Client;

internal static class NativeMethods
{
    internal const int WhKeyboardLl = 13;
    internal const int WmKeyDown = 0x0100;
    internal const int WmSysKeyDown = 0x0104;
    internal const int VkTab = 0x09;
    internal const int VkEscape = 0x1B;
    internal const int VkF4 = 0x73;
    internal const int VkLWin = 0x5B;
    internal const int VkRWin = 0x5C;
    internal const int VkMenu = 0x12;
    internal const int VkControl = 0x11;
    internal const int VkShift = 0x10;
    internal const int SwHide = 0;
    internal const int SwShow = 5;

    internal delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    internal struct KbdLlHookStruct
    {
        public uint VkCode;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public nint DwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    internal static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll")]
    internal static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    internal static extern IntPtr FindWindow(string? className, string? windowName);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    internal static IntPtr GetCurrentModuleHandle()
    {
        var moduleName = Process.GetCurrentProcess().MainModule?.ModuleName;
        return GetModuleHandle(moduleName);
    }
}
