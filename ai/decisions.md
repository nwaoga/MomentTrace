# MomentTrace Decisions

## 2026-06

- The source of truth is `D:\source\repos\MomentTrace` / `C:\Users\blakb\Documents\Codex\MomentTrace`.
- The Avalonia shell in `MomentTrace.Desktop` is the main UI surface for current polish work.
- `MomentTrace.App` remains in the repo as the older WPF Windows shell and reference surface, but it is not the main UI target.
- Planning and continuity docs live under `ai/` following the NthTerm structure.
- `README.md` stays focused on stable product, build, architecture, and release documentation.
- `ai/project-plan.md` owns the active roadmap, Azure DevOps mapping, QA checklist, and stability punch list.
- The app remains local-first: activity history, screenshots, settings, and privacy blocklists stay on the user's machine.
- AI features stay behind `IAiProvider`; no AI surface should render when no provider exists.
- Overview should feel like a visual daily story rather than a technical telemetry dashboard.
- Timeline and Overview should share presentation logic through `MomentTrace.Presentation`, not duplicate grouping rules in the shell.
- Sparse and zero-data days should feel intentionally designed, not merely empty.
- Windows custom chrome should use `ExtendClientAreaToDecorationsHint=True` with `WindowDecorations=BorderOnly` so resize handles work without duplicate native caption buttons.
- The left rail should avoid redundant navigation. Workspace contains primary views, Library contains saved-memory filters, and Sources contains source filters.
- The remaining known macOS UX bug is `Cmd+,` not reliably opening Settings from the main shell.
- Codex/Cursor conversation history is local tooling state and should not be assumed to live in Git.
- [2026-06-27 09:28 UTC+1] Local shell mutations should invalidate Timeline, Overview, detail, and rail caches together, and reload requests made during an active reload should queue one follow-up pass.
- [2026-06-27 09:28 UTC+1] Windows Explorer folder moments should persist the captured folder path in `TimelineActivity.Resource`; the desktop opener should not fall back to launching generic Explorer when no folder path can be resolved.
- [2026-06-27 10:05 UTC+1] Timeline and Overview refresh signatures should include per-moment saved-state metadata so pin, bookmark, and note mutations cannot be skipped as cache-equivalent UI states.
- [2026-06-27 18:40 UTC+1] The detail-pane note editor should use a dedicated dark-surface textbox style so Avalonia template internals cannot fall back to a light background and low-contrast text.
- [2026-07-03 16:40 UTC+1] Accent foreground text in the desktop theme palette should be chosen by measured contrast against the accent fill rather than a simple brightness threshold so Nero and espresso theme labels stay readable.

## Recently Completed

- Closed `AB#63` and `AB#87`: timeline card intelligence and visual polish.
- Closed `AB#88` and `AB#92`: Overview daily story dashboard.
- Closed `AB#89`: Avalonia shell alignment, spacing, and image framing.
- Fixed sidebar label truncation caused by nav label panels sitting in the icon column.
- Removed redundant `Browse`, `Search`, and `Insights` left-rail items.
- Restored Windows edge/corner resize while preventing native caption buttons from reappearing.
- Split active planning content out of `README.md` into the repo-local `ai/` structure.
- [2026-06-27 09:28 UTC+1] Completed the immediate refresh pass for pin, bookmark, note, delete, and timeline view-mode changes.
- [2026-06-27 18:40 UTC+1] Verified the immediate refresh pass manually and fixed the remaining note-editor contrast clash in the dark shell.
- [2026-07-01 23:34 UTC+1] Windows stability QA sweep is complete for the current shell; macOS shortcut verification remains a separate follow-up on real hardware.
- [2026-07-01 23:50 UTC+1] README preview artwork now uses sanitized SVG mockups for the current Workspace/Library/Sources shell, Overview dashboard, timeline/detail pane, and theme collage.
- [2026-06-27 09:28 UTC+1] Fixed Explorer folder moments so new captures reopen the captured folder path instead of bare Explorer.
- [2026-07-03 16:40 UTC+1] Fixed theme accent label contrast by switching `DesktopAppThemePalette.AccentText` to a contrast-based foreground selection.

## Open Decisions To Revisit

- Whether `AGENT.md` should remain as a root-level compatibility handoff or become a short pointer to `ai/`.
- Whether the macOS probe should graduate into a first-class `MomentTrace.Mac` platform project.
- Whether to add automated UI/presentation tests before the broader hardening pass.
