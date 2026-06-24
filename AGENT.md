# MomentTrace Agent Handoff

This file is a lightweight handoff for any future agent working in this repo.

## Repo

- Active workspace: `C:\Users\blakb\Documents\Codex\MomentTrace`
- Active branch: `main`
- Current HEAD at handoff time: `b637d18`
- Current HEAD message: `Fix timeline session count badge contrast`

## Product Snapshot

MomentTrace is a local-first activity timeline app. The current repo contains:

- `MomentTrace.Desktop`: Avalonia desktop shell and the main current UI surface
- `MomentTrace.App`: older Windows/WPF shell still present in the repo
- `MomentTrace.Core`: shared domain models, classifiers, privacy rules, summarizers
- `MomentTrace.Presentation`: presentation builders for overview/timeline models
- `MomentTrace.Storage`: SQLite persistence
- `MomentTrace.Windows`: Windows foreground recorder and Windows-specific helpers
- `MomentTrace.MacProbe`: early macOS probe work

## Architecture At A Glance

- Recording flow: platform-specific recorder code captures foreground activity, then shared core/storage layers persist it locally
- Presentation flow: stored activities are transformed into grouped moments and overview models by `MomentTrace.Presentation`
- UI flow: `MomentTrace.Desktop` binds those presentation models into the Avalonia overview/timeline experience

Primary responsibilities by project:

- `MomentTrace.Core`: domain entities, activity kinds/categories, privacy filters, classification rules, session naming contracts
- `MomentTrace.Storage`: local SQLite read/write for captured activity history
- `MomentTrace.Presentation`: transforms raw activities into timeline sessions, overview metrics, focus blocks, category breakdowns, and moment cards
- `MomentTrace.Desktop`: current desktop application shell, controls, screens, and interaction logic
- `MomentTrace.App`: older Windows/WPF shell still useful for reference but not the main polish target
- `MomentTrace.Windows`: Windows activity capture, startup integration, and icon/provider helpers
- `MomentTrace.MacProbe`: exploratory macOS-side capture work, not the main day-to-day product surface

Important cross-project paths:

- Activity capture and persistence start in `MomentTrace.Windows` or `MomentTrace.MacProbe`, then flow through `MomentTrace.Core` into `MomentTrace.Storage`
- Timeline grouping and overview shaping happen in `MomentTrace.Presentation`, especially timeline and overview builders
- Final rendering and interaction state live in `MomentTrace.Desktop`, especially `MainWindow.axaml` and `MainWindow.axaml.cs`

## What Was Just Finished

The most recent completed and in-progress passes focused on overview sparse-state polish, theme expansion, macOS validation, and Windows Avalonia shell recovery.

Completed:

- Added designed empty states for overview cards and rails when a day has no captured moments
- Improved low-activity layout behavior so sparse days still look intentional
- Made charts and summary controls degrade more gracefully with tiny datasets
- Tightened `Moments today` behavior for very small result sets
- Reduced awkward oversized presentation in `Focus rhythm` for sparse data
- Added `Starbucks Reserve` and `Nero Roast` alongside the existing theme set
- Completed real-mac verification for bundle ID capture, `app://...` identity, browser URL capture, screenshot cropping, and reopen targeting
- Recovered the Avalonia Windows shell so main window creation happens before recorder startup and the shell refreshes when new activity arrives
- Updated `README.md` and supporting docs to reflect the current overview, theme, platform, and validation state

Key files touched in these passes:

- `MomentTrace.Desktop/App.axaml.cs`
- `MomentTrace.Desktop/Program.cs`
- `MomentTrace.Desktop/MainWindow.axaml`
- `MomentTrace.Desktop/MainWindow.axaml.cs`
- `MomentTrace.Desktop/Controls/SummaryMetricCard.axaml`
- `MomentTrace.Desktop/Controls/SummaryMetricCard.axaml.cs`
- `MomentTrace.Desktop/Controls/ActivityHeatmapControl.axaml`
- `MomentTrace.Desktop/Controls/ActivityHeatmapControl.axaml.cs`
- `MomentTrace.Desktop/Controls/CategoryBreakdownControl.axaml`
- `MomentTrace.Desktop/Controls/CategoryBreakdownControl.axaml.cs`
- `MomentTrace.Desktop/Controls/FocusRhythmControl.axaml`
- `MomentTrace.Desktop/Controls/FocusRhythmControl.axaml.cs`
- `MomentTrace.Desktop/DesktopAppThemePalette.cs`
- `README.md`
- `docs/macos-spike.md`

## Decisions Already Made

- The active source of truth is this repo at `C:\Users\blakb\Documents\Codex\MomentTrace`, not the abandoned `nwaoga's TimeLine` folder
- The Avalonia desktop shell in `MomentTrace.Desktop` is the main UI surface to prioritize for polish work
- Sparse and zero-data days should feel deliberately designed, not merely less populated
- Overview components should shrink, simplify, or hide extra chrome when datasets are tiny rather than preserving full-density layouts
- The Windows Avalonia shell should surface the main window immediately, even if recorder or database initialization is slow
- Single-instance reopen behavior matters because the app is expected to live in the background while still being easy to summon back
- Privacy and history remain local-first; no cloud dependency is required for the core product experience
- Codex conversation history is treated as local tooling state, not project source material, and is not expected to be versioned in Git
- Old workspace screenshots may be reused as reference assets, but old code and old planning notes are not considered authoritative
- README updates are appropriate when the visible product surface materially changes, especially when screenshots or UX behavior shift
- The natural next product pass after overview sparse-state work is interaction QA and polish across Overview and Timeline, not a broad redesign
- The remaining known macOS UX bug is still `Cmd+,` from the main shell; do not assume it is fixed unless re-verified on a Mac

## Current Status

- Build status after the Windows shell recovery pass: `dotnet build MomentTrace.Desktop\\MomentTrace.Desktop.csproj -f net8.0-windows` succeeded
- The local shell was manually relaunched and confirmed to expose a real `MomentTrace` window handle on Windows again
- `%APPDATA%\\MomentTrace\\timeline.db` updated while the recovered shell was running, indicating recorder activity was flowing again
- There are active uncommitted desktop-shell changes in:
  - `MomentTrace.Desktop/App.axaml.cs`
  - `MomentTrace.Desktop/Controls/SummaryMetricCard.axaml`
  - `MomentTrace.Desktop/Controls/SummaryMetricCard.axaml.cs`
  - `MomentTrace.Desktop/MainWindow.axaml`
  - `MomentTrace.Desktop/MainWindow.axaml.cs`
  - `MomentTrace.Desktop/Program.cs`

## Stability Punch List

This is the current hardening list, ordered by practical risk:

1. Windows startup and window surfacing:
   - make first launch, relaunch, and single-instance reopen behavior deterministic
   - remove cases where the process is alive but the shell is not obviously surfaced to the user
2. Immediate UI consistency after local mutations:
   - pin, bookmark, note, delete, and library-filter changes should redraw Timeline, Overview, and detail state immediately
   - avoid cache-signature shortcuts that skip metadata-only edits
3. Interaction QA across data densities:
   - verify empty, sparse, normal, and dense days in both Timeline and Overview
   - check rail filters, detail pane state, focus ranges, card expansion, and view switching
4. macOS shell verification:
   - re-test shell shortcuts after meaningful UI work
   - `Cmd+,` from the main shell is still the main known regression until proven fixed
5. Lightweight regression routine before push:
   - launch the shell
   - confirm new activity appears
   - mutate a moment with pin/bookmark/note
   - switch views and confirm the UI stays in sync
   - reopen the app through the single-instance path

## Uncommitted / Local Focus

The current local worktree includes real in-progress shell work:

- Windows single-instance and show-existing-window guard in `Program.cs` and `App.axaml.cs`
- Main-window-first startup ordering so the shell does not stay hidden while background initialization runs
- Recorder-driven UI refresh wiring in `MainWindow.axaml.cs`
- Ongoing shell/nav/detail-pane polish in `MainWindow.axaml` and `MainWindow.axaml.cs`
- Quiet-value metric styling in `SummaryMetricCard.axaml` and `SummaryMetricCard.axaml.cs`

Treat these as intentional in-progress product work, not disposable scratch changes.

## Old Workspace Findings

An older folder was inspected at:

- `C:\Users\blakb\OneDrive\Documents\nwaoga's TimeLine`

Important conclusions:

- it was an abandoned/older parallel workspace
- it did not contain useful saved Codex chat history
- the code there was outdated relative to this repo
- the only worthwhile reusable assets found there were a few old theme screenshots

## Codex History Location

Codex conversation history is not stored in this repo by default.

Relevant local paths:

- `C:\Users\blakb\.codex\sessions`
- `C:\Users\blakb\.codex\archived_sessions`
- `C:\Users\blakb\.codex\session_index.jsonl`

This means normal `git add/commit/push` from this repo does not push Codex chat history unless someone manually copies artifacts into the repo.

## Suggested Next Work

The next likely product pass is:

1. Finish the current Avalonia shell polish pass and commit the Windows startup/refresh recovery with the surrounding UI work
2. Work through the stability punch list from startup/reopen first, then immediate mutation refresh, then interaction QA
3. Re-verify the macOS shell shortcuts, especially `Cmd+,`, after the next shell pass lands
4. Refresh README screenshots only if the visible shell changed materially again

## Good First Checks For The Next Agent

1. Run `git status --short`
2. Run `dotnet build MomentTrace.Desktop\\MomentTrace.Desktop.csproj -f net8.0-windows`
3. Launch the Avalonia shell and confirm the window appears and timeline activity refreshes live
4. If UI work continues, inspect `MainWindow.axaml`, `MainWindow.axaml.cs`, and `MomentTrace.Presentation/OverviewPresentationBuilder.cs`
5. If macOS work resumes, review `docs/macos-spike.md`, the README mac verification notes, and `MomentTrace.MacProbe`

## Notes For Another Model

- Prefer preserving the current visual direction rather than redesigning from scratch
- The desktop shell is the main surface to prioritize
- Be careful not to treat the old `TimeLine` folder as the source of truth
- Do not assume Codex history is in Git; it lives under the user profile `.codex` folder
