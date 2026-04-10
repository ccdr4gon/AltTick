using System.Diagnostics;
using System.Runtime.InteropServices;
using AltTick.Interop;

namespace AltTick.Services;

internal enum HookState
{
    Idle,
    AltHeld,
    Cycling,
}

internal class CycleEventArgs : EventArgs
{
    public bool Reverse { get; }
    public CycleEventArgs(bool reverse) => Reverse = reverse;
}

internal sealed class KeyboardHookService : IDisposable
{
    private IntPtr _hookId = IntPtr.Zero;
    private readonly NativeMethods.LowLevelKeyboardProc _proc;
    private HookState _state = HookState.Idle;
    private bool _backtickPressedDuringAlt;
    private bool _backtickHeld;

    public event EventHandler? CycleStarted;
    public event EventHandler<CycleEventArgs>? CycleNext;
    public event EventHandler? CycleCommitted;
    public event EventHandler? CycleCancelled;

    public HookState State => _state;

    public KeyboardHookService()
    {
        _proc = HookCallback;
    }

    public void Install()
    {
        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule!;
        _hookId = NativeMethods.SetWindowsHookEx(
            NativeConstants.WH_KEYBOARD_LL,
            _proc,
            NativeMethods.GetModuleHandle(curModule.ModuleName),
            0);

        if (_hookId == IntPtr.Zero)
            throw new InvalidOperationException($"Failed to install keyboard hook. Error: {Marshal.GetLastWin32Error()}");
    }

    public void Uninstall()
    {
        if (_hookId != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var hookStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            int msg = wParam.ToInt32();
            bool isKeyDown = msg == NativeConstants.WM_KEYDOWN || msg == NativeConstants.WM_SYSKEYDOWN;
            bool isKeyUp = msg == NativeConstants.WM_KEYUP || msg == NativeConstants.WM_SYSKEYUP;
            int vk = hookStruct.vkCode;

            bool handled = ProcessKey(vk, isKeyDown, isKeyUp);
            if (handled)
                return (IntPtr)1; // suppress
        }

        return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private bool ProcessKey(int vk, bool isKeyDown, bool isKeyUp)
    {
        switch (_state)
        {
            case HookState.Idle:
                if (IsAltKey(vk) && isKeyDown)
                {
                    _state = HookState.AltHeld;
                    _backtickPressedDuringAlt = false;
                    _backtickHeld = false;
                    return false; // don't suppress Alt itself
                }
                break;

            case HookState.AltHeld:
                if (IsAltKey(vk) && isKeyUp)
                {
                    _state = HookState.Idle;
                    return false;
                }
                if (vk == NativeConstants.VK_OEM_3 && isKeyDown)
                {
                    _backtickPressedDuringAlt = true;
                    _backtickHeld = true;
                    _state = HookState.Cycling;
                    // ShowWithWindows already selects index 1 (the next window),
                    // so don't fire CycleNext on the first backtick press.
                    CycleStarted?.Invoke(this, EventArgs.Empty);
                    return true; // suppress backtick
                }
                if (!IsAltKey(vk) && !IsShiftKey(vk) && vk != NativeConstants.VK_OEM_3)
                {
                    // Non-backtick key pressed during Alt hold - not our combo
                    _state = HookState.Idle;
                    return false;
                }
                break;

            case HookState.Cycling:
                if (IsAltKey(vk) && isKeyUp)
                {
                    _state = HookState.Idle;
                    if (_backtickPressedDuringAlt)
                    {
                        CycleCommitted?.Invoke(this, EventArgs.Empty);
                        return true; // suppress Alt release to prevent system menu
                    }
                    return false;
                }
                if (vk == NativeConstants.VK_OEM_3 && isKeyUp)
                {
                    _backtickHeld = false;
                    return true; // suppress backtick release
                }
                if (vk == NativeConstants.VK_OEM_3 && isKeyDown)
                {
                    if (_backtickHeld)
                        return true; // suppress key-repeat while held down

                    _backtickHeld = true;
                    bool shift = IsShiftDown();
                    CycleNext?.Invoke(this, new CycleEventArgs(shift));
                    return true; // suppress backtick
                }
                if (vk == NativeConstants.VK_ESCAPE && isKeyDown)
                {
                    _state = HookState.Idle;
                    _backtickPressedDuringAlt = false;
                    CycleCancelled?.Invoke(this, EventArgs.Empty);
                    return true; // suppress Escape
                }
                // Allow Shift through
                if (IsShiftKey(vk))
                    return false;
                break;
        }

        return false;
    }

    private static bool IsAltKey(int vk) =>
        vk == NativeConstants.VK_MENU || vk == NativeConstants.VK_LMENU || vk == NativeConstants.VK_RMENU;

    private static bool IsShiftKey(int vk) =>
        vk == NativeConstants.VK_SHIFT || vk == NativeConstants.VK_LSHIFT || vk == NativeConstants.VK_RSHIFT;

    private static bool IsShiftDown() =>
        (NativeMethods.GetAsyncKeyState(NativeConstants.VK_SHIFT) & 0x8000) != 0;

    public void Dispose()
    {
        Uninstall();
    }
}
