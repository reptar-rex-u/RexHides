using System.IO;
using RexHides.Models;
using RexHides.Win32Native;

namespace RexHides.Services;

public enum HideResult
{
    Hidden,
    NotEligible,
    AlreadyHidden,
    AccessDenied,
}

/// <summary>
/// Owns the map of hidden windows. All hide/restore paths (hotkey, tray, rules,
/// overlay button, UI) funnel through this service. UI-thread affine.
/// </summary>
public class WindowHidingService
{
    private static readonly string[] BlockedClasses =
    [
        "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "Progman", "WorkerW", "NotifyIconOverflowWindow",
    ];

    private readonly Dictionary<IntPtr, HiddenWindowEntry> _hidden = [];
    private readonly CrashRecoveryService _recovery;

    public WindowHidingService(CrashRecoveryService recovery)
    {
        _recovery = recovery;
    }

    public event Action<HiddenWindowEntry>? WindowHidden;

    public event Action<HiddenWindowEntry>? WindowRestored;

    /// <summary>Raised when hiding failed, most likely because the target is elevated.</summary>
    public event Action<string>? AccessDenied;

    /// <summary>Raised when a restore left the window invisible or DWM-cloaked.</summary>
    public event Action<string>? RestoreFailed;

    public IReadOnlyCollection<HiddenWindowEntry> HiddenWindows => _hidden.Values;

    public HideResult HideForegroundWindow() => Hide(NativeMethods.GetForegroundWindow());

    public HideResult Hide(IntPtr hwnd)
    {
        if (_hidden.ContainsKey(hwnd))
        {
            return HideResult.AlreadyHidden;
        }

        if (!IsEligible(hwnd))
        {
            return HideResult.NotEligible;
        }

        NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
        string? exePath = NativeMethods.GetProcessExePath(pid);
        string title = NativeMethods.GetWindowTitle(hwnd);

        NativeMethods.ShowWindow(hwnd, Win32Constants.SW_HIDE);

        // UIPI silently blocks hiding windows of higher-integrity (elevated) processes.
        if (NativeMethods.IsWindowVisible(hwnd))
        {
            AccessDenied?.Invoke(
                $"Could not hide \"{title}\". It is probably running as administrator - " +
                "restart RexHides as administrator to manage elevated windows.");
            return HideResult.AccessDenied;
        }

        var entry = new HiddenWindowEntry
        {
            Hwnd = hwnd.ToInt64(),
            Title = string.IsNullOrWhiteSpace(title) ? "(untitled window)" : title,
            ExePath = exePath ?? string.Empty,
            ProcessName = exePath is null ? string.Empty : Path.GetFileName(exePath),
            Pid = pid,
            HiddenAtUtc = DateTime.UtcNow,
        };

        _hidden[hwnd] = entry;
        _recovery.Update(_hidden.Values);
        WindowHidden?.Invoke(entry);
        return HideResult.Hidden;
    }

    public void Restore(IntPtr hwnd)
    {
        if (!_hidden.TryGetValue(hwnd, out HiddenWindowEntry? entry))
        {
            return;
        }

        if (!NativeMethods.IsWindow(hwnd))
        {
            _hidden.Remove(hwnd);
            _recovery.Update(_hidden.Values);
            WindowRestored?.Invoke(entry);
            return;
        }

        if (!TryMakeVisible(hwnd))
        {
            RestoreFailed?.Invoke(
                $"Could not restore \"{entry.Title}\". The app may have been suspended while hidden - try opening it from the Start menu.");
            return;
        }

        _hidden.Remove(hwnd);
        _recovery.Update(_hidden.Values);
        WindowRestored?.Invoke(entry);
    }

    /// <summary>
    /// Makes a previously hidden window visible again, including UWP frames that
    /// stay DWM-cloaked until they are activated. Returns false if the window is
    /// gone or still invisible/cloaked afterwards.
    /// </summary>
    public static bool TryMakeVisible(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !NativeMethods.IsWindow(hwnd))
        {
            return false;
        }

        ActivateWindow(hwnd);
        if (IsShown(hwnd))
        {
            return true;
        }

        // ApplicationFrameHost often stays cloaked until the packaged app is forced to resume.
        for (int i = 0; i < 8; i++)
        {
            NativeMethods.ShowWindow(hwnd, Win32Constants.SW_RESTORE);
            ActivateWindow(hwnd);
            Thread.Sleep(50);
            if (IsShown(hwnd))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsShown(IntPtr hwnd) =>
        NativeMethods.IsWindow(hwnd) &&
        NativeMethods.IsWindowVisible(hwnd) &&
        !NativeMethods.IsWindowCloaked(hwnd);

    private static void ActivateWindow(IntPtr hwnd)
    {
        NativeMethods.ShowWindow(hwnd, Win32Constants.SW_SHOW);
        if (NativeMethods.IsIconic(hwnd))
        {
            NativeMethods.ShowWindow(hwnd, Win32Constants.SW_RESTORE);
        }

        IntPtr foreground = NativeMethods.GetForegroundWindow();
        uint fgThread = NativeMethods.GetWindowThreadProcessId(foreground, out _);
        uint targetThread = NativeMethods.GetWindowThreadProcessId(hwnd, out _);
        bool attached = false;
        if (fgThread != 0 && targetThread != 0 && fgThread != targetThread)
        {
            attached = NativeMethods.AttachThreadInput(fgThread, targetThread, true);
        }

        try
        {
            NativeMethods.BringWindowToTop(hwnd);
            NativeMethods.SetForegroundWindow(hwnd);
        }
        finally
        {
            if (attached)
            {
                NativeMethods.AttachThreadInput(fgThread, targetThread, false);
            }
        }
    }

    public void RestoreAll()
    {
        foreach (IntPtr hwnd in _hidden.Keys.ToList())
        {
            Restore(hwnd);
        }
    }

    /// <summary>Politely closes a hidden window (WM_CLOSE) and drops its entry.</summary>
    public void CloseWindow(IntPtr hwnd)
    {
        if (_hidden.Remove(hwnd, out HiddenWindowEntry? entry))
        {
            if (NativeMethods.IsWindow(hwnd))
            {
                NativeMethods.PostMessage(hwnd, Win32Constants.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
            }

            _recovery.Update(_hidden.Values);
            WindowRestored?.Invoke(entry);
        }
    }

    /// <summary>Drops entries whose window no longer exists (process exited while hidden).</summary>
    public void PruneDeadWindows()
    {
        foreach (IntPtr hwnd in _hidden.Keys.Where(h => !NativeMethods.IsWindow(h)).ToList())
        {
            _hidden.Remove(hwnd, out HiddenWindowEntry? entry);
            if (entry != null)
            {
                WindowRestored?.Invoke(entry);
            }
        }

        _recovery.Update(_hidden.Values);
    }

    public bool IsEligible(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero ||
            !NativeMethods.IsWindow(hwnd) ||
            !NativeMethods.IsWindowVisible(hwnd) ||
            NativeMethods.IsWindowCloaked(hwnd))
        {
            return false;
        }

        // Top-level windows only.
        if (NativeMethods.GetAncestor(hwnd, Win32Constants.GA_ROOT) != hwnd)
        {
            return false;
        }

        // Never hide our own windows.
        NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == (uint)Environment.ProcessId)
        {
            return false;
        }

        // Never hide the desktop or taskbar.
        if (hwnd == NativeMethods.GetShellWindow() ||
            BlockedClasses.Contains(NativeMethods.GetWindowClassName(hwnd)))
        {
            return false;
        }

        return true;
    }
}
