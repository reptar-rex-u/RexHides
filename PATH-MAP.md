# PATH-MAP — RexHides

## Identity

- Root: `R:\RexApps\RexHides`
- Type: Windows .NET 8 WPF tray utility
- No local HTTP service or cross-project API

## Connections

| ID | Kind | Value / contract | Defined in | Also update |
|---|---|---|---|---|
| `rexhides.release` | github | https://github.com/reptar-rex-u/RexHides/releases — `RexHides-{version}-win-x64.zip` (self-contained) | `.github/workflows/release.yml` | README, Install.md |
| `rexhides.settings` | disk | `%AppData%\RexHides\settings.json` | app settings service | README |
| `rexhides.hotkey` | input | Default `Ctrl+Alt+H`; configurable in settings | hotkey service | README |
| `rexhides.elevation` | system | Optional elevated restart / Task Scheduler startup | elevation setup | setup docs |
| `rexhides.watchdog` | process | Watchdog restores hidden windows after a force-kill | crash recovery services | README |

## Change checklists

### When build variants or publish outputs change

1. Update README, Install.md, and this map.
2. Release: push `v*` tag → GitHub Actions → `RexHides-{version}-win-x64.zip` on Releases.
3. Local preview: `.\scripts\publish-public.ps1` → `dist\public\` + zip under `dist\`.

### When settings or recovery behavior changes

1. Update this map and README.
2. Document migration or compatibility changes in CHANGELOG.
3. Never store user window data or secrets in documentation.
