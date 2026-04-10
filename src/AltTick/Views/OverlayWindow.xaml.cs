using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using AltTick.Interop;
using AltTick.Models;

namespace AltTick.Views;

public partial class OverlayWindow : Window
{
    private readonly List<IntPtr> _thumbnailIds = [];
    private readonly List<Border> _thumbnailBorders = [];
    private List<AppWindow> _windows = [];
    private int _selectedIndex;
    private IntPtr _overlayHwnd;

    public event EventHandler? WindowClicked;

    private const int ThumbWidth = 240;
    private const int ThumbHeight = 160;
    private const int ThumbSpacing = 12;
    private const int MaxColumns = 6;

    public OverlayWindow()
    {
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _overlayHwnd = new WindowInteropHelper(this).Handle;

        // Make the window not steal focus and click-through for focus
        int exStyle = NativeMethods.GetWindowLong(_overlayHwnd, NativeConstants.GWL_EXSTYLE);
        exStyle |= (int)NativeConstants.WS_EX_TOOLWINDOW; // hide from Alt+Tab
        NativeMethods.SetWindowLong(_overlayHwnd, NativeConstants.GWL_EXSTYLE, exStyle);
    }

    public void ShowWithWindows(List<AppWindow> windows)
    {
        if (windows.Count <= 1)
            return;

        _windows = windows;
        _selectedIndex = 1; // Start at second window (first is the current foreground)

        UpdateAppHeader();
        CreateThumbnailSlots();

        // Position before showing using raw monitor info (no DPI conversion needed pre-show)
        PositionOnScreen();

        Show();
        UpdateLayout(); // Force layout so TranslatePoint works for DWM thumbnail positioning
        RegisterDwmThumbnails();
        UpdateSelection();

        var fadeIn = (Storyboard)FindResource("FadeIn");
        fadeIn.Begin();
    }

    public void CycleSelection(bool reverse)
    {
        if (_windows.Count == 0) return;

        if (reverse)
            _selectedIndex = (_selectedIndex - 1 + _windows.Count) % _windows.Count;
        else
            _selectedIndex = (_selectedIndex + 1) % _windows.Count;

        UpdateSelection();
    }

    public AppWindow? GetSelectedWindow()
    {
        if (_selectedIndex >= 0 && _selectedIndex < _windows.Count)
            return _windows[_selectedIndex];
        return null;
    }

    public void HideOverlay(Action? onComplete = null)
    {
        var fadeOut = (Storyboard)FindResource("FadeOut");
        fadeOut.Completed += (_, _) =>
        {
            UnregisterDwmThumbnails();
            Hide();
            onComplete?.Invoke();
        };
        fadeOut.Begin();
    }

    public void HideImmediately()
    {
        UnregisterDwmThumbnails();
        Hide();
    }

    private void UpdateAppHeader()
    {
        if (_windows.Count > 0)
        {
            AppNameText.Text = _windows[0].ProcessName ?? "Unknown";
            AppIcon.Source = _windows[0].Icon;
        }
    }

    private void CreateThumbnailSlots()
    {
        ThumbnailContainer.Items.Clear();
        _thumbnailBorders.Clear();

        foreach (var win in _windows)
        {
            int index = _thumbnailBorders.Count; // capture for closure
            var border = new Border
            {
                Width = ThumbWidth + 8,
                Height = ThumbHeight + 40,
                Margin = new Thickness(ThumbSpacing / 2.0),
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
                BorderThickness = new Thickness(2),
                BorderBrush = Brushes.Transparent,
                SnapsToDevicePixels = true,
                Cursor = Cursors.Hand,
            };

            border.MouseEnter += (_, _) =>
            {
                _selectedIndex = index;
                UpdateSelection();
            };

            border.MouseLeftButtonDown += (_, _) =>
            {
                _selectedIndex = index;
                WindowClicked?.Invoke(this, EventArgs.Empty);
            };

            var stack = new StackPanel();

            // Placeholder for DWM thumbnail (the DWM renders directly over this area)
            var thumbPlaceholder = new Border
            {
                Width = ThumbWidth,
                Height = ThumbHeight,
                Margin = new Thickness(4, 4, 4, 4),
                Background = new SolidColorBrush(Color.FromArgb(30, 0, 0, 0)),
                CornerRadius = new CornerRadius(4),
            };
            stack.Children.Add(thumbPlaceholder);

            // Window title
            var title = new TextBlock
            {
                Text = TruncateTitle(win.Title, 30),
                Foreground = Brushes.White,
                FontSize = 12,
                TextAlignment = TextAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(4, 2, 4, 4),
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            stack.Children.Add(title);

            border.Child = stack;
            ThumbnailContainer.Items.Add(border);
            _thumbnailBorders.Add(border);
        }
    }

    private void UpdateSelection()
    {
        for (int i = 0; i < _thumbnailBorders.Count; i++)
        {
            _thumbnailBorders[i].BorderBrush = i == _selectedIndex
                ? new SolidColorBrush(Color.FromRgb(0, 120, 212)) // Windows accent blue
                : Brushes.Transparent;
            _thumbnailBorders[i].Background = i == _selectedIndex
                ? new SolidColorBrush(Color.FromArgb(100, 0, 120, 212))
                : new SolidColorBrush(Color.FromArgb(60, 255, 255, 255));
        }
    }

    private void PositionOnScreen()
    {
        int count = _windows.Count;
        int cols = Math.Min(count, MaxColumns);
        int rows = (int)Math.Ceiling((double)count / MaxColumns);

        double totalWidth = cols * (ThumbWidth + 8 + ThumbSpacing) + 40;
        double totalHeight = rows * (ThumbHeight + 40 + ThumbSpacing) + 80;

        Width = totalWidth;
        Height = totalHeight;

        // Center on the monitor of the foreground window
        IntPtr foreground = _windows[0].Handle;
        IntPtr monitor = NativeMethods.MonitorFromWindow(foreground, NativeConstants.MONITOR_DEFAULTTONEAREST);
        var monInfo = new MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFO>() };
        NativeMethods.GetMonitorInfo(monitor, ref monInfo);

        // Get DPI scale - try PresentationSource first, fall back to GetDpiForWindow
        double dpiScale;
        var source = PresentationSource.FromVisual(this);
        if (source?.CompositionTarget != null)
        {
            dpiScale = source.CompositionTarget.TransformFromDevice.M11;
        }
        else
        {
            uint dpi = NativeMethods.GetDpiForWindow(_overlayHwnd != IntPtr.Zero ? _overlayHwnd : foreground);
            dpiScale = dpi > 0 ? 96.0 / dpi : 1.0;
        }

        double monX = monInfo.rcWork.Left * dpiScale;
        double monY = monInfo.rcWork.Top * dpiScale;
        double monW = monInfo.rcWork.Width * dpiScale;
        double monH = monInfo.rcWork.Height * dpiScale;

        Left = monX + (monW - totalWidth) / 2;
        Top = monY + (monH - totalHeight) / 2;
    }

    private void RegisterDwmThumbnails()
    {
        UnregisterDwmThumbnails();

        if (_overlayHwnd == IntPtr.Zero)
            return;

        for (int i = 0; i < _windows.Count; i++)
        {
            int hr = NativeMethods.DwmRegisterThumbnail(_overlayHwnd, _windows[i].Handle, out IntPtr thumbId);
            if (hr != 0)
            {
                _thumbnailIds.Add(IntPtr.Zero);
                continue;
            }

            _thumbnailIds.Add(thumbId);

            // Get source size
            NativeMethods.DwmQueryThumbnailSourceSize(thumbId, out var srcSize);

            // Calculate destination rect (in device pixels)
            var border = _thumbnailBorders[i];
            var pos = border.TranslatePoint(new Point(4, 4), this);

            // Get DPI scale factor
            var source = PresentationSource.FromVisual(this);
            double dpiScaleX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
            double dpiScaleY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;

            // Scale to fit within ThumbWidth x ThumbHeight maintaining aspect ratio
            double scaleX = (double)ThumbWidth / srcSize.cx;
            double scaleY = (double)ThumbHeight / srcSize.cy;
            double scale = Math.Min(scaleX, scaleY);

            int destW = (int)(srcSize.cx * scale);
            int destH = (int)(srcSize.cy * scale);
            int offsetX = (ThumbWidth - destW) / 2;
            int offsetY = (ThumbHeight - destH) / 2;

            int left = (int)((pos.X + 4 + offsetX) * dpiScaleX);
            int top = (int)((pos.Y + 4 + offsetY) * dpiScaleY);

            var props = new DWM_THUMBNAIL_PROPERTIES
            {
                dwFlags = NativeConstants.DWM_TNP_VISIBLE | NativeConstants.DWM_TNP_RECTDESTINATION | NativeConstants.DWM_TNP_OPACITY,
                fVisible = true,
                opacity = 255,
                rcDestination = new RECT
                {
                    Left = left,
                    Top = top,
                    Right = left + (int)(destW * dpiScaleX),
                    Bottom = top + (int)(destH * dpiScaleY),
                },
            };

            NativeMethods.DwmUpdateThumbnailProperties(thumbId, ref props);
        }
    }

    private void UnregisterDwmThumbnails()
    {
        foreach (var thumbId in _thumbnailIds)
        {
            if (thumbId != IntPtr.Zero)
                NativeMethods.DwmUnregisterThumbnail(thumbId);
        }
        _thumbnailIds.Clear();
    }

    private static string TruncateTitle(string title, int maxLength)
    {
        if (title.Length <= maxLength)
            return title;
        return title[..(maxLength - 3)] + "...";
    }
}
