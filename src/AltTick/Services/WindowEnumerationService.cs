using System.Diagnostics;
using System.Runtime.InteropServices;
using AltTick.Interop;
using AltTick.Models;

namespace AltTick.Services;

internal static class WindowEnumerationService
{
    public static List<AppWindow> GetWindowsForSameApp(IntPtr foregroundHwnd)
    {
        if (foregroundHwnd == IntPtr.Zero)
            foregroundHwnd = NativeMethods.GetForegroundWindow();

        if (foregroundHwnd == IntPtr.Zero)
            return [];

        NativeMethods.GetWindowThreadProcessId(foregroundHwnd, out uint targetPid);
        if (targetPid == 0)
            return [];

        // Check if it's an ApplicationFrameHost (UWP) process
        uint realPid = ResolveUwpProcessId(foregroundHwnd, targetPid);

        var shellWindow = NativeMethods.GetShellWindow();
        var windows = new List<AppWindow>();

        NativeMethods.EnumWindows((hWnd, _) =>
        {
            if (hWnd == shellWindow)
                return true;

            if (!IsValidAppWindow(hWnd))
                return true;

            NativeMethods.GetWindowThreadProcessId(hWnd, out uint windowPid);
            uint resolvedPid = ResolveUwpProcessId(hWnd, windowPid);

            if (resolvedPid == realPid)
            {
                // Check if cloaked (hidden by virtual desktop, etc.)
                if (!IsCloaked(hWnd))
                {
                    windows.Add(new AppWindow(hWnd));
                }
            }

            return true;
        }, IntPtr.Zero);

        // Put foreground window first so cycling starts from the next one
        var fgIndex = windows.FindIndex(w => w.Handle == foregroundHwnd);
        if (fgIndex > 0)
        {
            var fg = windows[fgIndex];
            windows.RemoveAt(fgIndex);
            windows.Insert(0, fg);
        }

        return windows;
    }

    private static bool IsValidAppWindow(IntPtr hWnd)
    {
        if (!NativeMethods.IsWindowVisible(hWnd))
            return false;

        if (NativeMethods.GetWindowTextLength(hWnd) == 0)
            return false;

        int exStyle = NativeMethods.GetWindowLong(hWnd, NativeConstants.GWL_EXSTYLE);

        // Tool windows should be excluded unless they also have WS_EX_APPWINDOW
        if ((exStyle & (int)NativeConstants.WS_EX_TOOLWINDOW) != 0 &&
            (exStyle & (int)NativeConstants.WS_EX_APPWINDOW) == 0)
            return false;

        // No-activate windows (like volume overlay)
        if ((exStyle & (int)NativeConstants.WS_EX_NOACTIVATE) != 0)
            return false;

        // Skip owned windows (child dialogs, etc.) unless they have WS_EX_APPWINDOW
        IntPtr owner = NativeMethods.GetWindow(hWnd, NativeConstants.GW_OWNER);
        if (owner != IntPtr.Zero && (exStyle & (int)NativeConstants.WS_EX_APPWINDOW) == 0)
            return false;

        return true;
    }

    private static bool IsCloaked(IntPtr hWnd)
    {
        int hr = NativeMethods.DwmGetWindowAttribute(hWnd, NativeConstants.DWMWA_CLOAKED, out int cloaked, sizeof(int));
        return hr == 0 && cloaked != 0;
    }

    private static uint ResolveUwpProcessId(IntPtr hWnd, uint pid)
    {
        try
        {
            var process = Process.GetProcessById((int)pid);
            if (string.Equals(process.ProcessName, "ApplicationFrameHost", StringComparison.OrdinalIgnoreCase))
            {
                // Find the real child window
                uint realPid = pid;
                NativeMethods.EnumWindows((childHwnd, _) =>
                {
                    NativeMethods.GetWindowThreadProcessId(childHwnd, out uint childPid);
                    if (childPid != pid && NativeMethods.GetWindow(childHwnd, NativeConstants.GW_OWNER) == hWnd)
                    {
                        realPid = childPid;
                        return false; // stop enumeration
                    }
                    return true;
                }, IntPtr.Zero);

                return realPid;
            }
        }
        catch { }

        return pid;
    }
}
