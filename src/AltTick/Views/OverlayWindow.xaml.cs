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
    private readonly List<Border> _thumbnailPlaceholders = [];
    private List<AppWindow> _windows = [];
    private int _selectedIndex;
    private int _hoverIndex = -1;
    private bool _isRemoving;
    private IntPtr _overlayHwnd;

    public event EventHandler? WindowClicked;
    public event EventHandler<IntPtr>? WindowCloseRequested;

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
        if (windows.Count == 0)
            return;

        // Clear any leftover animation holds on Width
        BeginAnimation(WidthProperty, null);

        _windows = windows;
        _selectedIndex = windows.Count > 1 ? 1 : 0;
        _hoverIndex = -1;

        UpdateAppHeader();
        CreateThumbnailSlots();

        // Position before showing using raw monitor info (no DPI conversion needed pre-show)
        PositionOnScreen();

        RootPanel.Opacity = 1;
        Show();
        UpdateLayout();
        RegisterDwmThumbnails();
        UpdateSelection();
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

    public List<AppWindow> GetWindows() => _windows;

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
        RootPanel.Opacity = 0;
        Hide();
    }

    public void RemoveWindowAt(int index, Action<List<AppWindow>>? onComplete = null)
    {
        if (_isRemoving || index < 0 || index >= _windows.Count) return;
        _isRemoving = true;

        // Unregister all DWM thumbnails before animating
        UnregisterDwmThumbnails();

        var removedBorder = _thumbnailBorders[index];

        // Animate only the removed card: width + margin collapse to 0
        var widthAnim = new DoubleAnimation
        {
            To = 0,
            Duration = TimeSpan.FromMilliseconds(150),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut },
            FillBehavior = FillBehavior.Stop,
        };

        widthAnim.Completed += (_, _) =>
        {
            // Clear animation hold
            removedBorder.BeginAnimation(WidthProperty, null);

            // Remove from lists
            _windows.RemoveAt(index);
            _thumbnailBorders.RemoveAt(index);
            _thumbnailPlaceholders.RemoveAt(index);
            ThumbnailContainer.Items.RemoveAt(index);

            // Fix selected index
            if (_windows.Count == 0)
            {
                _selectedIndex = -1;
            }
            else if (_selectedIndex >= _windows.Count)
            {
                _selectedIndex = _windows.Count - 1;
            }

            // Resize overlay directly (no animation) and re-register thumbnails
            if (_windows.Count > 0)
            {
                int cols = Math.Min(_windows.Count, MaxColumns);
                int rows = (int)Math.Ceiling((double)_windows.Count / MaxColumns);
                Width = cols * (ThumbWidth + 8 + ThumbSpacing) + 40;
                Height = rows * (ThumbHeight + 58 + ThumbSpacing) + 80;

                UpdateLayout();
                RegisterDwmThumbnails();
                UpdateSelection();
            }

            _isRemoving = false;
            onComplete?.Invoke(_windows);
        };

        // Also collapse margin so remaining items slide together
        removedBorder.Margin = new Thickness(0);
        removedBorder.BeginAnimation(WidthProperty, widthAnim);
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
        _thumbnailPlaceholders.Clear();

        foreach (var win in _windows)
        {
            int index = _thumbnailBorders.Count; // capture for closure
            var border = new Border
            {
                Width = ThumbWidth + 8,
                Height = ThumbHeight + 58, // +22 for close button area, +36 for title+padding
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
                _hoverIndex = index;
                UpdateSelection();
            };

            border.MouseLeave += (_, _) =>
            {
                _hoverIndex = -1;
                UpdateSelection();
            };

            border.MouseLeftButtonDown += (_, e) =>
            {
                _selectedIndex = index;
                WindowClicked?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
            };

            // Use Grid so close button can overlay on top-right
            var grid = new Grid();

            var stack = new StackPanel();

            // Placeholder for DWM thumbnail (top margin leaves room for close button)
            var thumbPlaceholder = new Border
            {
                Height = ThumbHeight,
                Margin = new Thickness(4, 22, 4, 4),
                Background = new SolidColorBrush(Color.FromArgb(30, 0, 0, 0)),
                CornerRadius = new CornerRadius(4),
            };
            stack.Children.Add(thumbPlaceholder);
            _thumbnailPlaceholders.Add(thumbPlaceholder);

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
            grid.Children.Add(stack);

            // Close button (top-right)
            var closeBtn = new Border
            {
                Width = 20,
                Height = 20,
                CornerRadius = new CornerRadius(10),
                Background = new SolidColorBrush(Color.FromArgb(0, 200, 50, 50)),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 2, 2, 0),
                Cursor = Cursors.Hand,
                Child = new TextBlock
                {
                    Text = "\u00D7",
                    Foreground = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)),
                    FontSize = 14,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, -2, 0, 0),
                },
            };

            closeBtn.MouseEnter += (_, _) =>
            {
                closeBtn.Background = new SolidColorBrush(Color.FromRgb(200, 50, 50));
                ((TextBlock)closeBtn.Child).Foreground = Brushes.White;
            };
            closeBtn.MouseLeave += (_, _) =>
            {
                closeBtn.Background = new SolidColorBrush(Color.FromArgb(0, 200, 50, 50));
                ((TextBlock)closeBtn.Child).Foreground = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255));
            };

            var capturedHandle = win.Handle;
            closeBtn.MouseLeftButtonDown += (_, e) =>
            {
                e.Handled = true;
                WindowCloseRequested?.Invoke(this, capturedHandle);
            };

            grid.Children.Add(closeBtn);
            border.Child = grid;
            ThumbnailContainer.Items.Add(border);
            _thumbnailBorders.Add(border);
        }
    }

    private void UpdateSelection()
    {
        for (int i = 0; i < _thumbnailBorders.Count; i++)
        {
            bool isSelected = i == _selectedIndex;
            bool isHovered = i == _hoverIndex;

            if (isSelected)
            {
                // Keyboard selection: accent blue border + tinted background
                _thumbnailBorders[i].BorderBrush = new SolidColorBrush(Color.FromRgb(0, 120, 212));
                _thumbnailBorders[i].Background = new SolidColorBrush(Color.FromArgb(100, 0, 120, 212));
            }
            else if (isHovered)
            {
                // Mouse hover: subtle lighten, no border
                _thumbnailBorders[i].BorderBrush = Brushes.Transparent;
                _thumbnailBorders[i].Background = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255));
            }
            else
            {
                _thumbnailBorders[i].BorderBrush = Brushes.Transparent;
                _thumbnailBorders[i].Background = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255));
            }
        }
    }

    private void PositionOnScreen()
    {
        int count = _windows.Count;
        int cols = Math.Min(count, MaxColumns);
        int rows = (int)Math.Ceiling((double)count / MaxColumns);

        double totalWidth = cols * (ThumbWidth + 8 + ThumbSpacing) + 40;
        double totalHeight = rows * (ThumbHeight + 58 + ThumbSpacing) + 80;

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

    private void RegisterDwmThumbnails(byte opacity = 255)
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

            // Get exact placeholder position from the element itself
            var placeholder = _thumbnailPlaceholders[i];
            var pos = placeholder.TranslatePoint(new Point(0, 0), this);

            var source = PresentationSource.FromVisual(this);
            double dpiScaleX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
            double dpiScaleY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;

            // Placeholder rect in device pixels
            int areaW = (int)Math.Round(placeholder.ActualWidth * dpiScaleX);
            int areaH = (int)Math.Round(placeholder.ActualHeight * dpiScaleY);
            int areaLeft = (int)Math.Round(pos.X * dpiScaleX);
            int areaTop = (int)Math.Round(pos.Y * dpiScaleY);

            // Scale source to fit within placeholder, maintaining aspect ratio
            double scaleX = (double)areaW / srcSize.cx;
            double scaleY = (double)areaH / srcSize.cy;
            double scale = Math.Min(scaleX, scaleY);
            if (scale > 1) scale = 1;

            int destW = (int)Math.Round(srcSize.cx * scale);
            int destH = (int)Math.Round(srcSize.cy * scale);

            // Clamp to not exceed placeholder area
            destW = Math.Min(destW, areaW);
            destH = Math.Min(destH, areaH);

            int left = areaLeft + (areaW - destW) / 2;
            int top = areaTop + (areaH - destH) / 2;

            var props = new DWM_THUMBNAIL_PROPERTIES
            {
                dwFlags = NativeConstants.DWM_TNP_VISIBLE | NativeConstants.DWM_TNP_RECTDESTINATION | NativeConstants.DWM_TNP_OPACITY,
                fVisible = true,
                opacity = opacity,
                rcDestination = new RECT
                {
                    Left = left,
                    Top = top,
                    Right = left + destW,
                    Bottom = top + destH,
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
