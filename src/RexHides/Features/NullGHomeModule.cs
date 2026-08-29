namespace RexHides.Features;

/// <summary>No-op G Home module used in public builds.</summary>
public sealed class NullGHomeModule : IGHomeModule
{
    public static NullGHomeModule Instance { get; } = new();

    private NullGHomeModule()
    {
    }

    public event Action? StateChanged
    {
        add { }
        remove { }
    }

    public bool IsEnabled => false;

    public bool IsOverlayActive => false;

    public bool IsConnected => false;

    public bool IsHidden => false;

    public string StatusText => string.Empty;

    public IntPtr TrackedHwnd => IntPtr.Zero;

    public void Initialize()
    {
    }

    public bool IsGHomeWindow(IntPtr hwnd) => false;

    public void Toggle()
    {
    }

    public void Show()
    {
    }

    public void Hide()
    {
    }

    public void CloseWindow()
    {
    }

    public void SetEnabled(bool enabled)
    {
    }

    public void SetShowOverlayButton(bool enabled)
    {
    }

    public bool TryCaptureFromForeground(out string message)
    {
        message = string.Empty;
        return false;
    }

    public void Dispose()
    {
    }
}
