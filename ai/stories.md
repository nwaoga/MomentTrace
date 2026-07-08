# MomentTrace Stories

## Backlog

No open local backlog items. Pull the next Azure DevOps priority when ready.

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
- [x] Add macOS Platform Service validation notes for app identity, browser URLs, screenshots, and reopen behavior.
- [x] [2026-06-27 09:28 UTC+1] Immediate refresh pass for local mutations and view changes across Timeline and Overview.
- [x] [2026-06-27 09:28 UTC+1] Capture Explorer folder paths so folder moments open the captured folder instead of generic Explorer.
- [x] [2026-07-01 23:34 UTC+1] Focused stability QA sweep across Windows shell interactions; macOS shortcut verification remains a separate follow-up story.
- [x] [2026-07-01 23:50 UTC+1] Refresh sanitized README preview artwork for the current Avalonia shell using SVG mock previews for Overview, timeline/detail, and theme collage.
- [x] [2026-07-03 16:40 UTC+1] Fix accent-label contrast selection so active buttons and accent pills stay readable across all shipped desktop themes.
- [x] [2026-07-04 16:28 UTC+1] Verify and fix macOS desktop shortcuts: `Cmd+,`, `Cmd+F`, `Cmd+1`, `Cmd+2`, and `Cmd+W`.
- [x] [2026-07-08 17:03 UTC+1] `AB#121`: Run lightweight visual QA across shipped desktop themes, including a scripted screenshot pass for Timeline, Overview, and Saved Moments.
- [x] [2026-07-08 17:32 UTC+1] Add regression tests for privacy exclusions, settings persistence, and warning-free build expectations with a new `MomentTrace.Tests` project covering domain/app filtering and desktop settings save/load behavior.
- [x] [2026-07-08 17:46 UTC+1] Add a Settings option to delete all local data for full privacy cleanup, including timeline history, previews, icons, settings, cached privacy blocklists, and startup disablement before app shutdown.
- [x] [2026-07-08 22:11 UTC+1] `AB#72`: Add first-run onboarding for privacy, startup behavior, and local storage expectations.
- [x] [2026-07-08 22:35 UTC+1] `AB#77`: Replace `MomentTrace.MacProbe` with the first-class `MomentTrace.Mac` platform service and wire it into `MomentTrace.Desktop`.

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
- [2026-07-03 16:40 UTC+1] Automated desktop theme contrast audit passed for base, surface, muted, and accent label pairings after switching accent foreground selection from a brightness threshold to a contrast-based pick; a broader manual visual theme sweep still remains open.
- [2026-07-04 16:28 UTC+1] macOS desktop shortcut pass succeeded on macOS 26.5 after routing shell shortcuts through a tunneling key handler. `Cmd+,` opened Settings, `Cmd+,` and `Cmd+W` closed Settings, `Cmd+F` focused search, physical `Cmd+1` activated Timeline, physical `Cmd+2` activated Overview, and `Cmd+W` closed the app. Main and Settings windows both used native traffic-light chrome.
- [2026-07-08 16:09 UTC+1] Theme QA follow-up found that active rail items kept idle text colors against the selected-state surface in several themes. Active workspace, library, and source nav rows now switch icon/title/subtitle foregrounds to the active accent foreground, and `dotnet build MomentTrace.slnx` passed cleanly after the fix.
- [2026-07-08 17:03 UTC+1] Proper desktop theme QA pass completed with scripted screenshot capture across Timeline, Overview, and Saved Moments for Midnight, Starbucks Reserve, Nero Roast, Costa Afterglow, Espresso (Mono), and Hot White. Side-by-side contact sheets under `ai/theme-qa-output/` confirmed the themes render as distinct variants, and no second theme-specific regression stood out after the active-nav foreground fix.
- [2026-07-08 17:32 UTC+1] Regression coverage now includes 9 automated tests in `MomentTrace.Tests` for domain/app privacy filtering, lock-screen/self exclusion rules, settings save/load round trips, legacy settings migration, and built-in privacy defaults. `dotnet test MomentTrace.Tests/MomentTrace.Tests.csproj` passed, and `dotnet build MomentTrace.slnx` passed with 0 warnings and 0 errors after closing a running desktop process that had locked desktop output assemblies.
- [2026-07-08 17:46 UTC+1] Full privacy cleanup now has a dedicated Settings action that deletes all local app data and then closes the app. Automated coverage includes a `LocalDataCleanupService` test, bringing `MomentTrace.Tests` to 10 passing tests, and `dotnet build MomentTrace.slnx` passed with 0 warnings and 0 errors.
- [2026-07-08 22:11 UTC+1] First-run onboarding now appears once after the main shell loads, explains local-first storage and cleanup controls, keeps adult-site filtering enabled by default, offers Start with Windows, and persists completion in `DesktopWindowPreferences`. `dotnet test MomentTrace.Tests/MomentTrace.Tests.csproj --no-build` passed with 10 tests, and `dotnet build MomentTrace.slnx` passed with 0 warnings and 0 errors.
- Windows chrome: last confirmed working with `ExtendClientAreaToDecorationsHint=True` and `WindowDecorations=BorderOnly`.
- Overview dashboard: #88 closed after build verification and Azure DevOps update.

