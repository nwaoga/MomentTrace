# macOS Technical Spike

This note captures the first macOS implementation plan for MomentTrace. The goal is to prove the recorder and permissions model before moving the UI to a cross-platform shell.

## Decision

Use an Avalonia desktop shell for the eventual cross-platform UI, while keeping OS integrations behind platform services:

- `MomentTrace.Core`: shared models, classification, sessions, search, AI interfaces.
- `MomentTrace.Storage`: shared SQLite persistence and migrations.
- `MomentTrace.Desktop`: future Avalonia UI.
- `MomentTrace.Windows`: Windows recorder, startup, tray, screenshot, URL capture.
- `MomentTrace.Mac`: macOS recorder, login item, menu bar, screenshot, URL capture.

Do not start by porting the full UI. Start with a macOS recorder probe that writes rows through `MomentTrace.Storage`, then display those rows in a thin Avalonia shell.

## macOS Recorder MVP

Minimum viable capture:

- Active app name.
- Process identifier and bundle identifier.
- Window title when permission allows.
- Started/ended timestamps.
- Local SQLite write through the existing store.

Recommended approach:

1. Use `NSWorkspace.frontmostApplication` and `NSWorkspace.didActivateApplicationNotification` to detect active app changes.
2. Poll the foreground app periodically, similar to the Windows recorder, so title changes within the same app can still produce moment boundaries.
3. Use Accessibility APIs (`AXUIElement`) only for window title and richer window metadata.
4. Treat screenshot capture as a later permission-gated layer using ScreenCaptureKit.

## Permissions

macOS will require explicit user trust for the features MomentTrace needs:

- Accessibility: needed for reading active window metadata through the Accessibility API.
- Screen Recording / Screen & System Audio Recording: needed for screenshots or any screen capture.
- Automation / Apple Events: likely needed if browser URL capture relies on scripting browsers.
- Login Items: needed for start-at-login behavior.

The app should surface these as first-run setup steps rather than surprising prompts. A Mac MVP can work in a reduced mode without screenshots or browser URLs: app name, process/bundle ID, timestamps, and window title when allowed.

## Browser URL Capture

Browser URL capture is the least certain part of the Mac plan.

Likely options:

- Apple Events / scripting for Safari, Chrome, and Edge.
- Accessibility fallback for visible address bar text, if allowed.
- No URL capture in the first Mac MVP; classify as app/browser activity and add URLs later.

Recommendation: do not block the Mac MVP on URLs. Prove app/window recording first.

## Screenshots

Use ScreenCaptureKit for future screenshot capture. Apple documents ScreenCaptureKit as the framework for capturing displays, apps, and windows, and its sample notes that the system prompts for Screen Recording permission the first time capture runs. Screenshots should remain local under the MomentTrace app data folder.

## Startup And Menu Bar

Use `SMAppService` for login item/start-at-login behavior on macOS 13 and later. Apple documents it as the replacement path for registering login items and launch agents. The tray equivalent should be a macOS menu bar/status item, not a Windows-style tray clone.

## How We Will See It Working

With a Mac linked to Visual Studio, there are two useful test loops:

1. Windows-hosted development with Mac target:
   - Build from Visual Studio.
   - Run the Mac project on the linked Mac.
   - Use the Mac desktop to grant permissions and inspect live behavior.

2. Direct Mac CLI loop:
   - Clone the repo on the Mac.
   - Run the recorder probe or Avalonia shell with `dotnet run`.
   - Inspect `%AppData%` equivalent under the user Library/Application Support path.

The first visible milestone should be a simple Mac recorder probe showing rows in SQLite. The second should be a thin Avalonia UI showing those rows as MomentTrace moments.

## Current Probe

The repository now includes `MomentTrace.MacProbe`, a minimal .NET console recorder that runs only on macOS and reuses the existing shared store.

Run it on a Mac from the repository root:

```bash
dotnet run --project ./MomentTrace.MacProbe/MomentTrace.MacProbe.csproj
```

The probe writes to the same SQLite schema as the Windows app. It captures active app name, process id as a local resource marker, active window title when macOS permissions allow it, and start/end timestamps.

It is intentionally not the final macOS implementation. It uses `osascript` and System Events as a fast proof path so we can validate permissions, storage, grouping, and developer workflow before replacing the capture layer with native AppKit/Accessibility bindings in `MomentTrace.Mac`.

## Open Questions

- Which macOS versions do we support for the first Mac preview?
- Do we require Accessibility permission on first launch, or allow reduced app-only capture first?
- Should screenshot capture be opt-in per theme/privacy setup?
- Is browser URL capture required for the first Mac MVP, or a second milestone?
- Will the first Mac build be Developer ID distributed outside the App Store?

## Source Notes

- Apple `NSWorkspace.frontmostApplication` returns the app receiving key events.
- Apple `NSWorkspace.didActivateApplicationNotification` provides activation events from the shared workspace notification center.
- Apple Accessibility APIs expose app UI elements through `AXUIElement`.
- Apple ScreenCaptureKit captures displays, apps, and windows and prompts for Screen Recording permission on first use.
- Apple `SMAppService` registers login items and launch agents on macOS 13 and later.
