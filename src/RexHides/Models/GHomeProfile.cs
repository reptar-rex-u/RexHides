namespace RexHides.Models;

/// <summary>
/// Opt-in identity for the Google Home Brave PWA persistent-tray profile.
/// Matching requires executable path, window class, and title signature together.
/// </summary>
public class GHomeProfile
{
    public bool Enabled { get; set; }

    /// <summary>Show the G Home-only title-bar overlay on the verified target window.</summary>
    public bool ShowOverlayButton { get; set; } = true;

    /// <summary>Full path to brave.exe for the PWA host process.</summary>
    public string ExePath { get; set; } = string.Empty;

    public string WindowClassName { get; set; } = string.Empty;

    /// <summary>Brave PWA host matched in the process command line (e.g. home.google.com).</summary>
    public string AppUrlSignature { get; set; } = string.Empty;

    /// <summary>Legacy title substring fallback when command-line matching is unavailable.</summary>
    public string TitleSignature { get; set; } = string.Empty;

    public bool HasIdentity =>
        !string.IsNullOrWhiteSpace(ExePath) &&
        !string.IsNullOrWhiteSpace(WindowClassName) &&
        (!string.IsNullOrWhiteSpace(AppUrlSignature) || !string.IsNullOrWhiteSpace(TitleSignature));
}
