# MomentTrace Stories

## Backlog

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
- [x] [2026-07-01 23:34 UTC+1] Focused stability QA sweep across Windows shell interactions; macOS shortcut verification remains a separate follow-up story.
- [x] [2026-07-01 23:50 UTC+1] Refresh sanitized README preview artwork for the current Avalonia shell using SVG mock previews for Overview, timeline/detail, and theme collage.

## QA Notes

Use this section for short pass/fail notes during the next stability sweep.

- [2026-06-27 10:05 UTC+1] Immediate refresh follow-up: strengthened Timeline/Overview refresh signatures so pin, bookmark, and note mutations always trigger a fresh render pass.
- [2026-06-27 18:40 UTC+1] Manual smoke pass succeeded for pin, bookmark, note, and delete refresh behavior; fixed a dark-theme note editor contrast clash in the detail pane.
- [2026-07-01 11:02 UTC+1] Build verification passed with `dotnet build MomentTrace.slnx` after closing the running desktop process.
- [2026-07-01 11:02 UTC+1] Single-instance relaunch check passed: launching `MomentTrace.Desktop` a second time kept one running process and reused the existing instance.
- [2026-07-01 11:12 UTC+1] UI automation pass confirmed Timeline and Overview navigation both render correctly on the current local dataset; Overview summary metrics, key moments, focus rhythm, and heatmap labels were visible.
- [2026-07-01 11:12 UTC+1] Settings window opened successfully from the shell and showed healthy recorder/database/browser URL/storage status on this machine.
- [2026-07-01 11:12 UTC+1] Library and Source filter pass succeeded on the current dataset: Saved Moments, Notes, Bookmarks, and Files rendered empty states; Apps, Websites, and Timeline rendered populated states with counts.
- [2026-07-01 23:34 UTC+1] Windows custom chrome pass succeeded on this machine: Win32 reported `WS_THICKFRAME` enabled, maximize/restore changed window bounds correctly, and `WS_SYSMENU` was absent so native caption buttons did not reappear over the custom shell controls.
- [2026-07-01 23:34 UTC+1] Single-instance relaunch check passed again after the chrome verification run.
- [2026-07-01 23:34 UTC+1] Toolbar drag-region behavior was not mouse-verified in this pass; no regression was observed during maximize/restore automation.
- [2026-07-01 23:45 UTC+1] Manual mouse verification passed for toolbar drag-region window movement on Windows.
- Windows chrome: last confirmed working with `ExtendClientAreaToDecorationsHint=True` and `WindowDecorations=BorderOnly`.
- Overview dashboard: #88 closed after build verification and Azure DevOps update.
- Known open issue: macOS `Cmd+,` main-shell shortcut remains unverified/failing until tested again on a Mac.
