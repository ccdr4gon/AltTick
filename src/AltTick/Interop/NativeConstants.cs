namespace AltTick.Interop;

internal static class NativeConstants
{
    // Keyboard Hook
    public const int WH_KEYBOARD_LL = 13;
    public const int WM_KEYDOWN = 0x0100;
    public const int WM_KEYUP = 0x0101;
    public const int WM_SYSKEYDOWN = 0x0104;
    public const int WM_SYSKEYUP = 0x0105;

    // Virtual Key Codes
    public const int VK_MENU = 0x12;       // Alt key
    public const int VK_LMENU = 0xA4;      // Left Alt
    public const int VK_RMENU = 0xA5;      // Right Alt
    public const int VK_OEM_3 = 0xC0;      // ` / ~ key
    public const int VK_ESCAPE = 0x1B;
    public const int VK_SHIFT = 0x10;
    public const int VK_LSHIFT = 0xA0;
    public const int VK_RSHIFT = 0xA1;
    public const int VK_TAB = 0x09;

    // KBDLLHOOKSTRUCT flags
    public const int LLKHF_ALTDOWN = 0x20;
    public const int LLKHF_UP = 0x80;
    public const int LLKHF_EXTENDED = 0x01;

    // Window Styles
    public const int GWL_EXSTYLE = -20;
    public const int GWL_STYLE = -16;
    public const uint WS_EX_TOOLWINDOW = 0x00000080;
    public const uint WS_EX_APPWINDOW = 0x00040000;
    public const uint WS_EX_NOACTIVATE = 0x08000000;
    public const uint WS_EX_TRANSPARENT = 0x00000020;
    public const uint WS_VISIBLE = 0x10000000;
    public const uint WS_EX_LAYERED = 0x00080000;

    // Window Relationship
    public const int GW_OWNER = 4;

    // Window Messages
    public const int WM_GETICON = 0x007F;
    public const int ICON_SMALL = 0;
    public const int ICON_BIG = 1;
    public const int ICON_SMALL2 = 2;

    // GetClassLongPtr
    public const int GCLP_HICON = -14;
    public const int GCLP_HICONSM = -34;

    // DWM Thumbnail
    public const int DWM_TNP_RECTDESTINATION = 0x00000001;
    public const int DWM_TNP_RECTSOURCE = 0x00000002;
    public const int DWM_TNP_OPACITY = 0x00000004;
    public const int DWM_TNP_VISIBLE = 0x00000008;
    public const int DWM_TNP_SOURCECLIENTAREAONLY = 0x00000010;

    // DWM Window Attribute
    public const int DWMWA_CLOAKED = 14;
    public const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;

    // ShowWindow
    public const int SW_RESTORE = 9;
    public const int SW_SHOW = 5;
    public const int SW_MINIMIZE = 6;
    public const int SW_SHOWNOACTIVATE = 4;

    // SetWindowPos flags
    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_SHOWWINDOW = 0x0040;

    // HWND constants
    public static readonly IntPtr HWND_TOPMOST = new(-1);

    // Monitor
    public const int MONITOR_DEFAULTTONEAREST = 2;

    // DWM Margins
    public const int DWM_MARGINS_ALL_MINUS_ONE = -1;

    // Keyboard event
    public const int KEYEVENTF_KEYUP = 0x0002;
}
