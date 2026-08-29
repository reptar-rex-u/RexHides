using System.Runtime.InteropServices;

namespace RexHides.Win32Native;

internal static partial class NativeMethods
{
    [DllImport("dwmapi.dll")]
    public static extern int DwmGetWindowAttribute(
        IntPtr hwnd, uint dwAttribute, out RECT pvAttribute, int cbAttribute);

    [DllImport("dwmapi.dll")]
    public static extern int DwmGetWindowAttribute(
        IntPtr hwnd, uint dwAttribute, out int pvAttribute, int cbAttribute);

    /// <summary>True if the window is DWM-cloaked (e.g. a suspended UWP frame).</summary>
    public static bool IsWindowCloaked(IntPtr hwnd)
    {
        int hr = DwmGetWindowAttribute(hwnd, Win32Constants.DWMWA_CLOAKED, out int cloaked, sizeof(int));
        return hr == 0 && cloaked != 0;
    }

    /// <summary>Visible frame bounds in physical pixels; falls back to GetWindowRect.</summary>
    public static RECT GetExtendedFrameBounds(IntPtr hwnd)
    {
        int hr = DwmGetWindowAttribute(
            hwnd, Win32Constants.DWMWA_EXTENDED_FRAME_BOUNDS,
            out RECT rect, Marshal.SizeOf<RECT>());
        if (hr != 0)
        {
            GetWindowRect(hwnd, out rect);
        }

        return rect;
    }
}
