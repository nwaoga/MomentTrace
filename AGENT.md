# MomentTrace Agent Handoff

This file is a lightweight handoff for any future agent working in this repo.

## Repo

- Active workspace: `D:\source\repos\MomentTrace`
- Main branch was used for the latest work
- Latest known pushed commit: `eec35ab`
- Latest known pushed commit message: `Polish overview sparse states and refresh README`

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

The most recent completed pass focused on overview empty states and low-data behavior.

Completed:

- Added designed empty states for overview cards and rails when a day has no captured moments
- Improved low-activity layout behavior so sparse days still look intentional
- Made charts and summary controls degrade more gracefully with tiny datasets
- Tightened `Moments today` behavior for very small result sets
- Reduced awkward oversized presentation in `Focus rhythm` for sparse data
- Updated `README.md` to reflect the current overview work and added more screenshots

Key files touched in that pass:

- `MomentTrace.Desktop/MainWindow.axaml`
- `MomentTrace.Desktop/MainWindow.axaml.cs`
- `MomentTrace.Desktop/Controls/ActivityHeatmapControl.axaml`
- `MomentTrace.Desktop/Controls/ActivityHeatmapControl.axaml.cs`
- `MomentTrace.Desktop/Controls/CategoryBreakdownControl.axaml`
- `MomentTrace.Desktop/Controls/CategoryBreakdownControl.axaml.cs`
- `MomentTrace.Desktop/Controls/FocusRhythmControl.axaml`
- `MomentTrace.Desktop/Controls/FocusRhythmControl.axaml.cs`
- `README.md`

## Decisions Already Made

- The active source of truth is this repo at `D:\source\repos\MomentTrace`, not the abandoned `nwaoga's TimeLine` folder
- The Avalonia desktop shell in `MomentTrace.Desktop` is the main UI surface to prioritize for polish work
- Sparse and zero-data days should feel deliberately designed, not merely less populated
- Overview components should shrink, simplify, or hide extra chrome when datasets are tiny rather than preserving full-density layouts
- Privacy and history remain local-first; no cloud dependency is required for the core product experience
- Codex conversation history is treated as local tooling state, not project source material, and is not expected to be versioned in Git
- Old workspace screenshots may be reused as reference assets, but old code and old planning notes are not considered authoritative
- README updates are appropriate when the visible product surface materially changes, especially when screenshots or UX behavior shift
- The natural next product pass after overview sparse-state work is interaction QA and polish across Overview and Timeline, not a broad redesign

## Current Status

- Build status after the overview pass: `dotnet build MomentTrace.Desktop\\MomentTrace.Desktop.csproj` succeeded
- The overview sparse-state work was committed and pushed
- The current repo has one known uncommitted item: `docs/assets/archive/`

## Uncommitted / Loose Artifacts

`docs/assets/archive/` contains copied screenshots from an old abandoned workspace. These were pulled in for reference/archive purposes and were not part of the last pushed commit.

If you keep them:

- decide whether they should be referenced from the README or kept as historical assets only
- stage and commit them intentionally

If you do not need them:

- leave them untracked or remove them manually after confirming they are no longer useful

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

The next likely product pass after the overview sparse-state work is:

1. Final interaction QA / polish sweep across Overview and Timeline
2. Verify empty, sparse, normal, and dense dataset behavior side by side
3. Check selection states, focus ranges, card expansion, rail filtering, and detail panel transitions
4. Run another README/screenshots pass only if UI changed materially

## Good First Checks For The Next Agent

1. Run `git status --short`
2. Decide what to do with `docs/assets/archive/`
3. Run `dotnet build MomentTrace.Desktop\\MomentTrace.Desktop.csproj`
4. If UI work continues, inspect `MainWindow.axaml`, `MainWindow.axaml.cs`, and `MomentTrace.Presentation/OverviewPresentationBuilder.cs`
5. If macOS work resumes, review `docs/macos-spike.md` and `MomentTrace.MacProbe`

## Notes For Another Model

- Prefer preserving the current visual direction rather than redesigning from scratch
- The desktop shell is the main surface to prioritize
- Be careful not to treat the old `TimeLine` folder as the source of truth
- Do not assume Codex history is in Git; it lives under the user profile `.codex` folder
