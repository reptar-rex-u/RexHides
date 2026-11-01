using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RexHides.Models;
using RexHides.Services;
using RexHides.Win32Native;

namespace RexHides.ViewModels;

public record RunningWindowInfo(long Hwnd, string Title, string ProcessName)
{
    public string DisplayText => $"{Title}  ({ProcessName})";
}

public partial class MainViewModel : ObservableObject
{
    private readonly WindowHidingService _hiding;
    private readonly SettingsService _settings;
    private readonly HotkeyService _hotkey;
    private readonly ElevationService _elevation;
    private readonly TitleBarOverlayService _overlay;

    [ObservableProperty]
    private string _hotkeyText = string.Empty;

    [ObservableProperty]
    private string? _hotkeyError;

    [ObservableProperty]
    private RunningWindowInfo? _selectedRunningWindow;

    [ObservableProperty]
    private string _statusText = "Ready.";

    public MainViewModel(
        WindowHidingService hiding,
        SettingsService settings,
        HotkeyService hotkey,
        ElevationService elevation,
        TitleBarOverlayService overlay)
    {
        _hiding = hiding;
        _settings = settings;
        _hotkey = hotkey;
        _elevation = elevation;
        _overlay = overlay;

        HotkeyText = settings.Current.Hotkey.ToString();

        foreach (AutoHideRule rule in settings.Current.Rules)
        {
            Rules.Add(new AutoHideRuleViewModel(rule, settings));
        }

        _hiding.WindowHidden += entry =>
        {
            HiddenWindows.Add(new HiddenWindowViewModel(entry, _hiding, AddAutoHideRuleFromEntry));
            StatusText = $"Hidden: {entry.Title}";
        };
        _hiding.WindowRestored += entry =>
        {
            HiddenWindowViewModel? vm = HiddenWindows.FirstOrDefault(w => w.Entry.Handle == entry.Handle);
            if (vm != null)
            {
                HiddenWindows.Remove(vm);
            }
        };

        RefreshRunningWindows();
    }

    public ObservableCollection<HiddenWindowViewModel> HiddenWindows { get; } = [];

    public ObservableCollection<AutoHideRuleViewModel> Rules { get; } = [];

    public ObservableCollection<RunningWindowInfo> RunningWindows { get; } = [];

    public bool IsElevated => _elevation.IsElevated;

    public bool IsNotElevated => !_elevation.IsElevated;

    public string ElevationLabel => _elevation.IsElevated ? "Administrator" : "Standard user";

    public bool StartMinimized
    {
        get => _settings.Current.StartMinimized;
        set { _settings.Current.StartMinimized = value; _settings.Save(); OnPropertyChanged(); }
    }

    public bool ShowBalloonTips
    {
        get => _settings.Current.ShowBalloonTips;
        set { _settings.Current.ShowBalloonTips = value; _settings.Save(); OnPropertyChanged(); }
    }

    public bool EnableTitleBarButton
    {
        get => _settings.Current.EnableTitleBarButton;
        set
        {
            _settings.Current.EnableTitleBarButton = value;
            _settings.Save();
            _overlay.SetEnabled(value);
            OnPropertyChanged();
        }
    }

    public bool AutostartEnabled
    {
        get => _elevation.IsAutostartEnabled();
        set { _elevation.SetAutostart(value); OnPropertyChanged(); }
    }

    public bool ElevatedAutostartEnabled
    {
        get => _elevation.IsElevatedAutostartEnabled();
        set
        {
            if (!_elevation.SetElevatedAutostart(value))
            {
                StatusText = "Could not change the elevated autostart task (requires administrator).";
            }

            OnPropertyChanged();
        }
    }

    public void ApplyHotkey(HotkeyDefinition hotkey)
    {
        if (_hotkey.Register(hotkey))
        {
            _settings.Current.Hotkey = hotkey;
            _settings.Save();
            HotkeyText = hotkey.ToString();
            HotkeyError = null;
        }
        else
        {
            HotkeyError = $"{hotkey} is already in use by another application.";
            _hotkey.Register(_settings.Current.Hotkey);
        }
    }

    [RelayCommand]
    private async Task HideActiveWindowAsync()
    {
        StatusText = "Focus the window to hide - it goes to the tray in 3 seconds...";
        await Task.Delay(3000);
        HideResult result = _hiding.HideForegroundWindow();
        StatusText = result switch
        {
            HideResult.Hidden => "Window sent to the tray.",
            HideResult.AccessDenied => "That window is elevated - restart RexHides as administrator.",
            _ => "That window cannot be hidden.",
        };
    }

    [RelayCommand]
    private void RestoreAll() => _hiding.RestoreAll();

    [RelayCommand]
    private void AddRule()
    {
        var rule = new AutoHideRule { Pattern = string.Empty };
        _settings.Current.Rules.Add(rule);
        _settings.Save();
        Rules.Add(new AutoHideRuleViewModel(rule, _settings));
    }

    [RelayCommand]
    private void RemoveRule(AutoHideRuleViewModel rule)
    {
        _settings.Current.Rules.Remove(rule.Rule);
        _settings.Save();
        Rules.Remove(rule);
    }

    [RelayCommand]
    private void AddRuleFromSelected()
    {
        if (SelectedRunningWindow is null)
        {
            return;
        }

        AddProcessAutoHideRule(SelectedRunningWindow.ProcessName, SelectedRunningWindow.Title);
    }

    public void AddAutoHideRuleFromEntry(HiddenWindowEntry entry)
    {
        AddProcessAutoHideRule(entry.ProcessName, entry.Title);
    }

    private void AddProcessAutoHideRule(string processName, string titleForStatus)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            StatusText = "Cannot create a rule - process name is unknown.";
            return;
        }

        bool exists = _settings.Current.Rules.Any(r =>
            r.MatchType == RuleMatchType.ProcessName &&
            string.Equals(NormalizeProcessName(r.Pattern), NormalizeProcessName(processName),
                StringComparison.OrdinalIgnoreCase));
        if (exists)
        {
            StatusText = $"An auto-hide rule for {NormalizeProcessName(processName)} already exists.";
            return;
        }

        var rule = new AutoHideRule
        {
            MatchType = RuleMatchType.ProcessName,
            Pattern = NormalizeProcessName(processName),
        };
        _settings.Current.Rules.Add(rule);
        _settings.Save();
        Rules.Add(new AutoHideRuleViewModel(rule, _settings));
        StatusText = $"Added auto-hide rule for {rule.Pattern} ({titleForStatus}).";
    }

    private static string NormalizeProcessName(string name)
    {
        name = name.Trim();
        if (!name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            name += ".exe";
        }

        return name;
    }

    [RelayCommand]
    private void RefreshRunningWindows()
    {
        RunningWindows.Clear();
        NativeMethods.EnumWindows(
            (hwnd, _) =>
            {
                if (_hiding.IsEligible(hwnd))
                {
                    string title = NativeMethods.GetWindowTitle(hwnd);
                    if (title.Length > 0)
                    {
                        NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
                        string process = Path.GetFileName(NativeMethods.GetProcessExePath(pid) ?? "unknown");
                        RunningWindows.Add(new RunningWindowInfo(hwnd.ToInt64(), title, process));
                    }
                }

                return true;
            },
            IntPtr.Zero);
    }

    [RelayCommand]
    private void RestartElevated()
    {
        if (_elevation.RestartElevated())
        {
            App.ExitForElevationHandoff();
        }
    }
}
