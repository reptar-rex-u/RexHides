using System.Diagnostics;
using System.IO;

namespace RexHides.Services;

/// <summary>
/// A second RexHides process that waits for the main process to exit. If the main
/// process was force-killed (no graceful-exit flag), the watchdog restores any
/// windows left in the recovery file so the user does not have to relaunch RexHides.
/// </summary>
public static class WatchdogService
{
    public const string WatchdogArg = "--watchdog";

    private static readonly string GracefulExitPath =
        Path.Combine(SettingsService.AppDataDirectory, "graceful-exit.flag");

    private static Process? _watchdogProcess;

    public static bool IsWatchdogMode(string[] args) =>
        args.Any(a => string.Equals(a, WatchdogArg, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Headless entry used when launched as <c>RexHides.exe --watchdog &lt;parentPid&gt;</c>.
    /// Does not take the single-instance mutex or show UI.
    /// </summary>
    public static int RunWatchdog(string[] args)
    {
        int pidIndex = Array.FindIndex(args, a =>
            string.Equals(a, WatchdogArg, StringComparison.OrdinalIgnoreCase));
        if (pidIndex < 0 || pidIndex + 1 >= args.Length ||
            !int.TryParse(args[pidIndex + 1], out int parentPid))
        {
            return 1;
        }

        try
        {
            using Process parent = Process.GetProcessById(parentPid);
            parent.WaitForExit();
        }
        catch (ArgumentException)
        {
            // Parent already gone - fall through to recovery.
        }

        if (ConsumeGracefulExitFlag())
        {
            return 0;
        }

        try
        {
            var recovery = new CrashRecoveryService();
            recovery.RecoverOrphanedWindows();
        }
        catch
        {
            // Best-effort; never leave a watchdog hanging.
        }

        return 0;
    }

    public static void StartWatching(int parentPid)
    {
        StopWatching(writeGracefulFlag: false);

        try
        {
            Directory.CreateDirectory(SettingsService.AppDataDirectory);
            File.Delete(GracefulExitPath);
        }
        catch (IOException)
        {
            // Non-fatal.
        }

        string? exe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exe))
        {
            return;
        }

        try
        {
            _watchdogProcess = Process.Start(new ProcessStartInfo
            {
                FileName = exe,
                Arguments = $"{WatchdogArg} {parentPid}",
                UseShellExecute = false,
                CreateNoWindow = true,
            });
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            Debug.WriteLine($"RexHides watchdog failed to start: {ex.Message}");
        }
    }

    /// <summary>
    /// Signals the watchdog that this exit is intentional so it must not restore windows
    /// (main already restored them, or an elevated handoff will take over the recovery file).
    /// </summary>
    public static void StopWatching(bool writeGracefulFlag)
    {
        if (writeGracefulFlag)
        {
            try
            {
                Directory.CreateDirectory(SettingsService.AppDataDirectory);
                File.WriteAllText(GracefulExitPath, DateTime.UtcNow.ToString("O"));
            }
            catch (IOException)
            {
                // Non-fatal.
            }
        }

        // Do not kill the watchdog - let it observe our exit and honor the flag.
        _watchdogProcess = null;
    }

    private static bool ConsumeGracefulExitFlag()
    {
        try
        {
            if (!File.Exists(GracefulExitPath))
            {
                return false;
            }

            File.Delete(GracefulExitPath);
            return true;
        }
        catch (IOException)
        {
            return true; // Prefer skipping recovery over double-restoring.
        }
    }
}
