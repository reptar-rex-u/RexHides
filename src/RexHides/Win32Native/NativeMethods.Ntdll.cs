using System.Runtime.InteropServices;

namespace RexHides.Win32Native;

[StructLayout(LayoutKind.Sequential)]
internal struct UnicodeString
{
    public ushort Length;
    public ushort MaximumLength;
    public IntPtr Buffer;
}

internal static partial class NativeMethods
{
    [DllImport("ntdll.dll")]
    public static extern int NtQueryInformationProcess(
        IntPtr processHandle,
        int processInformationClass,
        ref UnicodeString processInformation,
        uint processInformationLength,
        out uint returnLength);
}
