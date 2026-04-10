using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows;
using AltTick.Helpers;
using AltTick.Models;
using AltTick.Services;
using AltTick.Views;
using H.NotifyIcon;

namespace AltTick;

public partial class App : Application
{
    private SingleInstanceGuard? _guard;
    private KeyboardHookService? _hookService;
    private SettingsService? _settingsService;
    private OverlayWindow? _overlay;
    private TaskbarIcon? _trayIcon;
    private IntPtr _originalForeground;
    private bool _cycleActive;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _guard = new SingleInstanceGuard("AltTick_SingleInstance");
        if (!_guard.TryAcquire())
        {
            MessageBox.Show("AltTick is already running.", "AltTick", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        _settingsService = new SettingsService();
        _settingsService.Load();

        _overlay = new OverlayWindow();
        _overlay.WindowClicked += OnWindowClicked;
        _overlay.WindowCloseRequested += OnWindowCloseRequested;

        SetupTrayIcon();
        SetupKeyboardHook();
    }

    private void SetupKeyboardHook()
    {
        _hookService = new KeyboardHookService();
        _hookService.CycleStarted += OnCycleStarted;
        _hookService.CycleNext += OnCycleNext;
        _hookService.CycleCommitted += OnCycleCommitted;
        _hookService.CycleCancelled += OnCycleCancelled;
        _hookService.Install();
    }

    private void SetupTrayIcon()
    {
        var contextMenu = new System.Windows.Controls.ContextMenu();

        var startupItem = new System.Windows.Controls.MenuItem
        {
            Header = "Run at Startup",
            IsCheckable = true,
            IsChecked = _settingsService!.IsRunAtStartup(),
        };
        startupItem.Click += (_, _) =>
        {
            bool newValue = startupItem.IsChecked;
            _settingsService.SetRunAtStartup(newValue);
        };

        var aboutItem = new System.Windows.Controls.MenuItem { Header = "About AltTick" };
        aboutItem.Click += (_, _) =>
        {
            MessageBox.Show(
                "AltTick v1.0\n\nmacOS-style Alt+` window switcher for Windows.\n\nHold Alt and press ` to cycle between windows of the same application.\n\nBy ccdr4gon\nhttps://github.com/ccdr4gon/AltTick",
                "About AltTick",
                MessageBoxButton.OK,
                MessageBoxImage.None);
        };

        var exitItem = new System.Windows.Controls.MenuItem { Header = "Exit" };
        exitItem.Click += (_, _) => Shutdown();

        contextMenu.Items.Add(startupItem);
        contextMenu.Items.Add(new System.Windows.Controls.Separator());
        contextMenu.Items.Add(aboutItem);
        contextMenu.Items.Add(exitItem);

        _trayIcon = new TaskbarIcon
        {
            Icon = CreateTrayIcon(),
            ToolTipText = "AltTick - Alt+` Window Switcher",
        };
        _trayIcon.ForceCreate();

        // Ensure overlay has a Win32 HWND (without showing it) for SetForegroundWindow.
        var overlayHwnd = new System.Windows.Interop.WindowInteropHelper(_overlay!);
        overlayHwnd.EnsureHandle();

        _trayIcon.TrayRightMouseUp += (_, _) =>
        {
            // SetForegroundWindow gives the popup focus ownership,
            // so clicking outside will dismiss the menu.
            Interop.NativeMethods.SetForegroundWindow(overlayHwnd.Handle);
            contextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
            contextMenu.IsOpen = true;
        };
    }

    private void OnCycleStarted(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            _originalForeground = Interop.NativeMethods.GetForegroundWindow();
            var windows = WindowEnumerationService.GetWindowsForSameApp(_originalForeground);

            if (windows.Count == 0)
            {
                _cycleActive = false;
                return;
            }

            _cycleActive = true;
            _overlay?.ShowWithWindows(windows);
        });
    }

    private void OnCycleNext(object? sender, CycleEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (_cycleActive)
                _overlay?.CycleSelection(e.Reverse);
        });
    }

    private void OnCycleCommitted(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (!_cycleActive) return;
            _cycleActive = false;
            var selectedWindow = _overlay?.GetSelectedWindow();
            selectedWindow?.Activate();
            _overlay?.HideImmediately();
        });
    }

    private void OnCycleCancelled(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            _cycleActive = false;
            _overlay?.HideImmediately();
        });
    }

    private void OnWindowClicked(object? sender, EventArgs e)
    {
        var selectedWindow = _overlay?.GetSelectedWindow();
        selectedWindow?.Activate();
        _overlay?.HideImmediately();
    }

    private void OnWindowCloseRequested(object? sender, IntPtr windowHandle)
    {
        if (_overlay == null) return;

        var windows = _overlay.GetWindows();
        int windowIndex = windows.FindIndex(w => w.Handle == windowHandle);
        if (windowIndex < 0) return;

        windows[windowIndex].Close();

        _overlay.RemoveWindowAt(windowIndex, remaining =>
        {
            if (remaining.Count == 0)
            {
                _cycleActive = false;
                _overlay.HideImmediately();
            }
        });
    }

    private static System.Drawing.Icon CreateTrayIcon()
    {
        using var bmp = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // Back window: gray body + lighter gray title bar (20% lighter) + gray border
        var backBody = System.Drawing.Color.FromArgb(255, 140, 140, 140);
        var backHeader = System.Drawing.Color.FromArgb(255, 168, 168, 168);
        using var penBack = new Pen(System.Drawing.Color.FromArgb(255, 140, 140, 140), 1.5f);
        using var fillBackBody = new SolidBrush(backBody);
        using var fillBackHeader = new SolidBrush(backHeader);

        // Front window: 10% darker than back, opaque + white title bar + white border
        var frontBody = System.Drawing.Color.FromArgb(255, 115, 115, 115);
        var frontHeader = System.Drawing.Color.White;
        using var penFront = new Pen(System.Drawing.Color.White, 1.5f);
        using var fillFrontBody = new SolidBrush(frontBody);
        using var fillFrontHeader = new SolidBrush(frontHeader);

        // Back window (only top and right edges peek out)
        var backRect = new RectangleF(6, 2, 24, 20);
        g.FillRectangle(fillBackBody, backRect);
        g.FillRectangle(fillBackHeader, backRect.X, backRect.Y, backRect.Width, 5);
        g.DrawRectangle(penBack, backRect.X, backRect.Y, backRect.Width, backRect.Height);

        // Front window (opaque, covers back window body)
        var frontRect = new RectangleF(2, 8, 24, 20);
        g.FillRectangle(fillFrontBody, frontRect);
        g.FillRectangle(fillFrontHeader, frontRect.X, frontRect.Y, frontRect.Width, 5);
        g.DrawRectangle(penFront, frontRect.X, frontRect.Y, frontRect.Width, frontRect.Height);

        return System.Drawing.Icon.FromHandle(bmp.GetHicon());
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hookService?.Dispose();
        _trayIcon?.Dispose();
        _guard?.Dispose();
        base.OnExit(e);
    }
}
