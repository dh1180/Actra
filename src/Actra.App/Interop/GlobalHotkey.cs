using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Actra.Interop;

public sealed class GlobalHotkey : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const uint ModControl = 0x0002;
    private const uint ModNoRepeat = 0x4000;
    private const uint VkSpace = 0x20;
    private const int HotkeyId = 0xA17A;

    private readonly IntPtr _windowHandle;
    private readonly HwndSource _source;
    private readonly Action _callback;
    private bool _registered;

    public GlobalHotkey(Window window, Action callback)
    {
        _callback = callback;

        _windowHandle = new WindowInteropHelper(window).Handle;
        _source = HwndSource.FromHwnd(_windowHandle)
                  ?? throw new InvalidOperationException("Window handle을 가져오지 못했습니다.");

        _source.AddHook(WndProc);

        _registered = RegisterHotKey(
            _windowHandle,
            HotkeyId,
            ModControl | ModNoRepeat,
            VkSpace);
    }

    public bool IsRegistered => _registered;

    private IntPtr WndProc(
        IntPtr hwnd,
        int msg,
        IntPtr wParam,
        IntPtr lParam,
        ref bool handled)
    {
        if (msg == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            handled = true;
            _callback();
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_registered)
        {
            UnregisterHotKey(_windowHandle, HotkeyId);
            _registered = false;
        }

        _source.RemoveHook(WndProc);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(
        IntPtr hWnd,
        int id,
        uint fsModifiers,
        uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
