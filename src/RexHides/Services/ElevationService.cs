using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;
using Microsoft.Win32;

namespace RexHides.Services;

/// <summary>
/// Elevation state and transitions. The app manifest stays asInvoker; running as
/// administrator is opt-in so everyday use has no UAC friction.
/// </summary>
public class ElevationService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "RexHides";
    private const string TaskName = "RexHides Elevated Autostart";

    public bool IsElevated { get; } =
        new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);

    public static string ExecutablePath => Environment.ProcessPath ?? string.Empty;

    /// <summary>Relaunches the app with a UAC prompt. Returns false if the user declined.</summary>
    public bool RestartElevated()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = ExecutablePath,
                UseShellExecute = true,
                Verb = "runas",
            });
            return true;
        }
        catch (Win32Exception)
        {
            return false; // User cancelled the UAC prompt.
        }
    }

    // ---- Normal (non-elevated) autostart via HKCU Run key ----

    public bool IsAutostartEnabled()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return key?.GetValue(AppName) != null;
    }

    public void SetAutostart(bool enabled)
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        if (key == null)
        {
            return;
        }

        if (enabled)
        {
            key.SetValue(AppName, $"\"{ExecutablePath}\" --minimized");
        }
        else
        {
            key.DeleteValue(AppName, throwOnMissingValue: false);
        }
    }

    // ---- Elevated autostart via Task Scheduler (RunLevel=Highest, no UAC prompt at logon) ----

    public bool IsElevatedAutostartEnabled()
    {
        return RunSchtasks($"/Query /TN \"{TaskName}\"") == 0;
    }

    /// <summary>Creating/removing the highest-run-level task itself requires elevation.</summary>
    public bool SetElevatedAutostart(bool enabled)
    {
        if (enabled)
        {
            return RunSchtasks(
                $"/Create /F /TN \"{TaskName}\" /SC ONLOGON /RL HIGHEST " +
                $"/TR \"'{ExecutablePath}' --minimized\"") == 0;
        }

        return RunSchtasks($"/Delete /F /TN \"{TaskName}\"") == 0;
    }

    private static int RunSchtasks(string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            process!.WaitForExit(10_000);
            return process.ExitCode;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            return -1;
        }
    }
}
