namespace RexHides.Models;

public class AppSettings
{
    public HotkeyDefinition Hotkey { get; set; } = HotkeyDefinition.Default;

    public List<AutoHideRule> Rules { get; set; } = [];

    /// <summary>Start with the settings window hidden (tray only).</summary>
    public bool StartMinimized { get; set; } = true;

    public bool ShowBalloonTips { get; set; } = true;

    /// <summary>Stretch feature: overlay a tray-minimize button on the active window's title bar.</summary>
    public bool EnableTitleBarButton { get; set; }

#if GHOME
    /// <summary>Opt-in persistent tray profile for the Google Home Brave PWA (personal build only).</summary>
    public GHomeProfile GHome { get; set; } = new();
#endif
}
