# RexHides Installation and Build

## Download (public build)

1. Open [GitHub Releases](https://github.com/reptar-rex-u/RexHides/releases).
2. Download `RexHides-{version}-win-x64.zip`.
3. Extract `RexHides.exe` and run it.

The release build is **self-contained** — Windows 10/11 x64 only; no separate .NET install required.

To start in the tray only:

```powershell
RexHides.exe --minimized
```

To stop a running instance gracefully:

```powershell
RexHides.exe --stop
```

## Requirements (from source)

- Windows 10 or Windows 11, x64
- .NET 8 SDK

## Build

```powershell
cd "R:\RexApps\5. Windows Apps\RexHides"
dotnet build RexHides.sln
```

Public build (default). For the personal G Home build, add `-p:EnableGHome=true` (see [PERSONAL.md](PERSONAL.md)).

## Run from source

```powershell
dotnet run --project src\RexHides
```

## Publish locally

**Public shareable build** (self-contained single-file exe + zip):

```powershell
.\scripts\publish-public.ps1
```

**Personal build** (G Home enabled, framework-dependent — for RexControls):

```powershell
.\scripts\publish-personal.ps1
```

## Release process (maintainers)

1. Bump `<Version>` in `src/RexHides/RexHides.csproj` and update `CHANGELOG.md`.
2. Commit and push to `main`.
3. Tag and push:

```powershell
git tag v0.1.0
git push origin main --tags
```

GitHub Actions builds `RexHides-{version}-win-x64.zip` and attaches it to the release.
