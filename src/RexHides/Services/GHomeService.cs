using System.Windows.Threading;
using RexHides.Features;
using RexHides.Models;
using RexHides.Win32Native;

namespace RexHides.Services;

/// <summary>
/// Opt-in persistent-tray mode for the Google Home Brave PWA. Uses the normal
/// hide/restore journal for crash safety while keeping a dedicated tray icon alive.
/// </summary>
public class GHomeService : IGHomeModule
{
    private readonly WindowHidingService _hiding;
    private readonly SettingsService _settings;
    private readonly Dispatcher _dispatcher;
    private readonly DispatcherTimer _pollTimer;
    private IntPtr _trackedHwnd;
    private string _statusText = "G Home is disabled.";

    public GHomeService(WindowHidingService hiding, SettingsService settings)
    {
        _hiding = hiding;
        _settings = settings;
        _dispatcher = Dispatcher.CurrentDispatcher;

        _pollTimer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher)
        {
            Interval = TimeSpan.FromSeconds(1),
        };
        _pollTimer.Tick += (_, _) => RefreshConnection();

        _hiding.WindowHidden += OnWindowHidden;
        _hiding.WindowRestored += OnWindowRestored;
    }

    public event Action? StateChanged;

    public GHomeProfile Profile => _settings.Current.GHome;

    public bool IsEnabled => Profile.Enabled && Profile.HasIdentity;

    public bool IsOverlayActive => IsEnabled && Profile.ShowOverlayButton;

    public bool IsConnected => _trackedHwnd != IntPtr.Zero && NativeMethods.IsWindow(_trackedHwnd);

    public bool IsHidden =>
        IsConnected && _hiding.HiddenWindows.Any(entry => entry.Handle == _trackedHwnd);

    public string StatusText => _statusText;

    public IntPtr TrackedHwnd => _trackedHwnd;

    public void Initialize()
    {
        MigrateLegacyIdentity();

        if (Profile.Enabled)
        {
            if (!Profile.HasIdentity)
            {
                SetStatus("G Home is enabled but no window identity is saved. Identify the Google Home window first.");
                Profile.Enabled = false;
                _settings.Save();
                return;
            }

            StartPolling();
            RefreshConnection();
            SetStatus(IsConnected
                ? $"Tracking {NativeMethods.GetWindowTitle(_trackedHwnd)}."
                : "Waiting for the Google Home window to appear.");
        }
    }

    public void SetEnabled(bool enabled)
    {
        if (enabled && !Profile.HasIdentity)
        {
            SetStatus("Identify the Google Home window before enabling G Home mode.");
            return;
        }

        Profile.Enabled = enabled;
        _settings.Save();

        if (enabled)
        {
            MigrateLegacyIdentity();
            StartPolling();
            RefreshConnection();
            SetStatus(IsConnected
                ? $"G Home enabled for {NativeMethods.GetWindowTitle(_trackedHwnd)}."
                : "G Home enabled. Waiting for the Google Home window.");
        }
        else
        {
            StopPolling();
            _trackedHwnd = IntPtr.Zero;
            SetStatus("G Home is disabled.");
        }

        RaiseStateChanged();
    }

    public void SetShowOverlayButton(bool enabled)
    {
        Profile.ShowOverlayButton = enabled;
        _settings.Save();
        RaiseStateChanged();
    }

    public bool TryCaptureFromForeground(out string message)
    {
        IntPtr foreground = ResolveRoot(NativeMethods.GetForegroundWindow());
        NativeMethods.GetWindowThreadProcessId(foreground, out uint foregroundPid);
        bool rexHidesFocused = foregroundPid == (uint)Environment.ProcessId;

        if (!rexHidesFocused &&
            foreground != IntPtr.Zero &&
            GHomeIdentityMatcher.LooksLikeGoogleHomeWindow(foreground))
        {
            return TryCaptureWindow(foreground, out message);
        }

        List<IntPtr> candidates = GHomeIdentityMatcher.FindCandidateWindows();
        if (candidates.Count == 1)
        {
            return TryCaptureWindow(candidates[0], out message);
        }

        if (candidates.Count > 1)
        {
            message = "Multiple Google Home windows were found. Focus the correct Brave PWA and try again.";
            return false;
        }

        string title = foreground != IntPtr.Zero ? NativeMethods.GetWindowTitle(foreground) : "(none)";
        message = rexHidesFocused
            ? "RexHides is focused. Leave Google Home open (even in the tray) and click Find Google Home again."
            : $"No Google Home Brave PWA found (focused: \"{title}\"). Open Google Home in Brave first.";
        return false;
    }

    private bool TryCaptureWindow(IntPtr hwnd, out string message)
    {
        hwnd = ResolveRoot(hwnd);
        if (hwnd == IntPtr.Zero || !GHomeIdentityMatcher.LooksLikeGoogleHomeWindow(hwnd))
        {
            message = "That window is not the Google Home Brave PWA.";
            return false;
        }

        NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
        string? exePath = NativeMethods.GetProcessExePath(pid);
        if (!GHomeIdentityMatcher.IsBraveProcess(exePath))
        {
            message = "Google Home must be running in Brave.";
            return false;
        }

        string title = NativeMethods.GetWindowTitle(hwnd);
        GHomeProfile captured = GHomeIdentityMatcher.CaptureIdentity(hwnd);
        Profile.ExePath = captured.ExePath;
        Profile.WindowClassName = captured.WindowClassName;
        Profile.AppUrlSignature = captured.AppUrlSignature;
        Profile.TitleSignature = captured.TitleSignature;
        _trackedHwnd = hwnd;
        _settings.Save();

        message = $"Saved G Home identity for \"{title}\" ({Profile.AppUrlSignature}).";
        SetStatus(message);
        RaiseStateChanged();
        return true;
    }

    public bool IsGHomeWindow(IntPtr hwnd)
    {
        hwnd = ResolveRoot(hwnd);
        if (!IsEnabled || hwnd == IntPtr.Zero)
        {
            return false;
        }

        return GHomeIdentityMatcher.MatchesProfile(Profile, hwnd);
    }

    public void Toggle()
    {
        if (!EnsureTrackedWindow())
        {
            return;
        }

        if (IsHidden)
        {
            Show();
        }
        else
        {
            Hide();
        }
    }

    public void Show()
    {
        if (!EnsureTrackedWindow())
        {
            return;
        }

        if (IsHidden)
        {
            _hiding.Restore(_trackedHwnd);
            SetStatus($"Showing {NativeMethods.GetWindowTitle(_trackedHwnd)}.");
        }
        else if (NativeMethods.IsWindow(_trackedHwnd))
        {
            WindowHidingService.TryMakeVisible(_trackedHwnd);
            SetStatus($"Brought {NativeMethods.GetWindowTitle(_trackedHwnd)} to the front.");
        }

        RaiseStateChanged();
    }

    public void Hide()
    {
        if (!EnsureTrackedWindow())
        {
            return;
        }

        if (IsHidden)
        {
            return;
        }

        HideResult result = _hiding.Hide(_trackedHwnd);
        SetStatus(result switch
        {
            HideResult.Hidden => $"Hidden {NativeMethods.GetWindowTitle(_trackedHwnd)} to the G Home tray icon.",
            HideResult.AccessDenied => "Could not hide Google Home. Restart RexHides as administrator if Brave is elevated.",
            HideResult.AlreadyHidden => "Google Home is already hidden.",
            _ => "Google Home cannot be hidden right now.",
        });
        RaiseStateChanged();
    }

    public void CloseWindow()
    {
        if (_trackedHwnd == IntPtr.Zero || !NativeMethods.IsWindow(_trackedHwnd))
        {
            RefreshConnection();
            if (_trackedHwnd == IntPtr.Zero)
            {
                SetStatus("Google Home is not open.");
                RaiseStateChanged();
                return;
            }
        }

        if (IsHidden)
        {
            _hiding.CloseWindow(_trackedHwnd);
        }
        else
        {
            NativeMethods.PostMessage(_trackedHwnd, Win32Constants.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
        }

        _trackedHwnd = IntPtr.Zero;
        SetStatus("Closed the Google Home window.");
        RaiseStateChanged();
    }

    public void Dispose()
    {
        StopPolling();
        _pollTimer.Stop();
    }

    private void StartPolling()
    {
        if (!_pollTimer.IsEnabled)
        {
            _pollTimer.Start();
        }
    }

    private void StopPolling()
    {
        _pollTimer.Stop();
    }

    private void RefreshConnection()
    {
        if (!Profile.HasIdentity)
        {
            _trackedHwnd = IntPtr.Zero;
            return;
        }

        IntPtr previous = _trackedHwnd;
        _trackedHwnd = FindMatchingWindow();

        if (previous != _trackedHwnd)
        {
            if (_trackedHwnd == IntPtr.Zero)
            {
                SetStatus("Google Home is not open. The tray icon stays ready.");
            }
            else
            {
                SetStatus($"Connected to {NativeMethods.GetWindowTitle(_trackedHwnd)}.");
            }

            RaiseStateChanged();
        }
    }

    private IntPtr FindMatchingWindow()
    {
        IntPtr visibleMatch = IntPtr.Zero;
        IntPtr hiddenMatch = IntPtr.Zero;

        NativeMethods.EnumWindows(
            (hwnd, _) =>
            {
                if (!GHomeIdentityMatcher.MatchesProfile(Profile, hwnd))
                {
                    return true;
                }

                if (_hiding.IsEligible(hwnd))
                {
                    visibleMatch = hwnd;
                    return false;
                }

                if (IsKnownHiddenWindow(hwnd))
                {
                    hiddenMatch = hwnd;
                }

                return true;
            },
            IntPtr.Zero);

        return visibleMatch != IntPtr.Zero ? visibleMatch : hiddenMatch;
    }

    private bool IsKnownHiddenWindow(IntPtr hwnd) =>
        _hiding.HiddenWindows.Any(entry => entry.Handle == hwnd);

    private bool EnsureTrackedWindow()
    {
        RefreshConnection();
        if (_trackedHwnd != IntPtr.Zero)
        {
            return true;
        }

        SetStatus("Google Home is not open. Launch the Brave PWA, then try again.");
        RaiseStateChanged();
        return false;
    }

    private void OnWindowHidden(HiddenWindowEntry entry)
    {
        if (!IsGHomeWindow(entry.Handle))
        {
            return;
        }

        _trackedHwnd = entry.Handle;
        SetStatus($"Google Home is hidden. Use the G Home tray icon to show it again.");
        RaiseStateChanged();
    }

    private void OnWindowRestored(HiddenWindowEntry entry)
    {
        if (!IsGHomeWindow(entry.Handle))
        {
            return;
        }

        _trackedHwnd = entry.Handle;
        SetStatus($"Google Home is visible again.");
        RaiseStateChanged();
    }

    private static IntPtr ResolveRoot(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        IntPtr root = NativeMethods.GetAncestor(hwnd, Win32Constants.GA_ROOT);
        return root == IntPtr.Zero ? hwnd : root;
    }

    private void SetStatus(string status)
    {
        _statusText = status;
    }

    private void RaiseStateChanged() => StateChanged?.Invoke();

    private void MigrateLegacyIdentity()
    {
        bool changed = false;

        if (Profile.HasIdentity && string.IsNullOrWhiteSpace(Profile.AppUrlSignature))
        {
            Profile.AppUrlSignature = GHomeIdentityMatcher.DefaultAppHost;
            changed = true;
        }

        if (Profile.HasIdentity && string.IsNullOrWhiteSpace(Profile.TitleSignature))
        {
            Profile.TitleSignature = "Google Home";
            changed = true;
        }

        if (changed)
        {
            _settings.Save();
        }
    }
}
