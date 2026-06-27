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

## Recently Completed

- Closed `AB#63` and `AB#87`: timeline card intelligence and visual polish.
- Closed `AB#88` and `AB#92`: Overview daily story dashboard.
- Closed `AB#89`: Avalonia shell alignment, spacing, and image framing.
- Fixed sidebar label truncation caused by nav label panels sitting in the icon column.
- Removed redundant `Browse`, `Search`, and `Insights` left-rail items.
- Restored Windows edge/corner resize while preventing native caption buttons from reappearing.
- Split active planning content out of `README.md` into the repo-local `ai/` structure.
- [2026-06-27 09:28 UTC+1] Completed the immediate refresh pass for pin, bookmark, note, delete, and timeline view-mode changes.
- [2026-06-27 09:28 UTC+1] Fixed Explorer folder moments so new captures reopen the captured folder path instead of bare Explorer.

## Open Decisions To Revisit

- Whether `AGENT.md` should remain as a root-level compatibility handoff or become a short pointer to `ai/`.
- Whether the macOS probe should graduate into a first-class `MomentTrace.Mac` platform project.
- Whether to add automated UI/presentation tests before the broader hardening pass.
