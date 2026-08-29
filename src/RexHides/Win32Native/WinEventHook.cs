namespace RexHides.Win32Native;

/// <summary>
/// Safe wrapper around SetWinEventHook. Keeps the callback delegate alive for the
/// lifetime of the hook (otherwise the GC collects it and the process crashes).
/// Must be created on a thread with a message pump (the WPF UI thread).
/// </summary>
internal sealed class WinEventHook : IDisposable
{
    private readonly WinEventDelegate _callback;
    private IntPtr _hookHandle;

    public WinEventHook(uint eventMin, uint eventMax, WinEventDelegate callback, uint idProcess = 0)
    {
        _callback = callback;
        _hookHandle = NativeMethods.SetWinEventHook(
            eventMin, eventMax, IntPtr.Zero, _callback,
            idProcess, 0,
            Win32Constants.WINEVENT_OUTOFCONTEXT | Win32Constants.WINEVENT_SKIPOWNPROCESS);
    }

    public bool IsActive => _hookHandle != IntPtr.Zero;

    public void Dispose()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            NativeMethods.UnhookWinEvent(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }
    }
}
