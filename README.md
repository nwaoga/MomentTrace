# MomentTrace

MomentTrace is a Windows-first desktop activity timeline inspired by the spirit of Windows 10 Timeline. It records foreground window activity with timestamps, stores it locally, and keeps running from the system tray.

## Current MVP

- Native WPF desktop app
- Starts recording when the app launches
- Optional "Start with Windows" toggle
- Minimize-to-tray behavior
- Tray menu for open, pause, resume, and exit
- Timestamped activity records
- Local SQLite database at `%AppData%\MomentTrace\timeline.db`
- Search, activity type filter, pinning, and detail view

## Project Layout

- `MomentTrace.App` - WPF shell, tray icon, UI, startup wiring
- `MomentTrace.Core` - timeline models, interfaces, classification
- `MomentTrace.Storage` - SQLite persistence
- `MomentTrace.Windows` - Windows foreground-window recorder and startup registration

The split is intentional. Future macOS/Linux support can add platform-specific recorder projects while keeping the core model and storage layer.

## Build

```powershell
dotnet build MomentTrace.slnx
```

## Run

```powershell
dotnet run --project .\MomentTrace.App\MomentTrace.App.csproj
```

To launch minimized, use:

```powershell
dotnet run --project .\MomentTrace.App\MomentTrace.App.csproj -- --minimized
```

## Publish A Standalone Windows Build

```powershell
dotnet publish .\MomentTrace.App\MomentTrace.App.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o .\publish\win-x64
```

The published executable will be written to `publish\win-x64`.

## Startup Behavior

The in-app "Start with Windows" checkbox writes a per-user startup command to:

`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`

It launches the app with `--minimized`, so recording starts quietly and the app lives in the tray.
