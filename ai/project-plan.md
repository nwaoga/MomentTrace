# MomentTrace Project Plan

## Product

MomentTrace is a Windows-first, local-first desktop app that builds a private memory timeline from foreground apps, websites, screenshots, and documents.

## Intent

Users should be able to quickly revisit what happened during a day, recover past work, inspect meaningful moments, and keep control of all captured data locally.

## Delivery Approach

- Prefer small, incremental stories and commits.
- Keep stable project docs in `README.md`.
- Keep active planning, continuity, and agent handoff docs in `ai/`.
- Keep platform-neutral timeline and overview shaping in `MomentTrace.Presentation`.
- Keep platform-specific capture and startup behavior in platform projects.
- Use Azure DevOps project `blakboi/MomentTrace` for work items.
- Use GitHub `main` for pushed work after user approval.

---

## Current State (2026-07-01 23:50 UTC+1)

**Phase:** Late-alpha hardening and polish.

**Working today:**

- Avalonia desktop shell with Timeline and Overview.
- Overview daily story dashboard with summary cards, heatmap, Moments Today, activity breakdown, and focus rhythm.
- Overview drill-ins: heatmap range focus, moment timeline navigation, direct open, and pin actions.
- Left rail simplified into primary workspace views, Library filters, and Source filters.
- Windows custom chrome with resize handles and no duplicate native caption buttons.
- Immediate local mutation refresh across Timeline, Overview, detail pane, and Library filters.
- Detail-pane note editor now uses a theme-safe dark surface with readable text and focus states.
- Explorer folder moments capture and reopen the captured folder path.
- README preview artwork refreshed with sanitized SVG mockups for the current shell, Overview dashboard, timeline/detail pane, and theme collage.
- Shared presentation builders for timeline, overview, and detail context.
- Local SQLite activity store and Windows foreground recorder.

**Last pushed:** `090703d` — record stability QA sweep notes.

---

## Pick Next Work

Choose one track below. Each is scoped for a single focused agent session or small PR series. Update `stories.md`, `decisions.md`, and Azure DevOps when done.

### Option A — Immediate Refresh Pass

**Why:** Mutations should feel instant and trustworthy.

**Status:** Completed 2026-06-27 09:28 UTC+1.

**Scope:**

- Pin, bookmark, note, delete, and view-mode changes update Timeline and Overview immediately.
- Detail pane action labels and active styles stay in sync.
- Library filters update after metadata-only changes.
- Cache signatures do not skip updates for pin/bookmark/note mutations.

**Key files:**

- `MomentTrace.Desktop/MainWindow.axaml.cs`
- `MomentTrace.Presentation/TimelinePresentationBuilder.cs`
- `MomentTrace.Presentation/OverviewPresentationBuilder.cs`
- `MomentTrace.Storage/`

**Acceptance criteria:**

- Pin/unpin from detail pane updates the selected card, Overview moments, and Library filters without stale UI.
- Bookmark/note changes immediately affect Saved Moments, Notes, and Bookmarks views.
- Delete removes the moment from Timeline, Overview, detail pane, and source filters without requiring a full relaunch.
- Switching card/list view does not leave stale selection/detail state.

---

### Option B — Stability QA Sweep

**Why:** Recent shell work touched layout, chrome, navigation, and Overview interactions.

**Status:** Completed 2026-07-01 23:34 UTC+1 on Windows. macOS shell verification remains tracked separately under Option D.

**Scope:**

- Verify empty, sparse, normal, and dense days.
- Check Timeline, Overview, Library filters, Source filters, detail pane, and Settings.
- Verify Windows custom chrome: resize handles, maximize/restore, drag regions, no duplicate native buttons.
- Re-run app relaunch/single-instance behavior.
- Capture and document regressions in `stories.md`.

**Acceptance criteria:**

- QA checklist in `stories.md` is updated with pass/fail notes.
- Any confirmed regression has a concrete follow-up story.
- No blocking shell regressions remain before the next feature slice.

**Progress so far:**

- Build verification passed on Windows.
- Single-instance relaunch behavior passed.
- Timeline, Overview, Settings, and Library/Source filter coverage passed on the current local dataset.
- Windows custom chrome verification passed for thick-frame resize capability, maximize/restore, absence of native caption/sysmenu chrome over the custom shell controls, and manual toolbar drag-region movement.

---

### Option C — README Preview Refresh

**Why:** The visible shell changed materially: left rail cleanup, Overview actions, and Windows chrome.

**Status:** Completed 2026-07-01 23:50 UTC+1.

**Scope:**

- Refresh sanitized screenshots/assets under `docs/assets/`.
- Update README image captions only if needed.
- Keep private activity out of screenshots.

**Acceptance criteria:**

- README preview reflects current left rail, Overview dashboard, and shell chrome.
- Screenshots are sanitized and safe to publish.

---

### Option D — macOS Shortcut Verification

**Why:** `Cmd+,` remains the known macOS shell bug.

**Handover:** See `ai/macos-handover.md` before pulling on the Mac.

**Scope:**

- Pull latest `main` on macOS.
- Build/run `MomentTrace.Desktop`.
- Re-test `Cmd+,`, `Cmd+F`, `Cmd+1`, `Cmd+2`, and `Cmd+W`.
- Update `decisions.md` and README macOS validation notes if behavior changes.

**Acceptance criteria:**

- `Cmd+,` status is re-verified on a real Mac.
- Any fix or remaining regression is documented.

---

## Recommended Priority

1. **Option D** — macOS shortcut verification.

Completed:

- [2026-06-27 09:28 UTC+1] **Option A** — Immediate refresh pass.
- [2026-07-01 23:34 UTC+1] **Option B** — Stability QA sweep on Windows.
- [2026-07-01 23:50 UTC+1] **Option C** — README preview refresh.

---

## Agent Handoff Checklist

When starting a session, read:

1. `ai/project-plan.md` — pick a track.
2. `ai/architecture.md` — understand code layout and constraints.
3. `ai/stories.md` — mark the active story.
4. `ai/decisions.md` — check recent decisions and log non-obvious choices.
5. `README.md` — stable build/run and product context.

When finishing:

- Run the narrowest useful build/test command.
- Update `stories.md` and `decisions.md`.
- Update Azure DevOps if the task maps to a work item.
- Commit and push only when the user asks.
