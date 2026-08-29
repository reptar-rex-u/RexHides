using System.Diagnostics;
using System.Windows.Interop;
using System.Windows.Threading;
using RexHides.Features;
using RexHides.Views;
using RexHides.Win32Native;

namespace RexHides.Services;

/// <summary>
/// Stretch feature: floats a "minimize to tray" button over the active window's
/// title bar, just left of its caption buttons. Implemented as a non-activating
/// overlay window tracked with WinEvent hooks - no cross-process code injection.
/// </summary>
public class TitleBarOverlayService : IDisposable
{
    private readonly WindowHidingService _hiding;
    private readonly IGHomeModule _gHome;
    private readonly Dispatcher _dispatcher;

    private WinEventHook? _foregroundHook;
    private WinEventHook? _locationHook;
    private WinEventHook? _stateHook;
    private DispatcherTimer? _pollTimer;
    private TitleBarOverlayWindow? _overlay;
    private IntPtr _target;
    private bool _generalEnabled;
    private bool _trackingActive;

    public TitleBarOverlayService(WindowHidingService hiding, IGHomeModule gHome)
    {
        _hiding = hiding;
        _gHome = gHome;
        _dispatcher = Dispatcher.CurrentDispatcher;
        _hiding.WindowHidden += _ => Marshal(HideOverlay);
#if GHOME
        _gHome.StateChanged += () => Marshal(RefreshTrackingState);
#endif
    }

    public void SetEnabled(bool enabled)
    {
        _generalEnabled = enabled;
        RefreshTrackingState();
    }

    public void RefreshGHomeOverlay() => RefreshTrackingState();

    /// <summary>
    /// Re-evaluate the current foreground window. Called when the settings window
    /// deactivates and on the poll timer so a missed WinEvent cannot leave the
    /// overlay stuck hidden.
    /// </summary>
    public void RefreshFromForeground()
    {
        if (!_trackingActive)
        {
            return;
        }

        OnForegroundChanged(NativeMethods.GetForegroundWindow());
    }

    public void Dispose()
    {
        StopTracking();
        _overlay?.Close();
        _overlay = null;
    }

    private void RefreshTrackingState()
    {
        bool shouldTrack = _generalEnabled;
#if GHOME
        shouldTrack |= _gHome.IsOverlayActive;
#endif
        if (shouldTrack == _trackingActive)
        {
            if (shouldTrack)
            {
                RefreshFromForeground();
            }

            return;
        }

        _trackingActive = shouldTrack;
        if (shouldTrack)
        {
            StartTracking();
        }
        else
        {
            StopTracking();
        }
    }

    private void StartTracking()
    {
        _foregroundHook = new WinEventHook(
            Win32Constants.EVENT_SYSTEM_FOREGROUND, Win32Constants.EVENT_SYSTEM_FOREGROUND,
            (_, _, hwnd, idObject, _, _, _) =>
            {
                if (idObject == Win32Constants.OBJID_WINDOW)
                {
                    Marshal(() => OnForegroundChanged(hwnd));
                }
            });
        Debug.WriteLine($"RexHides overlay: foreground hook active={_foregroundHook.IsActive}");

        _pollTimer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(500),
        };
        _pollTimer.Tick += (_, _) => RefreshFromForeground();
        _pollTimer.Start();
        RefreshFromForeground();
    }

    private void StopTracking()
    {
        _pollTimer?.Stop();
        _pollTimer = null;
        HideOverlay();
        _foregroundHook?.Dispose();
        _foregroundHook = null;
        _trackingActive = false;
    }

    private void Marshal(Action action)
    {
        if (_dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            _dispatcher.BeginInvoke(action);
        }
    }

    private void OnForegroundChanged(IntPtr hwnd)
    {
        hwnd = ResolveRoot(hwnd);
        if (!IsOverlayTarget(hwnd))
        {
            Debug.WriteLine($"RexHides overlay: skip hwnd=0x{hwnd:X} (not a suitable target)");
            HideOverlay();
            return;
        }

        if (hwnd != _target)
        {
            _target = hwnd;
            RehookTarget(hwnd);
            Debug.WriteLine($"RexHides overlay: tracking \"{NativeMethods.GetWindowTitle(hwnd)}\" hwnd=0x{hwnd:X}");
        }

        UpdatePosition();
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

    private bool IsOverlayTarget(IntPtr hwnd)
    {
#if GHOME
        if (_gHome.IsOverlayActive && _gHome.IsGHomeWindow(hwnd))
        {
            return IsCaptionWindow(hwnd);
        }
#endif

        if (!_generalEnabled)
        {
            return false;
        }

        return _hiding.IsEligible(hwnd) && IsCaptionWindow(hwnd);
    }

    private static bool IsCaptionWindow(IntPtr hwnd)
    {
        long style = NativeMethods.GetWindowLongPtr(hwnd, Win32Constants.GWL_STYLE).ToInt64();
        return (style & Win32Constants.WS_CAPTION) == Win32Constants.WS_CAPTION;
    }

    private void RehookTarget(IntPtr hwnd)
    {
        _locationHook?.Dispose();
        _stateHook?.Dispose();

        NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);

        _locationHook = new WinEventHook(
            Win32Constants.EVENT_OBJECT_LOCATIONCHANGE, Win32Constants.EVENT_OBJECT_LOCATIONCHANGE,
            (_, _, h, idObject, _, _, _) =>
            {
                if (h == _target && idObject == Win32Constants.OBJID_WINDOW)
                {
                    Marshal(UpdatePosition);
                }
            },
            idProcess: pid);

        _stateHook = new WinEventHook(
            Win32Constants.EVENT_SYSTEM_MOVESIZESTART, Win32Constants.EVENT_SYSTEM_MINIMIZEEND,
            (_, eventType, h, _, _, _, _) =>
            {
                if (h != _target)
                {
                    return;
                }

                Marshal(() =>
                {
                    if (eventType is Win32Constants.EVENT_SYSTEM_MOVESIZESTART
                        or Win32Constants.EVENT_SYSTEM_MINIMIZESTART)
                    {
                        _overlay?.Hide();
                    }
                    else
                    {
                        UpdatePosition();
                    }
                });
            },
            idProcess: pid);
    }

    private void UpdatePosition()
    {
        IntPtr target = _target;
        if (target == IntPtr.Zero || !NativeMethods.IsWindow(target) ||
            !NativeMethods.IsWindowVisible(target) || NativeMethods.IsIconic(target))
        {
            HideOverlay();
            return;
        }

        RECT frame = NativeMethods.GetExtendedFrameBounds(target);
        uint dpi = NativeMethods.GetDpiForWindow(target);
        double scale = dpi == 0 ? 1.0 : dpi / 96.0;

        int width = Math.Max((int)(28 * scale), 16);
        int height = Math.Max((int)(22 * scale), 14);

        int captionButtons = (int)(150 * scale);
        int x = frame.Right - captionButtons - width;
        int y = frame.Top + (int)(4 * scale);

        int maxX = frame.Right - width - (int)(4 * scale);
        int minX = frame.Left + (int)(4 * scale);
        if (maxX < minX)
        {
            maxX = minX;
        }

        x = Math.Clamp(x, minX, maxX);

        if (frame.Width < width || frame.Height < height)
        {
            HideOverlay();
            return;
        }

        EnsureOverlay();
        var helper = new WindowInteropHelper(_overlay!);
        helper.EnsureHandle();
        if (helper.Handle == IntPtr.Zero)
        {
            Debug.WriteLine("RexHides overlay: overlay HWND is zero");
            return;
        }

        Debug.WriteLine($"RexHides overlay: SetWindowPos ({x},{y}) {width}x{height} dpi={dpi}");
        NativeMethods.SetWindowPos(
            helper.Handle, Win32Constants.HWND_TOPMOST, x, y, width, height,
            Win32Constants.SWP_NOACTIVATE | Win32Constants.SWP_SHOWWINDOW);

        if (_overlay is { IsVisible: false })
        {
            _overlay.Show();
        }
    }

    private void EnsureOverlay()
    {
        if (_overlay != null)
        {
            return;
        }

        _overlay = new TitleBarOverlayWindow();
        _overlay.HideRequested += (_, _) =>
        {
            IntPtr target = _target;
            HideOverlay();
#if GHOME
            if (_gHome.IsGHomeWindow(target))
            {
                _gHome.Hide();
                return;
            }
#endif
            _hiding.Hide(target);
        };
        new WindowInteropHelper(_overlay).EnsureHandle();
    }

    private void HideOverlay()
    {
        _target = IntPtr.Zero;
        _locationHook?.Dispose();
        _locationHook = null;
        _stateHook?.Dispose();
        _stateHook = null;
        _overlay?.Hide();
    }
}
