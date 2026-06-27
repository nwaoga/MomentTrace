# MomentTrace Stories

## Backlog

- [ ] Focused stability QA sweep across Windows and macOS shell interactions.
- [ ] Refresh sanitized README preview artwork for the current Avalonia shell.
- [ ] Re-verify and fix macOS `Cmd+,` Settings shortcut from the main shell.
- [ ] Add lightweight visual QA pass for dark, Starbucks, Nero, Costa, espresso, and light themes.
- [ ] Add regression tests for privacy exclusions, settings persistence, and warning-free build expectations.
- [ ] Add a Settings option to delete local data when the user wants full privacy cleanup.
- [ ] Add first-run onboarding for privacy, startup behavior, and local storage.
- [ ] Replace `MomentTrace.MacProbe` with a proper `MomentTrace.Mac` platform service.

## Done

- [x] Create repo-local AI continuity documents for planning, architecture, decisions, stories, and prompts.
- [x] Split active planning content out of README.
- [x] `AB#88` / `AB#92`: Overview daily story dashboard.
- [x] Overview cards support timeline navigation, direct open, and pin actions.
- [x] Overview responsive layout uses actual Overview content width.
- [x] `AB#63` / `AB#87`: timeline card intelligence and visual polish.
- [x] `AB#89`: Avalonia shell alignment, spacing, and image framing.
- [x] Remove redundant left-rail items: Browse, Search, and Insights.
- [x] Fix sidebar label truncation by placing nav labels in the correct Grid column.
- [x] Tighten content layout and reduce excess right-side padding.
- [x] Fix Windows custom chrome so resize handles work without duplicate native caption buttons.
- [x] Fix `SystemSounds` compile issue on the non-Windows `net8.0` target with a Windows compile guard.
- [x] Add single-instance behavior that brings the running app back when launched again.
- [x] Add Overview empty/low-data states and theme-aware dashboard controls.
- [x] Add macOS probe validation notes for app identity, browser URLs, screenshots, and reopen behavior.
- [x] [2026-06-27 09:28 UTC+1] Immediate refresh pass for local mutations and view changes across Timeline and Overview.
- [x] [2026-06-27 09:28 UTC+1] Capture Explorer folder paths so folder moments open the captured folder instead of generic Explorer.

## QA Notes

Use this section for short pass/fail notes during the next stability sweep.

- [2026-06-27 10:05 UTC+1] Immediate refresh follow-up: strengthened Timeline/Overview refresh signatures so pin, bookmark, and note mutations always trigger a fresh render pass.
- [2026-06-27 18:40 UTC+1] Manual smoke pass succeeded for pin, bookmark, note, and delete refresh behavior; fixed a dark-theme note editor contrast clash in the detail pane.
- Windows chrome: last confirmed working with `ExtendClientAreaToDecorationsHint=True` and `WindowDecorations=BorderOnly`.
- Overview dashboard: #88 closed after build verification and Azure DevOps update.
- Known open issue: macOS `Cmd+,` main-shell shortcut remains unverified/failing until tested again on a Mac.
