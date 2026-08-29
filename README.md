# RexHides

A Windows system utility (.NET 8 WPF) that minimizes any application window to its own dedicated system tray icon.

**Download:** [GitHub Releases](https://github.com/reptar-rex-u/RexHides/releases) — self-contained Windows x64 zip, no separate .NET install required.

## Features

- **Minimize any window to the tray** - hide the active window via hotkey, the settings UI, or automatic rules. Restore it by clicking its tray icon.
- **Dynamic per-window tray icons** - each hidden window gets its own tray icon using that application's real icon (extracted via `WM_GETICON`, the window class icon, or the executable's embedded icon). Left-click restores the window and removes the icon; right-click offers Restore / Close Window / Restore All.
- **Theme-aware tray menus** - context menus for the main tray icon and hidden-window icons follow the Windows app light/dark theme detected when the tray manager starts. Theme changes made afterward take effect the next time the tray manager starts.
- **Customizable global hotkey** - default `Ctrl+Alt+H`, re-mappable live in settings (implemented with `RegisterHotKey`, no keyboard-hook DLLs).
- **Auto-hide rules** - match by process name (`spotify.exe`) or window title wildcard (`*Discord*`); matching windows are sent to the tray automatically when they appear. Rules can apply once per window or on every show, and never fight a window you manually restored.
- **Title bar button (experimental)** - optionally floats a small charcoal "minimize to tray" button with a Plex-gold (`#E5A00D`) glyph closer to the standard caption controls/minimize button of the active window. It is implemented as a non-activating overlay window; no code is injected into other processes.
- **Elevation support (opt-in)** - runs as a standard user by default. "Restart as Administrator" enables managing elevated windows, and an optional Task Scheduler task starts RexHides elevated at logon without a UAC prompt.
- **Crash safety** - the hidden-window list is persisted to disk; on exit all windows are restored. A lightweight watchdog process also restores orphaned windows immediately if RexHides is force-killed from Task Manager (no need to relaunch).

## Build and run

Requires the .NET 8 SDK on Windows 10/11 (x64).

```powershell
dotnet build RexHides.sln
dotnet run --project src/RexHides
```

Publish a release build locally:

```powershell
.\scripts\publish-public.ps1
```

Output: `dist\public\RexHides.exe`, `dist\public\index.html`, and `dist\RexHides-{version}-win-x64.zip`.

Official releases are built by GitHub Actions when you push a `v*` tag (see [Install.md](Install.md)).

Run with `--minimized` to start straight into the tray. A running instance can
be stopped gracefully with `--stop`; this signals the existing instance to run
its normal shutdown path and restore hidden windows.

## Usage

1. Launch RexHides - it lives in the system tray (charcoal circular minimize-to-tray icon with a Plex-gold (`#E5A00D`) glyph). The title-bar minimize-to-tray button uses the same charcoal background and Plex-gold glyph. Double-click to open settings.
2. Press the global hotkey (`Ctrl+Alt+H` by default) to send the focused window to the tray.
3. Click the window's tray icon to bring it back.
4. Add auto-hide rules in settings for apps you always want tray-parked at launch.

Tray context menus for the main icon and hidden-window icons use the Windows app light/dark theme detected when the tray manager starts; they do not switch live when the system theme changes.

Settings are stored in `%AppData%\RexHides\settings.json`.

To request a graceful stop from another controller:

```powershell
RexHides.exe --stop
```

## Project structure

```
src/RexHides/
  Win32Native/   P/Invoke layer (user32, shell32, kernel32, dwmapi) + WinEvent hook wrapper
  Models/        Settings, rules, hotkey, hidden-window entries
  Services/      Window hiding, tray icons, icon extraction, hotkey, auto-hide monitor,
                 settings, crash recovery, elevation, title bar overlay
  ViewModels/    CommunityToolkit.Mvvm view models
  Views/         MainWindow, hotkey capture control, overlay window
```

## Known limitations

- Elevated (admin) windows can only be hidden when RexHides itself runs elevated (Windows UIPI). Use "Restart as Administrator" in settings or the tray menu.
- Hidden windows disappear from the taskbar and Alt-Tab by design (`SW_HIDE`). A watchdog companion restores them if RexHides is force-killed; a normal Exit from the tray menu restores them itself.
- After a force-kill, leftover tray icons from the dead process are ghosts that do nothing. Hover the notification area (or click the overflow chevron) so Explorer purges them. The hidden windows themselves are restored by the watchdog without relaunching RexHides.
- UWP apps (Calculator, Settings, Store, and others hosted by ApplicationFrameHost) can stay DWM-cloaked if Windows terminated the suspended app while it was hidden. If recovery cannot bring the window back, open the app again from the Start menu.
- The title bar button sits left of the caption controls, closer to the standard minimize button (including room for a fourth button such as Brave's profile control); apps with fully custom title bars (Chrome, Electron, some UWP apps) may still place it imperfectly.
- While running elevated, dragging items from non-elevated apps onto the RexHides window is blocked by Windows (UIPI); use the built-in running-windows picker instead.

## Cursor subagents

This repo defines project subagents in `.cursor/agents/`: `win32-interop` (native API work), `wpf-mvvm-ui` (XAML/MVVM), `tray-lifecycle` (NotifyIcon and hidden-window safety invariants), and `release-docs` (README/CHANGELOG/versioning). See `.cursor/rules/subagent-delegation.mdc` for when to use each.
