namespace RexHides.Win32Native;

internal static class Win32Constants
{
    // ShowWindow
    public const int SW_HIDE = 0;
    public const int SW_SHOW = 5;
    public const int SW_RESTORE = 9;

    // Window messages
    public const int WM_CLOSE = 0x0010;
    public const int WM_GETICON = 0x007F;
    public const int WM_HOTKEY = 0x0312;

    // WM_GETICON parameters
    public const int ICON_SMALL = 0;
    public const int ICON_BIG = 1;
    public const int ICON_SMALL2 = 2;

    // GetClassLongPtr indices
    public const int GCLP_HICON = -14;
    public const int GCLP_HICONSM = -34;

    // GetWindowLongPtr indices
    public const int GWL_STYLE = -16;
    public const int GWL_EXSTYLE = -20;

    // Window styles
    public const long WS_CAPTION = 0x00C00000;

    // Extended window styles
    public const long WS_EX_TOOLWINDOW = 0x00000080;
    public const long WS_EX_NOACTIVATE = 0x08000000;

    // Hotkey modifiers
    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;
    public const uint MOD_NOREPEAT = 0x4000;

    // WinEvents
    public const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    public const uint EVENT_SYSTEM_MOVESIZESTART = 0x000A;
    public const uint EVENT_SYSTEM_MOVESIZEEND = 0x000B;
    public const uint EVENT_SYSTEM_MINIMIZESTART = 0x0016;
    public const uint EVENT_SYSTEM_MINIMIZEEND = 0x0017;
    public const uint EVENT_OBJECT_CREATE = 0x8000;
    public const uint EVENT_OBJECT_DESTROY = 0x8001;
    public const uint EVENT_OBJECT_SHOW = 0x8002;
    public const uint EVENT_OBJECT_HIDE = 0x8003;
    public const uint EVENT_OBJECT_LOCATIONCHANGE = 0x800B;

    public const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    public const uint WINEVENT_SKIPOWNPROCESS = 0x0002;

    public const int OBJID_WINDOW = 0;
    public const int CHILDID_SELF = 0;

    // GetAncestor
    public const uint GA_ROOT = 2;

    // SendMessageTimeout
    public const uint SMTO_ABORTIFHUNG = 0x0002;

    // OpenProcess
    public const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
    public const uint PROCESS_VM_READ = 0x0010;

    // NtQueryInformationProcess (PROCESSINFOCLASS)
    public const int ProcessCommandLineInformation = 60;

    // DwmGetWindowAttribute
    public const uint DWMWA_CLOAKED = 14;
    public const uint DWMWA_EXTENDED_FRAME_BOUNDS = 9;

    // SetWindowPos
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_SHOWWINDOW = 0x0040;
    public static readonly IntPtr HWND_TOPMOST = new(-1);
    public static readonly IntPtr HWND_MESSAGE = new(-3);

    // SHGetFileInfo
    public const uint SHGFI_ICON = 0x000000100;
    public const uint SHGFI_SMALLICON = 0x000000001;
}
