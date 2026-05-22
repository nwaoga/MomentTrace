# MomentTrace

MomentTrace is a Windows-first desktop app that builds a private memory timeline of your computer activity. It helps you revisit and recover past work by grouping foreground apps, websites, screenshots, and documents into meaningful local "moments" instead of showing a raw stream of process events.

The long-term goal is a cross-platform desktop timeline for Windows and macOS. The current implementation focuses on a shippable Windows MVP while keeping the core model and storage code separate from Windows-specific integrations.

## Preview

![MomentTrace dark card timeline](docs/assets/momenttrace-dark-card-timeline.png)

MomentTrace now uses a dark Windows Timeline-inspired workspace: left navigation, date and type filters, grouped horizontal moment sections, compact cards with app icons or website screenshots, and a right-hand detail rail. The window has custom chrome with draggable top regions across the main shell, left rail, and details rail, plus minimize and close-to-tray controls.

![MomentTrace timeline activity list](docs/assets/momenttrace-timeline-detail.png)

The recorder is session based. It polls the foreground window, keeps the same activity open while the app/window/resource remains active, and collapses repeated sequential entries in the UI so noisy history reads as one continuous moment. Rule-based local classification then groups nearby activity into memory-oriented sessions such as Website browsing, Development work, Communication, Document review, Research session, and Screenshot capture.

When browser activity is detected, MomentTrace attempts to capture the active URL with Windows UI Automation and saves a local PNG screenshot only when a new browser session starts.

The **Overview** page gives a lightweight analytics view of the selected day without requiring AI features: summary cards, a 15-minute activity heatmap, top applications, a focus rhythm chart, and day metadata.

## Current Features

- Native Windows desktop UI built with WPF
- Dark grouped timeline with card and list view modes
- Overview page with daily activity heatmap and lightweight productivity analytics
- Memory-oriented session grouping into moments rather than raw activity rows
- Rule-based local activity classification for browsing, development, communication, documents, research, and screenshot capture
- Foreground app/window activity recording with session collapsing
- Start recording when the app launches
- Optional "Start with Windows" setting
- Minimize-to-tray behavior
- Tray menu for open, pause, resume, and exit
- Timestamped activity history
- Local SQLite storage
- Search, activity type filtering, pinning, and detail view
- Date filtering with Today, Yesterday, then concrete activity dates
- Real app icon extraction when executable metadata is available
- Browser URL capture and local screenshot previews
- Adult website privacy filter with built-in starter domains, optional cached blocklist updates, and manual allow/block lists
- Single-instance behavior that brings the running app back when launched again
- Noise filtering for MomentTrace itself and Windows lock-screen activity
- Settings modal with startup, recording, timeline view mode, and appearance controls
- "Where I Live" diagnostics for install, database, settings, and screenshot locations
- Real alpha transparency control that keeps the dark colour palette while making the app more or less clear
- Optional AI provider abstraction for future summaries and insights

## Project Structure

```text
MomentTrace.slnx
├─ MomentTrace.App       WPF app shell, UI, tray icon, settings wiring
├─ MomentTrace.Core      Shared models, interfaces, local classification/search/session services
├─ MomentTrace.Storage   SQLite persistence
└─ MomentTrace.Windows   Windows foreground recorder and startup registration
```

This split is intentional. Future macOS support should add platform-specific services beside `MomentTrace.Windows` while reusing `MomentTrace.Core` and `MomentTrace.Storage`.

## Requirements

- Windows 10 or later
- .NET SDK 8 or later
- Visual Studio 2022 with the **.NET desktop development** workload, or the `dotnet` CLI

## Build

From the repository root:

```powershell
dotnet build .\MomentTrace.slnx
```

## Run During Development

Run the desktop app:

```powershell
dotnet run --project .\MomentTrace.App\MomentTrace.App.csproj
```

Launch minimized to the tray:

```powershell
dotnet run --project .\MomentTrace.App\MomentTrace.App.csproj -- --minimized
```

In Visual Studio, open `MomentTrace.slnx`, set `MomentTrace.App` as the startup project, and press `F5`.

## Local Data

MomentTrace stores user data locally:

```text
%AppData%\MomentTrace\timeline.db
%AppData%\MomentTrace\settings.json
%AppData%\MomentTrace\screenshots\
%AppData%\MomentTrace\privacy-blocklists\
```

Older prototype data from `%AppData%\NwaogaTimeline` is copied into the new MomentTrace folder the first time the renamed app runs.

The app currently keeps captured history on the local machine. The database, screenshots, settings, and cached privacy blocklists are intentionally stored under the user's AppData folder so privacy controls can delete, export, or exclude activity without depending on a cloud service.

## Startup Behavior

The in-app **Start with Windows** checkbox writes a per-user startup entry to:

```text
HKCU\Software\Microsoft\Windows\CurrentVersion\Run
```

The startup command launches MomentTrace with `--minimized`, so recording starts quietly and the app lives in the tray.

MomentTrace also uses a single-instance guard. If the app is already running, launching it again signals the existing instance and brings the current window back instead of opening a duplicate recorder.

## Timeline And Overview

The Timeline page is the memory explorer. It shows moments grouped by `Today`, `Yesterday`, then dated sections, with card and list modes controlled from Settings.

Cards are organized into semantic sessions using deterministic local rules. For example, nearby browser pages can become `Website browsing` or `Research session`, code editors and terminals become `Development work`, messaging tools become `Communication`, document tools become `Document review`, and Snipping Tool activity becomes `Screenshot capture`.

Large groups show a compact preview first. `View all` expands that session inline and changes to `Show less`, keeping the timeline readable while still allowing detailed recovery.

The Overview page is the activity analytics view. It summarizes the selected date with:

- Total active time, session count, most-used app, and longest focus session
- A horizontal 15-minute heatmap inspired by GitHub contribution graphs and screen-time tools
- Top applications with duration, session count, percentage of the day, and a simple trend column
- A lightweight focus timeline showing active blocks and gaps
- A right sidebar for day totals, quick filters, and category legend

Overview intentionally avoids raw event lists so it does not duplicate Timeline.

## Local Classification And Search

MomentTrace is designed so AI can be layered in later, but the current MVP is fully local and deterministic. The Core project includes these extension points:

```csharp
IActivityClassifier
ISessionSummarizer
ISearchProvider
IInsightProvider
```

Current implementations are rule-based:

- `RuleBasedActivityClassifier` maps apps, processes, titles, and URLs to semantic categories.
- `RuleBasedSessionSummarizer` gives sessions human-readable names and descriptions.
- `LocalMetadataSearchProvider` searches local metadata, category labels, titles, apps, and URLs.
- `LocalInsightProvider` returns simple local-only metadata insights.

There are no LLM, embedding, vector database, or cloud dependencies in the app today.

## Recording Model

The Windows recorder lives in `MomentTrace.Windows` and currently:

- Polls the foreground window every few seconds
- Opens a new timeline activity when the active app, title, or browser URL changes
- Extends the current activity while the same item remains active
- Throttles duration updates to reduce database writes
- Captures browser screenshots only when a new browser session starts
- Closes the active session cleanly when recording stops or the app exits
- Excludes MomentTrace itself and default Windows lock-screen activity from new recordings
- Excludes configured adult websites before saving activity or capturing screenshots

Existing stored MomentTrace and lock-screen rows are also filtered out of the Timeline and Overview views without requiring a database cleanup.

## Privacy Controls

MomentTrace treats privacy as part of the product experience:

- Activity history is stored locally in SQLite.
- Screenshots are stored locally under AppData.
- Adult website filtering happens before data is saved or screenshotted.
- Manual always-block and always-allow domain lists are available in Settings.
- Optional adult blocklist updates are cached locally under `%AppData%\MomentTrace\privacy-blocklists\`.
- Timeline and Overview hide matching privacy-filtered rows from existing history without deleting the database.

## AI Extensibility

MomentTrace has an optional `IAiProvider` abstraction in `MomentTrace.Core` for future features:

```csharp
Task<string> GetDailySummaryAsync(IReadOnlyList<TimelineActivity> activities, CancellationToken cancellationToken = default);
Task<IReadOnlyList<string>> GetProductivityInsightsAsync(IReadOnlyList<TimelineActivity> activities, CancellationToken cancellationToken = default);
```

No AI provider is registered today. AI summaries, recommendations, and conversational UI must only render when an AI provider exists. Without a provider, the app should feel complete and simply hide those surfaces. Current classification, session naming, search, and local insights are deterministic.

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

The published executable is written to:

```text
publish\win-x64\MomentTrace.App.exe
```

## CI And Releases

GitHub Actions are used for build checks and downloadable releases:

- `.github/workflows/ci.yml` builds the solution on pushes and pull requests to `main`.
- `.github/workflows/release.yml` creates a versioned Windows zip and attaches it to a GitHub Release.

To create a release from Git:

```powershell
git tag v0.1.0
git push origin v0.1.0
```

The release workflow validates the `vMAJOR.MINOR.PATCH` tag format, publishes the app as a self-contained Windows `win-x64` build, stamps the assembly/file version, creates a zip named like `MomentTrace-0.1.0-win-x64.zip`, and uploads it to the GitHub Release.

You can also run the **Release** workflow manually in GitHub Actions and provide a version such as `v0.1.0`.

For Azure DevOps planning, current implementation work is tracked in the MomentTrace project and commits can reference work items with `AB#<id>` in the commit message.

## Development Notes

- Keep platform-neutral logic in `MomentTrace.Core` and `MomentTrace.Storage`.
- Keep Windows API, registry, tray, and executable-icon behavior in `MomentTrace.Windows` or `MomentTrace.App`.
- Keep optional AI features behind `IAiProvider`; do not show AI panels when no provider is registered.
- Keep captured user data local-first unless a future feature explicitly asks for sync.
- Do not commit `bin`, `obj`, `.vs`, `publish`, or local user settings.
- Treat privacy as a product feature: activity history is local-first, and future recording controls should make data capture obvious and user-controlled.

## Roadmap

- Add clickable category/source filters for the new navigation sections
- Add saved moments, notes, and bookmarks as real library features
- Add richer website/app card previews and stronger related activity grouping
- Add privacy controls for deleting, exporting, and excluding activity
- Create a proper Windows installer with icon, shortcuts, and uninstall support
- Plan the cross-platform desktop path, likely with Avalonia for Windows/macOS UI reuse
