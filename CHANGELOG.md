# Changelog

All notable changes to RexHides are documented here.
Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/); versioning: [SemVer](https://semver.org/).

## [Unreleased]

### Fixed

- G Home now matches the Google Home Brave PWA by `home.google.com` in the process command line instead of the window title, so pages like Devices reconnect reliably.
- Crash recovery now uses the same UWP-aware restore path as a normal tray restore (`SW_SHOW` / `SW_RESTORE`, foreground activation, DWM-cloak retry). Failed restores keep the tray icon and recovery file instead of dropping the window.
- Pressing the global hotkey while RexHides is focused hides the settings window to the main tray icon instead of doing nothing.
- Title bar overlay button now re-evaluates the foreground window when settings deactivate, polls `GetForegroundWindow` as a fallback, marshals WinEvent callbacks onto the UI thread, and clamps its position to the target frame.

### Changed

- Public GitHub releases are self-contained win-x64 zips (no separate .NET Desktop Runtime install).
- **Public vs personal builds:** G Home is compiled out of the default public build (`EnableGHome=false`). Enable with `-p:EnableGHome=true` for the personal build; see [PERSONAL.md](PERSONAL.md).

### Added

- GitHub repo and tag-triggered release workflow (`.github/workflows/release.yml`).
- Main tray icon and title-bar minimize-to-tray button now use charcoal backgrounds with Plex-gold (`#E5A00D`) glyphs.
- Tray context menus for the main tray icon and hidden-window icons now follow the Windows app light/dark theme detected when the tray manager starts; theme changes apply after the next tray manager start.
- Title bar overlay button moved further left to clear apps with a fourth caption control (e.g. Brave), and uses a minimal two-stroke down-chevron glyph.
- README Known Limitations: ghost tray icons after a Task Manager kill, watchdog recovery, and UWP suspension/termination while hidden.

### Added

- Opt-in **G Home** persistent tray mode for the Google Home Brave PWA (personal build only): identify the exact window, keep a dedicated tray icon, toggle show/hide from the icon or G Home-only title-bar button, and manage the window from the main tray **G Home** submenu.
- Per-row "Auto-hide" button under Hidden windows that creates a process-name auto-hide rule for that window.

## [0.1.0] - 2026-08-12

### Added

- Core minimize-to-tray: hide any eligible top-level window with `ShowWindow(SW_HIDE)` and restore it later; eligibility guards protect the desktop, taskbar, and RexHides itself.
- Dynamic per-window tray icons: each hidden window gets a `NotifyIcon` with the application's own icon (WM_GETICON -> class icon -> exe icon -> shell icon fallback chain). Left-click restores; context menu offers Restore, Close Window, Restore All.
- Customizable global hotkey (default `Ctrl+Alt+H`) via `RegisterHotKey` with live re-mapping and conflict detection.
- Auto-hide rules engine driven by `SetWinEventHook(EVENT_OBJECT_SHOW)`: match on process name or title wildcard, once-per-window or every-show, with manual-restore protection.
- Experimental title bar overlay button that floats a tray-minimize control over the active window's caption area (non-activating overlay; no process injection).
- Opt-in elevation: "Restart as Administrator" with single-instance handoff, plus an optional Task Scheduler task for prompt-free elevated autostart.
- Crash safety: hidden windows are journaled to `%AppData%\RexHides\hidden-windows.json`, restored on exit, and recovered on the next launch after a crash.
- Settings window (WPF, CommunityToolkit.Mvvm): hidden-window list, rules editor with running-window picker, hotkey capture box, autostart options.
