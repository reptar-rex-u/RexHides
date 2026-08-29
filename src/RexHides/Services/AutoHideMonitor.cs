using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Threading;
using RexHides.Models;
using RexHides.Win32Native;

namespace RexHides.Services;

/// <summary>
/// Watches for new top-level windows via SetWinEventHook(EVENT_OBJECT_SHOW) -
/// event-driven, no polling, no DLL injection - and auto-hides windows matching
/// the user's rules. Must be started on the UI thread (needs a message pump).
/// </summary>
public class AutoHideMonitor : IDisposable
{
    private readonly SettingsService _settings;
    private readonly WindowHidingService _hiding;
    private readonly Dispatcher _dispatcher;

    /// <summary>Windows already auto-hidden once (for OnlyFirstLaunch semantics).</summary>
    private readonly HashSet<IntPtr> _handledOnce = [];

    /// <summary>Windows the user manually restored - never fight the user by re-hiding these.</summary>
    private readonly HashSet<IntPtr> _manuallyRestored = [];

    private WinEventHook? _showHook;

    public AutoHideMonitor(SettingsService settings, WindowHidingService hiding)
    {
        _settings = settings;
        _hiding = hiding;
        _dispatcher = Dispatcher.CurrentDispatcher;

        _hiding.WindowRestored += entry => _manuallyRestored.Add(entry.Handle);
    }

    public event Action<HiddenWindowEntry>? WindowAutoHidden;

    public void Start()
    {
        _showHook ??= new WinEventHook(
            Win32Constants.EVENT_OBJECT_SHOW, Win32Constants.EVENT_OBJECT_SHOW, OnWinEvent);
    }

    public void Dispose()
    {
        _showHook?.Dispose();
        _showHook = null;
    }

    private void OnWinEvent(
        IntPtr hook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
    {
        if (idObject != Win32Constants.OBJID_WINDOW || idChild != Win32Constants.CHILDID_SELF || hwnd == IntPtr.Zero)
        {
            return;
        }

        if (_settings.Current.Rules.Count == 0 || _manuallyRestored.Contains(hwnd))
        {
            return;
        }

        // Give the window a moment to finish initializing (title, styles) before evaluating.
        _ = _dispatcher.BeginInvoke(DispatcherPriority.Background, async () =>
        {
            await Task.Delay(400);
            TryApplyRules(hwnd);
        });
    }

    private void TryApplyRules(IntPtr hwnd)
    {
        if (_manuallyRestored.Contains(hwnd) || !_hiding.IsEligible(hwnd))
        {
            return;
        }

        NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
        string processName = Path.GetFileName(NativeMethods.GetProcessExePath(pid) ?? string.Empty);
        string title = NativeMethods.GetWindowTitle(hwnd);

        foreach (AutoHideRule rule in _settings.Current.Rules)
        {
            if (!rule.Enabled || string.IsNullOrWhiteSpace(rule.Pattern))
            {
                continue;
            }

            if (rule.OnlyFirstLaunch && _handledOnce.Contains(hwnd))
            {
                continue;
            }

            if (!Matches(rule, processName, title))
            {
                continue;
            }

            if (_hiding.Hide(hwnd) == HideResult.Hidden)
            {
                _handledOnce.Add(hwnd);
                if (_hiding.HiddenWindows.FirstOrDefault(e => e.Handle == hwnd) is { } entry)
                {
                    WindowAutoHidden?.Invoke(entry);
                }
            }

            return;
        }
    }

    private static bool Matches(AutoHideRule rule, string processName, string title)
    {
        switch (rule.MatchType)
        {
            case RuleMatchType.ProcessName:
                string pattern = rule.Pattern.Trim();
                if (!pattern.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    pattern += ".exe";
                }

                return string.Equals(processName, pattern, StringComparison.OrdinalIgnoreCase);

            case RuleMatchType.TitleWildcard:
                if (title.Length == 0)
                {
                    return false;
                }

                string regex = "^" + Regex.Escape(rule.Pattern.Trim())
                    .Replace(@"\*", ".*")
                    .Replace(@"\?", ".") + "$";
                return Regex.IsMatch(title, regex, RegexOptions.IgnoreCase);

            default:
                return false;
        }
    }
}
