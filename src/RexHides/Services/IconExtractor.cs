using System.Drawing;
using System.IO;
using RexHides.Win32Native;

namespace RexHides.Services;

/// <summary>
/// Resolves a managed Icon for a window using a fallback chain:
/// WM_GETICON -> window class icon -> icon embedded in the exe -> shell file icon
/// -> generic application icon. All returned icons are fully owned managed clones,
/// so foreign HICON handles vanishing later cannot break the tray.
/// </summary>
public static class IconExtractor
{
    public static Icon GetIconForWindow(IntPtr hwnd, string exePath)
    {
        return TryFromWindowMessage(hwnd)
            ?? TryFromWindowClass(hwnd)
            ?? TryFromExecutable(exePath)
            ?? TryFromShell(exePath)
            ?? (Icon)SystemIcons.Application.Clone();
    }

    private static Icon? TryFromWindowMessage(IntPtr hwnd)
    {
        foreach (int size in new[] { Win32Constants.ICON_SMALL2, Win32Constants.ICON_SMALL, Win32Constants.ICON_BIG })
        {
            NativeMethods.SendMessageTimeout(
                hwnd, Win32Constants.WM_GETICON, new IntPtr(size), IntPtr.Zero,
                Win32Constants.SMTO_ABORTIFHUNG, 300, out IntPtr hIcon);
            Icon? icon = CloneForeignIcon(hIcon);
            if (icon != null)
            {
                return icon;
            }
        }

        return null;
    }

    private static Icon? TryFromWindowClass(IntPtr hwnd)
    {
        foreach (int index in new[] { Win32Constants.GCLP_HICONSM, Win32Constants.GCLP_HICON })
        {
            Icon? icon = CloneForeignIcon(NativeMethods.GetClassLongPtr(hwnd, index));
            if (icon != null)
            {
                return icon;
            }
        }

        return null;
    }

    private static Icon? TryFromExecutable(string exePath)
    {
        if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
        {
            return null;
        }

        var small = new IntPtr[1];
        NativeMethods.ExtractIconEx(exePath, 0, null, small, 1);
        return CloneOwnedIcon(small[0]);
    }

    private static Icon? TryFromShell(string exePath)
    {
        if (string.IsNullOrEmpty(exePath))
        {
            return null;
        }

        var info = default(SHFILEINFO);
        NativeMethods.SHGetFileInfo(
            exePath, 0, ref info, (uint)System.Runtime.InteropServices.Marshal.SizeOf<SHFILEINFO>(),
            Win32Constants.SHGFI_ICON | Win32Constants.SHGFI_SMALLICON);
        return CloneOwnedIcon(info.hIcon);
    }

    /// <summary>Clones an HICON we do NOT own (copies it first, then destroys only the copy).</summary>
    private static Icon? CloneForeignIcon(IntPtr hIcon)
    {
        if (hIcon == IntPtr.Zero)
        {
            return null;
        }

        return CloneOwnedIcon(NativeMethods.CopyIcon(hIcon));
    }

    /// <summary>Clones an HICON we own into a managed Icon and destroys the native handle.</summary>
    private static Icon? CloneOwnedIcon(IntPtr hIcon)
    {
        if (hIcon == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            using Icon temp = Icon.FromHandle(hIcon);
            return (Icon)temp.Clone();
        }
        catch (Exception ex) when (ex is ArgumentException or System.Runtime.InteropServices.ExternalException)
        {
            return null;
        }
        finally
        {
            NativeMethods.DestroyIcon(hIcon);
        }
    }
}
