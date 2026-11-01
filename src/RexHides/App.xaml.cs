using System.Windows;
using System.Windows.Threading;
using RexHides.Services;
using RexHides.ViewModels;
using RexHides.Views;
using RexHides.Win32Native;

namespace RexHides;

public partial class App : Application
{
    private Mutex? _instanceMutex;
    private SettingsService? _settings;
    private CrashRecoveryService? _recovery;
    private WindowHidingService? _hiding;
    private TrayIconManager? _tray;
    private HotkeyService? _hotkey;
    private AutoHideMonitor? _monitor;
    private TitleBarOverlayService? _overlay;
    private ElevationService? _elevation;
    private MainWindow? _mainWindow;
    private EventWaitHandle? _stopEvent;
    private RegisteredWaitHandle? _stopRegistration;

    private static bool _isElevationHandoff;
    private const string StopEventName = "RexHides_Stop";

    public static bool IsExitRequested { get; private set; }

    /// <summary>Exit without restoring hidden windows: the elevated instance takes over via the recovery file.</summary>
    public static void ExitForElevationHandoff()
    {
        IsExitRequested = true;
        _isElevationHandoff = true;
        WatchdogService.StopWatching(writeGracefulFlag: true);
        var app = (App)Current;
        app._tray?.Dispose();
        app.Shutdown();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Headless crash-recovery companion - no UI, no single-instance mutex.
        if (WatchdogService.IsWatchdogMode(e.Args))
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            int code = WatchdogService.RunWatchdog(e.Args);
            Shutdown(code);
            return;
        }

        if (e.Args.Any(arg => string.Equals(arg, "--stop", StringComparison.OrdinalIgnoreCase)))
        {
            TrySignalStop();
            Shutdown();
            return;
        }

        if (!AcquireSingleInstanceMutex())
        {
            MessageBox.Show("RexHides is already running (check the system tray).",
                "RexHides", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        _stopEvent = new EventWaitHandle(false, EventResetMode.AutoReset, StopEventName);
        _stopRegistration = ThreadPool.RegisterWaitForSingleObject(
            _stopEvent,
            (_, _) => Dispatcher.BeginInvoke(ExitApplication),
            null,
            Timeout.Infinite,
            executeOnlyOnce: true);

        WatchdogService.StartWatching(Environment.ProcessId);

        DispatcherUnhandledException += OnUnhandledException;

        // Composition root - all services are UI-thread affine singletons.
        _settings = new SettingsService();
        _recovery = new CrashRecoveryService();
        _hiding = new WindowHidingService(_recovery);
        _elevation = new ElevationService();
        _hotkey = new HotkeyService();
        _monitor = new AutoHideMonitor(_settings, _hiding);
        _overlay = new TitleBarOverlayService(_hiding);
        _tray = new TrayIconManager(_hiding, _settings, _elevation);

        // Recover windows orphaned by a previous crash before doing anything else.
        int recovered = _recovery.RecoverOrphanedWindows();

        _tray.Initialize();
        _tray.OpenSettingsRequested += (_, _) => ShowMainWindow();
        _tray.ExitRequested += (_, _) => ExitApplication();
        _tray.RestartElevatedRequested += (_, _) =>
        {
            if (_elevation.RestartElevated())
            {
                ExitForElevationHandoff();
            }
        };

        _hotkey.HotkeyPressed += (_, _) => OnHotkeyPressed();
        if (!_hotkey.Register(_settings.Current.Hotkey))
        {
            _tray.ShowBalloon("Hotkey unavailable",
                $"{_settings.Current.Hotkey} is in use by another app. Pick a different one in settings.",
                System.Windows.Forms.ToolTipIcon.Warning);
        }

        _hiding.AccessDenied += message =>
            _tray.ShowBalloon("Administrator required", message, System.Windows.Forms.ToolTipIcon.Warning);
        _hiding.RestoreFailed += message =>
            _tray.ShowBalloon("Could not restore window", message, System.Windows.Forms.ToolTipIcon.Warning);

        _monitor.WindowAutoHidden += entry =>
            _tray.ShowBalloon("Auto-hidden", $"{entry.Title} was sent to the tray.");
        _monitor.Start();

        _overlay.SetEnabled(_settings.Current.EnableTitleBarButton);

        var viewModel = new MainViewModel(_hiding, _settings, _hotkey, _elevation, _overlay);
        _mainWindow = new MainWindow(viewModel);
        _mainWindow.Deactivated += (_, _) => _overlay.RefreshFromForeground();

        if (recovered > 0)
        {
            _tray.ShowBalloon("Windows recovered",
                $"Restored {recovered} window(s) hidden by a previous session.");
        }

        bool startHidden = e.Args.Contains("--minimized") || _settings.Current.StartMinimized;
        if (!startHidden)
        {
            ShowMainWindow();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Tell the watchdog this exit is intentional before we go away.
        WatchdogService.StopWatching(writeGracefulFlag: true);

        // Never leave windows stranded in the hidden state (except during elevation
        // handoff, where the recovery file lets the new instance rescue them).
        if (!_isElevationHandoff)
        {
            _hiding?.RestoreAll();
        }

        _overlay?.Dispose();
        _monitor?.Dispose();
        _hotkey?.Dispose();
        _tray?.Dispose();
        _stopRegistration?.Unregister(null);
        _stopEvent?.Dispose();
        _instanceMutex?.ReleaseMutex();
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }

    private void OnHotkeyPressed()
    {
        IntPtr foreground = NativeMethods.GetForegroundWindow();
        IntPtr root = NativeMethods.GetAncestor(foreground, Win32Constants.GA_ROOT);
        if (root == IntPtr.Zero)
        {
            root = foreground;
        }

        NativeMethods.GetWindowThreadProcessId(root, out uint pid);
        if (pid == (uint)Environment.ProcessId)
        {
            _mainWindow?.Hide();
            return;
        }

        _hiding?.HideForegroundWindow();
    }

    private void ShowMainWindow()
    {
        if (_mainWindow == null)
        {
            return;
        }

        _mainWindow.Show();
        if (_mainWindow.WindowState == WindowState.Minimized)
        {
            _mainWindow.WindowState = WindowState.Normal;
        }

        _mainWindow.Activate();
    }

    private void ExitApplication()
    {
        IsExitRequested = true;
        Shutdown();
    }

    private static void TrySignalStop()
    {
        try
        {
            using var stopEvent = EventWaitHandle.OpenExisting(StopEventName);
            stopEvent.Set();
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            // RexHides is not running.
        }
        catch (InvalidOperationException)
        {
            // The named event was closed while the stop request raced shutdown.
        }
    }

    /// <summary>
    /// Acquires the single-instance mutex, waiting briefly so that
    /// "Restart as Administrator" can hand off from the exiting instance.
    /// </summary>
    private bool AcquireSingleInstanceMutex()
    {
        for (int attempt = 0; attempt < 10; attempt++)
        {
            _instanceMutex = new Mutex(initiallyOwned: true, "RexHides_SingleInstance", out bool createdNew);
            if (createdNew)
            {
                return true;
            }

            _instanceMutex.Dispose();
            _instanceMutex = null;
            Thread.Sleep(500);
        }

        return false;
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // Last-ditch safety: make every hidden window visible again before dying.
        try
        {
            WatchdogService.StopWatching(writeGracefulFlag: true);
            _hiding?.RestoreAll();
            _tray?.Dispose();
        }
        catch
        {
            // Nothing more we can do.
        }

        MessageBox.Show(
            $"RexHides hit an unexpected error and will close.\n\n{e.Exception.Message}",
            "RexHides", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
