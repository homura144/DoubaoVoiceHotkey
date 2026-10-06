using System.ComponentModel;
using System.Runtime.InteropServices;

namespace DoubaoVoiceHotkey;

internal sealed class GlobalInputHook : IDisposable
{
    private readonly NativeMethods.HookProc _keyboardCallback;
    private readonly NativeMethods.HookProc _mouseCallback;
    private readonly HashSet<int> _keysDown = new();
    private readonly HashSet<int> _mouseDown = new();
    private IntPtr _keyboardHook;
    private IntPtr _mouseHook;
    private bool _disposed;

    internal Func<InputSignal, bool>? InputReceived { get; set; }

    internal GlobalInputHook()
    {
        _keyboardCallback = KeyboardHook;
        _mouseCallback = MouseHook;

        var module = NativeMethods.GetModuleHandle(null);
        _keyboardHook = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _keyboardCallback, module, 0);
        if (_keyboardHook == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to install keyboard hook.");

        _mouseHook = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, _mouseCallback, module, 0);
        if (_mouseHook == IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = IntPtr.Zero;
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to install mouse hook.");
        }
    }

    private IntPtr KeyboardHook(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code == NativeMethods.HC_ACTION)
        {
            var message = wParam.ToInt32();
            var isDown = message is NativeMethods.WM_KEYDOWN or NativeMethods.WM_SYSKEYDOWN;
            var isUp = message is NativeMethods.WM_KEYUP or NativeMethods.WM_SYSKEYUP;
            if (isDown || isUp)
            {
                var data = Marshal.PtrToStructure<NativeMethods.KbdLlHookStruct>(lParam);
                var vk = unchecked((int)data.vkCode);
                var repeat = false;
                if (isDown)
                {
                    repeat = !_keysDown.Add(vk);
                }
                else
                {
                    _keysDown.Remove(vk);
                }

                var consumed = InputReceived?.Invoke(
                    new InputSignal(InputBindingKind.Keyboard, vk, isDown, repeat)) == true;
                if (consumed)
                    return new IntPtr(1);
            }
        }

        return NativeMethods.CallNextHookEx(_keyboardHook, code, wParam, lParam);
    }

    private IntPtr MouseHook(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code == NativeMethods.HC_ACTION)
        {
            var message = wParam.ToInt32();
            var isDown = message == NativeMethods.WM_XBUTTONDOWN;
            var isUp = message == NativeMethods.WM_XBUTTONUP;
            if (isDown || isUp)
            {
                var data = Marshal.PtrToStructure<NativeMethods.MsLlHookStruct>(lParam);
                var button = unchecked((int)((data.mouseData >> 16) & 0xFFFF));
                var repeat = false;
                if (isDown)
                    repeat = !_mouseDown.Add(button);
                else
                    _mouseDown.Remove(button);

                var consumed = InputReceived?.Invoke(
                    new InputSignal(InputBindingKind.MouseXButton, button, isDown, repeat)) == true;
                if (consumed)
                    return new IntPtr(1);
            }
        }

        return NativeMethods.CallNextHookEx(_mouseHook, code, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        if (_keyboardHook != IntPtr.Zero)
            NativeMethods.UnhookWindowsHookEx(_keyboardHook);
        if (_mouseHook != IntPtr.Zero)
            NativeMethods.UnhookWindowsHookEx(_mouseHook);

        _keyboardHook = IntPtr.Zero;
        _mouseHook = IntPtr.Zero;
    }
}
