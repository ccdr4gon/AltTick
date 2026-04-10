using System.Runtime.InteropServices;
using System.Text;

namespace AltTick.Interop;

internal static class NativeMethods
{
    internal delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
    internal delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    // Kernel32
    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    internal static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("kernel32.dll")]
    internal static extern uint GetCurrentThreadId();

    // User32 - Hooks
    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    internal static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    internal static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    // User32 - Window enumeration
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    internal static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    internal static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetWindow(IntPtr hWnd, int uCmd);

    [DllImport("user32.dll")]
    internal static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    internal static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetShellWindow();

    // User32 - Window management
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsZoomed(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

    [DllImport("user32.dll")]
    internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr lpdwProcessId);

    // User32 - Icons
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    internal static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetClassLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DestroyIcon(IntPtr handle);

    // User32 - Monitor
    [DllImport("user32.dll")]
    internal static extern IntPtr MonitorFromWindow(IntPtr hwnd, int dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    // User32 - Keyboard
    [DllImport("user32.dll")]
    internal static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    internal static extern void keybd_event(byte bVk, byte bScan, int dwFlags, int dwExtraInfo);

    // User32 - DPI
    [DllImport("user32.dll")]
    internal static extern uint GetDpiForWindow(IntPtr hwnd);

    // Dwmapi
    [DllImport("dwmapi.dll")]
    internal static extern int DwmRegisterThumbnail(IntPtr hwndDestination, IntPtr hwndSource, out IntPtr phThumbnailId);

    [DllImport("dwmapi.dll")]
    internal static extern int DwmUnregisterThumbnail(IntPtr hThumbnailId);

    [DllImport("dwmapi.dll")]
    internal static extern int DwmUpdateThumbnailProperties(IntPtr hThumbnailId, ref DWM_THUMBNAIL_PROPERTIES ptnProperties);

    [DllImport("dwmapi.dll")]
    internal static extern int DwmQueryThumbnailSourceSize(IntPtr hThumbnailId, out SIZE pSize);

    [DllImport("dwmapi.dll")]
    internal static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS pMarInset);

    [DllImport("dwmapi.dll")]
    internal static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);

    [DllImport("dwmapi.dll")]
    internal static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out RECT pvAttribute, int cbAttribute);

    // Shell32
    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    internal static extern IntPtr ExtractIcon(IntPtr hInst, string lpszExeFileName, int nIconIndex);
}
