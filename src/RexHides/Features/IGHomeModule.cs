namespace RexHides.Features;

/// <summary>Compile-time optional Google Home persistent-tray integration.</summary>
public interface IGHomeModule : IDisposable
{
    event Action? StateChanged;

    bool IsEnabled { get; }

    bool IsOverlayActive { get; }

    bool IsConnected { get; }

    bool IsHidden { get; }

    string StatusText { get; }

    IntPtr TrackedHwnd { get; }

    void Initialize();

    bool IsGHomeWindow(IntPtr hwnd);

    void Toggle();

    void Show();

    void Hide();

    void CloseWindow();

    void SetEnabled(bool enabled);

    void SetShowOverlayButton(bool enabled);

    bool TryCaptureFromForeground(out string message);
}
