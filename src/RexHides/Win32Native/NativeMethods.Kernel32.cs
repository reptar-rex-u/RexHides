using System.Runtime.InteropServices;
using System.Text;

namespace RexHides.Win32Native;

internal static partial class NativeMethods
{
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern bool QueryFullProcessImageName(
        IntPtr hProcess, uint dwFlags, StringBuilder lpExeName, ref uint lpdwSize);

    [DllImport("kernel32.dll")]
    public static extern bool CloseHandle(IntPtr hObject);

    /// <summary>
    /// Returns the full executable path for a process, or null if the process
    /// cannot be opened (e.g. it is elevated and we are not).
    /// </summary>
    public static string? GetProcessExePath(uint pid)
    {
        IntPtr hProcess = OpenProcess(Win32Constants.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (hProcess == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var sb = new StringBuilder(1024);
            uint size = (uint)sb.Capacity;
            return QueryFullProcessImageName(hProcess, 0, sb, ref size) ? sb.ToString() : null;
        }
        finally
        {
            CloseHandle(hProcess);
        }
    }
}
