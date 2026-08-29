using System.IO;
using System.Text.Json;
using RexHides.Models;
using RexHides.Win32Native;

namespace RexHides.Services;

/// <summary>
/// Persists the list of currently hidden windows to disk so that if RexHides
/// crashes (or is killed) the windows are not lost forever: on the next start
/// any still-alive hidden windows are made visible again.
/// </summary>
public class CrashRecoveryService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _recoveryPath =
        Path.Combine(SettingsService.AppDataDirectory, "hidden-windows.json");

    public void Update(IEnumerable<HiddenWindowEntry> hidden)
    {
        try
        {
            var list = hidden.ToList();
            if (list.Count == 0)
            {
                File.Delete(_recoveryPath);
            }
            else
            {
                File.WriteAllText(_recoveryPath, JsonSerializer.Serialize(list, JsonOptions));
            }
        }
        catch (IOException)
        {
            // Best-effort safety net; never crash the app over it.
        }
    }

    /// <summary>
    /// Restores windows orphaned by a previous crash. Returns how many were recovered.
    /// Verifies handle liveness and process identity before touching anything, since
    /// HWND values can be recycled between sessions.
    /// </summary>
    public int RecoverOrphanedWindows()
    {
        List<HiddenWindowEntry>? entries = null;
        try
        {
            if (File.Exists(_recoveryPath))
            {
                entries = JsonSerializer.Deserialize<List<HiddenWindowEntry>>(File.ReadAllText(_recoveryPath));
                File.Delete(_recoveryPath);
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return 0;
        }

        if (entries == null)
        {
            return 0;
        }

        int recovered = 0;
        var stillHidden = new List<HiddenWindowEntry>();
        foreach (HiddenWindowEntry entry in entries)
        {
            IntPtr hwnd = entry.Handle;
            if (!NativeMethods.IsWindow(hwnd))
            {
                continue;
            }

            NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid != entry.Pid)
            {
                continue; // Handle was recycled by another process.
            }

            if (NativeMethods.IsWindowVisible(hwnd) && !NativeMethods.IsWindowCloaked(hwnd))
            {
                recovered++;
                continue;
            }

            if (WindowHidingService.TryMakeVisible(hwnd))
            {
                recovered++;
            }
            else if (NativeMethods.IsWindow(hwnd))
            {
                stillHidden.Add(entry);
            }
        }

        Update(stillHidden);
        return recovered;
    }
}
