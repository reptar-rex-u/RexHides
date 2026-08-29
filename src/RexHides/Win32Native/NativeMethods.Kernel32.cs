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

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool ReadProcessMemory(
        IntPtr hProcess,
        IntPtr lpBaseAddress,
        byte[] lpBuffer,
        int dwSize,
        out int lpNumberOfBytesRead);

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

    /// <summary>
    /// Returns the command line for a process, or null if the process cannot be
    /// opened or its command line cannot be read (e.g. elevation/UIPI).
    /// </summary>
    public static string? GetProcessCommandLine(uint pid)
    {
        uint access = Win32Constants.PROCESS_QUERY_LIMITED_INFORMATION | Win32Constants.PROCESS_VM_READ;
        IntPtr hProcess = OpenProcess(access, false, pid);
        if (hProcess == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            UnicodeString cmdLine = default;
            int status = NtQueryInformationProcess(
                hProcess,
                Win32Constants.ProcessCommandLineInformation,
                ref cmdLine,
                (uint)Marshal.SizeOf<UnicodeString>(),
                out _);

            if (status < 0 || cmdLine.Buffer == IntPtr.Zero || cmdLine.Length == 0)
            {
                return null;
            }

            byte[] buffer = new byte[cmdLine.Length];
            if (!ReadProcessMemory(hProcess, cmdLine.Buffer, buffer, buffer.Length, out _))
            {
                return null;
            }

            return Encoding.Unicode.GetString(buffer);
        }
        finally
        {
            CloseHandle(hProcess);
        }
    }
}
