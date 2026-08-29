using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RexHides.Features;
using RexHides.Models;
using RexHides.Services;

namespace RexHides.ViewModels;

public partial class MainViewModel
{
    [ObservableProperty]
    private string _gHomeStatusText = "G Home is disabled.";

    partial void OnGHomeStatusTextChanged(string value) { }

    internal void WireGHome(IGHomeModule gHome, TitleBarOverlayService overlay, SettingsService settings)
    {
        _gHome.StateChanged += () =>
        {
            GHomeStatusText = _gHome.StatusText;
            OnPropertyChanged(nameof(GHomeEnabled));
            OnPropertyChanged(nameof(GHomeShowOverlayButton));
            OnPropertyChanged(nameof(GHomeHasIdentity));
            OnPropertyChanged(nameof(GHomeIdentitySummary));
            OnPropertyChanged(nameof(GHomeConnectionSummary));
            overlay.RefreshGHomeOverlay();
        };

        GHomeStatusText = _gHome.StatusText;
    }

    public bool GHomeEnabled
    {
        get => _settings.Current.GHome.Enabled;
        set
        {
            _gHome.SetEnabled(value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(GHomeConnectionSummary));
        }
    }

    public bool GHomeShowOverlayButton
    {
        get => _settings.Current.GHome.ShowOverlayButton;
        set
        {
            _gHome.SetShowOverlayButton(value);
            OnPropertyChanged();
        }
    }

    public bool GHomeHasIdentity => _settings.Current.GHome.HasIdentity;

    public string GHomeIdentitySummary => GHomeHasIdentity
        ? $"{IdentityHostSummary()} · {_settings.Current.GHome.WindowClassName}"
        : "No Google Home window identified yet.";

    private string IdentityHostSummary()
    {
        GHomeProfile profile = _settings.Current.GHome;
        if (!string.IsNullOrWhiteSpace(profile.AppUrlSignature))
        {
            return profile.AppUrlSignature;
        }

        return string.IsNullOrWhiteSpace(profile.TitleSignature)
            ? GHomeIdentityMatcher.DefaultAppHost
            : profile.TitleSignature;
    }

    public string GHomeConnectionSummary
    {
        get
        {
            if (!GHomeEnabled)
            {
                return "Disabled";
            }

            if (!_gHome.IsConnected)
            {
                return "Waiting for Google Home";
            }

            return _gHome.IsHidden ? "Hidden in tray" : "Visible";
        }
    }

    [RelayCommand]
    private void IdentifyGHomeWindow()
    {
        if (_gHome.TryCaptureFromForeground(out string message))
        {
            GHomeStatusText = message;
            OnPropertyChanged(nameof(GHomeHasIdentity));
            OnPropertyChanged(nameof(GHomeIdentitySummary));
            OnPropertyChanged(nameof(GHomeConnectionSummary));
        }
        else
        {
            GHomeStatusText = message;
        }
    }

    [RelayCommand]
    private void ToggleGHome() => _gHome.Toggle();

    [RelayCommand]
    private void ShowGHome() => _gHome.Show();

    [RelayCommand]
    private void HideGHome() => _gHome.Hide();
}
