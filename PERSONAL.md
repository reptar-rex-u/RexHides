# RexHides Personal Build (G Home)

This build includes the opt-in **G Home** persistent tray mode for the Google Home Brave PWA. It is compiled from the same repository as the public build, with `EnableGHome=true`.

## Build

```powershell
dotnet build RexHides.sln -p:EnableGHome=true
dotnet run --project src\RexHides -p:EnableGHome=true
```

## Publish

```powershell
.\scripts\publish-personal.ps1
```

Output: `dist\personal\RexHides.exe`

## G Home workflow

1. Open the Google Home Brave PWA.
2. Open RexHides settings and go to **G Home**.
3. Focus the Google Home window and click **Find Google Home**, or leave it running (even hidden in the tray) and click **Find Google Home** from RexHides settings.
4. Enable **G Home persistent tray mode**.

After that:

- RexHides tracks the Brave PWA by `home.google.com` in its process command line, not the page title — Devices and other pages still work.
- A dedicated G Home tray icon stays in the notification area.
- Left-click the icon to toggle show/hide.
- Right-click for Show, Hide, Close G Home, or Exit RexHides.
- The global hotkey toggles G Home when that window is focused.
- Optional G Home-only title-bar minimize button (separate from the general title-bar button).

Other Brave windows and PWAs are unaffected — they use the normal temporary per-window tray flow.

Settings for G Home are stored in `%AppData%\RexHides\settings.json` under the `GHome` section (personal builds only).

## Share the public build instead

For distribution without G Home, use the default build (`EnableGHome=false`) or `.\scripts\publish-public.ps1` → `dist\public\`.
