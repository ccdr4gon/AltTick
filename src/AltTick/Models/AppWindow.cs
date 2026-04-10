using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AltTick.Interop;

namespace AltTick.Models;

public class AppWindow
{
    public IntPtr Handle { get; }
    public string Title { get; }
    public uint ProcessId { get; }
    public string? ProcessName { get; }
    public ImageSource? Icon { get; }

    public AppWindow(IntPtr handle)
    {
        Handle = handle;
        Title = GetWindowTitle(handle);
        NativeMethods.GetWindowThreadProcessId(handle, out uint pid);
        ProcessId = pid;

        try
        {
            var process = System.Diagnostics.Process.GetProcessById((int)pid);
            ProcessName = process.ProcessName;
        }
        catch
        {
            ProcessName = null;
        }

        Icon = GetWindowIcon(handle);
    }

    private static string GetWindowTitle(IntPtr hWnd)
    {
        int length = NativeMethods.GetWindowTextLength(hWnd);
        if (length == 0) return string.Empty;

        var sb = new StringBuilder(length + 1);
        NativeMethods.GetWindowText(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }

    private static ImageSource? GetWindowIcon(IntPtr hWnd)
    {
        try
        {
            IntPtr iconHandle = NativeMethods.SendMessage(hWnd, NativeConstants.WM_GETICON, NativeConstants.ICON_BIG, 0);
            if (iconHandle == IntPtr.Zero)
                iconHandle = NativeMethods.SendMessage(hWnd, NativeConstants.WM_GETICON, NativeConstants.ICON_SMALL2, 0);
            if (iconHandle == IntPtr.Zero)
                iconHandle = NativeMethods.GetClassLongPtr(hWnd, NativeConstants.GCLP_HICON);
            if (iconHandle == IntPtr.Zero)
                iconHandle = NativeMethods.GetClassLongPtr(hWnd, NativeConstants.GCLP_HICONSM);

            if (iconHandle != IntPtr.Zero)
            {
                var source = Imaging.CreateBitmapSourceFromHIcon(iconHandle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                source.Freeze();
                return source;
            }
        }
        catch { }

        return null;
    }

    public bool IsMinimized => NativeMethods.IsIconic(Handle);

    public void Activate()
    {
        if (IsMinimized)
            NativeMethods.ShowWindow(Handle, NativeConstants.SW_RESTORE);

        // Simulate an Alt key press/release to bypass SetForegroundWindow restrictions.
        // This is the most reliable method and avoids the flicker that
        // AttachThreadInput + BringWindowToTop causes.
        NativeMethods.keybd_event((byte)NativeConstants.VK_MENU, 0, 0, 0);
        NativeMethods.SetForegroundWindow(Handle);
        NativeMethods.keybd_event((byte)NativeConstants.VK_MENU, 0, NativeConstants.KEYEVENTF_KEYUP, 0);
    }

    public void Close()
    {
        NativeMethods.PostMessage(Handle, NativeConstants.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
    }
}
