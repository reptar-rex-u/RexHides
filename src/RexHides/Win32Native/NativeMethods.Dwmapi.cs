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

    [DllImport("dwmapi.dll")]
    public static extern int DwmSetWindowAttribute(
        IntPtr hwnd, uint dwAttribute, ref int pvAttribute, int cbAttribute);

    /// <summary>
    /// Asks DWM for a dark caption bar in the given colors (COLORREF, 0x00BBGGRR). The explicit
    /// caption color also overrides "show accent color on title bars". Unsupported attributes are
    /// silently ignored on older Windows builds.
    /// </summary>
    public static void UseDarkTitleBar(IntPtr hwnd, int captionColor, int textColor)
    {
        int enabled = 1;
        DwmSetWindowAttribute(hwnd, Win32Constants.DWMWA_USE_IMMERSIVE_DARK_MODE, ref enabled, sizeof(int));
        DwmSetWindowAttribute(hwnd, Win32Constants.DWMWA_CAPTION_COLOR, ref captionColor, sizeof(int));
        DwmSetWindowAttribute(hwnd, Win32Constants.DWMWA_TEXT_COLOR, ref textColor, sizeof(int));
    }

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
