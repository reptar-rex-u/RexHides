using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using RexHides.Models;

namespace RexHides.Services;

/// <summary>Loads and saves settings.json under %AppData%\RexHides.</summary>
public class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _settingsPath;

    public SettingsService()
    {
        string dir = AppDataDirectory;
        Directory.CreateDirectory(dir);
        _settingsPath = Path.Combine(dir, "settings.json");
        Current = Load();
    }

    public static string AppDataDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RexHides");

    public AppSettings Current { get; private set; }

    public event EventHandler? Saved;

    public void Save()
    {
        try
        {
            File.WriteAllText(_settingsPath, JsonSerializer.Serialize(Current, JsonOptions));
        }
        catch (IOException)
        {
            // Non-fatal: settings simply won't persist this time.
        }

        Saved?.Invoke(this, EventArgs.Empty);
    }

    private AppSettings Load()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var loaded = JsonSerializer.Deserialize<AppSettings>(
                    File.ReadAllText(_settingsPath), JsonOptions);
                if (loaded != null)
                {
                    return loaded;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            // Corrupt or unreadable settings: fall through to defaults.
        }

        return new AppSettings();
    }
}
