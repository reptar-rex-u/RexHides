using System.Text.Json.Serialization;

namespace RexHides.Models;

public class HiddenWindowEntry
{
    /// <summary>Window handle stored as a long for JSON round-tripping.</summary>
    public long Hwnd { get; set; }

    public string Title { get; set; } = string.Empty;

    public string ExePath { get; set; } = string.Empty;

    public string ProcessName { get; set; } = string.Empty;

    public uint Pid { get; set; }

    public DateTime HiddenAtUtc { get; set; }

    [JsonIgnore]
    public IntPtr Handle => new(Hwnd);
}
