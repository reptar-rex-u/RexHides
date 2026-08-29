using System.IO;
using RexHides.Models;
using RexHides.Win32Native;

namespace RexHides.Services;

/// <summary>Safe matching helpers for the Google Home Brave PWA target.</summary>
public static class GHomeIdentityMatcher
{
    public const string DefaultAppHost = "home.google.com";

    public static bool IsBraveProcess(string? exePath) =>
        !string.IsNullOrWhiteSpace(exePath) &&
        exePath.EndsWith("brave.exe", StringComparison.OrdinalIgnoreCase);

    public static bool CommandLineContainsAppHost(string? commandLine, string host) =>
        !string.IsNullOrWhiteSpace(commandLine) &&
        commandLine.Contains(host, StringComparison.OrdinalIgnoreCase);

    public static bool LooksLikeGoogleHomeTitle(string title) =>
        title.Contains("Google Home", StringComparison.OrdinalIgnoreCase) ||
        title.Contains("Home - Devices", StringComparison.OrdinalIgnoreCase) ||
        title.Equals("Devices", StringComparison.OrdinalIgnoreCase);

    public static bool IsTopLevelWindow(IntPtr hwnd) =>
        hwnd != IntPtr.Zero &&
        NativeMethods.IsWindow(hwnd) &&
        NativeMethods.GetAncestor(hwnd, Win32Constants.GA_ROOT) == hwnd;

    public static List<IntPtr> FindCandidateWindows()
    {
        var matches = new List<IntPtr>();
        NativeMethods.EnumWindows(
            (hwnd, _) =>
            {
                if (IsTopLevelWindow(hwnd) && LooksLikeGoogleHomeWindow(hwnd))
                {
                    matches.Add(hwnd);
                }

                return true;
            },
            IntPtr.Zero);

        return matches;
    }

    public static bool LooksLikeGoogleHomeWindow(IntPtr hwnd)
    {
        NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
        string? exePath = NativeMethods.GetProcessExePath(pid);
        if (!IsBraveProcess(exePath))
        {
            return false;
        }

        string? commandLine = NativeMethods.GetProcessCommandLine(pid);
        if (CommandLineContainsAppHost(commandLine, DefaultAppHost))
        {
            return true;
        }

        string title = NativeMethods.GetWindowTitle(hwnd);
        return LooksLikeGoogleHomeTitle(title);
    }

    public static bool MatchesProfile(GHomeProfile profile, IntPtr hwnd)
    {
        if (!profile.HasIdentity || hwnd == IntPtr.Zero || !NativeMethods.IsWindow(hwnd))
        {
            return false;
        }

        if (!string.Equals(
                NativeMethods.GetWindowClassName(hwnd),
                profile.WindowClassName,
                StringComparison.Ordinal))
        {
            return false;
        }

        NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
        string? exePath = NativeMethods.GetProcessExePath(pid);
        if (string.IsNullOrWhiteSpace(exePath))
        {
            return false;
        }

        try
        {
            if (!string.Equals(
                    Path.GetFullPath(exePath),
                    Path.GetFullPath(profile.ExePath),
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }
        catch (Exception)
        {
            if (!string.Equals(exePath, profile.ExePath, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        string host = string.IsNullOrWhiteSpace(profile.AppUrlSignature)
            ? DefaultAppHost
            : profile.AppUrlSignature;
        string? commandLine = ResolveBraveCommandLine(hwnd, pid);
        if (CommandLineContainsAppHost(commandLine, host))
        {
            return true;
        }

        string title = NativeMethods.GetWindowTitle(hwnd);
        if (!string.IsNullOrWhiteSpace(profile.TitleSignature) &&
            title.Contains(profile.TitleSignature, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(host, DefaultAppHost, StringComparison.OrdinalIgnoreCase) &&
            LooksLikeGoogleHomeTitle(title))
        {
            return true;
        }

        return false;
    }

    public static string DeriveTitleSignature(string title)
    {
        if (title.Contains("Google Home", StringComparison.OrdinalIgnoreCase))
        {
            return "Google Home";
        }

        if (title.Contains("Home - Devices", StringComparison.OrdinalIgnoreCase))
        {
            return "Home - Devices";
        }

        if (title.Equals("Devices", StringComparison.OrdinalIgnoreCase))
        {
            return "Devices";
        }

        return "Google Home";
    }

    /// <summary>
    /// Chromium PWAs often run window UI on a subprocess whose command line does not
    /// include the --app URL. Fall back to any Brave process hosting home.google.com.
    /// </summary>
    private static string? ResolveBraveCommandLine(IntPtr hwnd, uint windowPid)
    {
        string? commandLine = NativeMethods.GetProcessCommandLine(windowPid);
        if (CommandLineContainsAppHost(commandLine, DefaultAppHost))
        {
            return commandLine;
        }

        NativeMethods.GetWindowThreadProcessId(hwnd, out _);
        foreach (System.Diagnostics.Process process in System.Diagnostics.Process.GetProcessesByName("brave"))
        {
            try
            {
                string? braveCommandLine = NativeMethods.GetProcessCommandLine((uint)process.Id);
                if (CommandLineContainsAppHost(braveCommandLine, DefaultAppHost))
                {
                    return braveCommandLine;
                }
            }
            catch (InvalidOperationException)
            {
                // Process exited while scanning.
            }
        }

        return commandLine;
    }

    public static GHomeProfile CaptureIdentity(IntPtr hwnd)
    {
        NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
        string? exePath = NativeMethods.GetProcessExePath(pid);
        string? commandLine = NativeMethods.GetProcessCommandLine(pid);
        string title = NativeMethods.GetWindowTitle(hwnd);

        return new GHomeProfile
        {
            ExePath = exePath ?? string.Empty,
            WindowClassName = NativeMethods.GetWindowClassName(hwnd),
            AppUrlSignature = DeriveAppUrlSignature(commandLine),
            TitleSignature = DeriveTitleSignature(title),
        };
    }

    public static string DeriveAppUrlSignature(string? commandLine)
    {
        string? appUrl = TryExtractAppUrl(commandLine);
        if (appUrl != null && Uri.TryCreate(appUrl, UriKind.Absolute, out Uri? uri))
        {
            return uri.Host;
        }

        return DefaultAppHost;
    }

    private static string? TryExtractAppUrl(string? commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return null;
        }

        int appIndex = commandLine.IndexOf("--app=", StringComparison.OrdinalIgnoreCase);
        if (appIndex >= 0)
        {
            return ReadCommandLineValue(commandLine, appIndex + "--app=".Length);
        }

        if (CommandLineContainsAppHost(commandLine, DefaultAppHost))
        {
            return $"https://{DefaultAppHost}/";
        }

        return null;
    }

    private static string ReadCommandLineValue(string commandLine, int startIndex)
    {
        if (startIndex >= commandLine.Length)
        {
            return string.Empty;
        }

        if (commandLine[startIndex] == '"')
        {
            int endQuote = commandLine.IndexOf('"', startIndex + 1);
            return endQuote > startIndex
                ? commandLine[(startIndex + 1)..endQuote]
                : commandLine[(startIndex + 1)..];
        }

        int space = commandLine.IndexOf(' ', startIndex);
        return space > startIndex
            ? commandLine[startIndex..space]
            : commandLine[startIndex..];
    }
}
