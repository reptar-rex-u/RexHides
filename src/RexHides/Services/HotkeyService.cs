using System.Windows.Interop;
using RexHides.Models;
using RexHides.Win32Native;

namespace RexHides.Services;

/// <summary>
/// Global hotkey via RegisterHotKey against an invisible message-only window.
/// (No low-level WH_KEYBOARD_LL hook needed; RegisterHotKey is the right primitive
/// and is not flagged by antivirus software.)
/// </summary>
public class HotkeyService : IDisposable
{
    private const int HotkeyId = 0x5248; // "RH"

    private readonly HwndSource _source;
    private bool _registered;

    public HotkeyService()
    {
        var parameters = new HwndSourceParameters("RexHidesHotkeyWindow")
        {
            ParentWindow = Win32Constants.HWND_MESSAGE,
            WindowStyle = 0,
            Width = 0,
            Height = 0,
        };
        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);
    }

    public event EventHandler? HotkeyPressed;

    /// <summary>Registers the hotkey, replacing any previous one. False on conflict.</summary>
    public bool Register(HotkeyDefinition hotkey)
    {
        Unregister();
        _registered = NativeMethods.RegisterHotKey(
            _source.Handle, HotkeyId,
            hotkey.Modifiers | Win32Constants.MOD_NOREPEAT, hotkey.VirtualKey);
        return _registered;
    }

    public void Unregister()
    {
        if (_registered)
        {
            NativeMethods.UnregisterHotKey(_source.Handle, HotkeyId);
            _registered = false;
        }
    }

    public void Dispose()
    {
        Unregister();
        _source.RemoveHook(WndProc);
        _source.Dispose();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Win32Constants.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
            handled = true;
        }

        return IntPtr.Zero;
    }
}
