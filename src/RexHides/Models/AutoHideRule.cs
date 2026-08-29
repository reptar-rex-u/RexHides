namespace RexHides.Models;

public enum RuleMatchType
{
    /// <summary>Match the process executable name, e.g. "spotify.exe".</summary>
    ProcessName,

    /// <summary>Match the window title with * and ? wildcards, e.g. "*Discord*".</summary>
    TitleWildcard,
}

public class AutoHideRule
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public bool Enabled { get; set; } = true;

    public RuleMatchType MatchType { get; set; } = RuleMatchType.ProcessName;

    public string Pattern { get; set; } = string.Empty;

    /// <summary>
    /// When true, each window is only auto-hidden once; restoring it keeps it visible.
    /// When false, the window is re-hidden every time it is shown (except right after
    /// a manual restore from the tray).
    /// </summary>
    public bool OnlyFirstLaunch { get; set; } = true;
}
