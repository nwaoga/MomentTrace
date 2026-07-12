# MomentTrace Stories

## Backlog

- [ ] Verify the replacement beta release artifacts on GitHub: Windows installer, Windows portable zip, and macOS app zip.
- [ ] Smoke test the Windows installer and confirm it launches `MomentTrace.Desktop.exe`.
- [ ] Rebuild the beta release after fixing macOS app bundle signing, then smoke test the macOS app zip on a Mac and document any Gatekeeper/permissions caveats.
- [ ] Verify tray/background behavior in the shipped Avalonia desktop app on real Windows and macOS release builds, including the Windows tray icon and the macOS menu bar status item.
- [ ] Verify icon parity in the shipped Windows and macOS builds, including app window, installer/app bundle, tray icon, Dock icon, Finder icon, and macOS menu bar status item visuals.
- [ ] Reduce dense-day lag in the Avalonia shell after Windows beta smoke testing found multi-second card selection delays around ~700 moments/day.
- [ ] Smoke test the macOS app zip on a Mac and document any Gatekeeper/permissions caveats.
- [ ] `AB#130` (in progress): Capture the actual macOS app window by window ID instead of relying only on screen-rectangle screenshots. Code now prefers `screencapture -l` via CGWindowList; still needs real Mac smoke verification.
- [ ] `AB#131`: Add macOS screenshot fallback diagnostics so missed or degraded captures explain whether we hit window capture, rectangle fallback, or no capture path.
- [ ] `AB#132`: Evaluate ScreenCaptureKit window capture as the durable macOS replacement path if shell-based capture still misses the app window.
- [ ] Promote marketing launch assets after release artifacts are verified.
- [ ] Add real Windows signing secrets and plan macOS Developer ID signing/notarization when credentials are available.

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
- [x] [2026-07-12 01:13 UTC+1] `AB#133`: Convert Settings from a modal dialog to an in-shell page with Overview-matching cards, rail active state, and Notes-style text boxes; hide rail user profile for later revisit.
- [x] [2026-07-08 17:03 UTC+1] `AB#121`: Run lightweight visual QA across shipped desktop themes, including a scripted screenshot pass for Timeline, Overview, and Saved Moments.
- [x] [2026-07-08 17:32 UTC+1] Add regression tests for privacy exclusions, settings persistence, and warning-free build expectations with a new `MomentTrace.Tests` project covering domain/app filtering and desktop settings save/load behavior.
- [x] [2026-07-08 17:46 UTC+1] Add a Settings option to delete all local data for full privacy cleanup, including timeline history, previews, icons, settings, cached privacy blocklists, and startup disablement before app shutdown.
- [x] [2026-07-08 22:11 UTC+1] `AB#72`: Add first-run onboarding for privacy, startup behavior, and local storage expectations.
- [x] [2026-07-08 22:35 UTC+1] `AB#77`: Replace `MomentTrace.MacProbe` with the first-class `MomentTrace.Mac` platform service and wire it into `MomentTrace.Desktop`.
- [x] [2026-07-08 22:48 UTC+1] `AB#78`: Document the macOS packaging, Developer ID signing, notarization, DMG, permission, and release pipeline plan.
- [x] [2026-07-08 23:05 UTC+1] `AB#70`: Add optional Windows code-signing hooks for release artifacts and document verified publisher setup.
- [x] [2026-07-08 23:22 UTC+1] Add beta tag support to the GitHub release workflow so test releases can ship as prereleases before a stable marketing launch.
- [x] [2026-07-08 23:34 UTC+1] Add a marketing launch kit with positioning, landing page copy, social posts, demo script, screenshots, and beta-release checklist.
- [x] [2026-07-08 23:45 UTC+1] Add a static landing page draft under `docs/landing/` for the desktop beta launch.
- [x] [2026-07-08 23:58 UTC+1] Update release workflow to build Windows and macOS artifacts together from the Avalonia desktop shell.
- [x] [2026-07-09 00:18 UTC+1] Fix README preview SVG rendering and the GitHub release publish job for `v0.1.1-beta.6`.

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
- [2026-07-09 00:18 UTC+1] Release follow-up pass: all README SVG assets parse as valid XML and README image paths resolve locally. `v0.1.1-beta.6` publish job now sets `GH_REPO` so `gh release create` can publish without a checkout-backed `.git` directory.
- [2026-07-09] Beta artifact check found `MomentTrace-0.1.1-beta.6-macos-arm64.zip` was not corrupt: its SHA-256 matched the GitHub release digest and `unzip -t` passed. The extracted `MomentTrace.app` failed `codesign --verify --deep --strict` because the bundle was not signed coherently, which can surface to users as a broken or damaged app. Packaging now needs to ad-hoc sign the app bundle before zipping, then publish a replacement beta.
- [2026-07-09] `v0.1.1-beta.7` fixed the outer app-bundle seal but still failed `codesign --verify --deep --strict` because nested runtime files in `Contents/MacOS` were unsigned. Signing each published file before re-signing the outer `.app` passed strict verification on the extracted artifact and should be used for the next replacement beta.
- [2026-07-09] `v0.1.1-beta.8` failed in CI before upload because `codesign` reached the app executable before all sibling managed assemblies were signed. The signing order now puts sibling files before `MomentTrace.Desktop`, then seals the outer `.app`.
- [2026-07-09] `v0.1.1-beta.9` release workflow passed and published Windows plus macOS assets. The macOS zip SHA-256 matched GitHub's digest, `unzip -t` passed, and `ditto` extraction produced an app that passed `codesign --verify --deep --strict`. `spctl` still rejects the app because beta builds are ad-hoc signed rather than Developer ID signed/notarized.
- [2026-07-09] Windows beta.9 follow-up: the published portable `MomentTrace.Desktop.exe` contained `v0.1.1-beta.9`, but Settings displayed the numeric assembly version because the Avalonia shell did not read `AssemblyInformationalVersion`. The Settings version field now matches the stamped prerelease version, and stale beta.6 public links were updated.
- [2026-07-09 22:42 UTC+1] Windows beta smoke test on downloaded `v0.1.1-beta.10`: the Avalonia shell launched, but no system tray icon appeared even though the app was running. This confirmed the release is shipping `MomentTrace.Desktop.exe` without the old WPF tray host behavior.
- [2026-07-09 22:42 UTC+1] Same Windows beta smoke test found dense-day responsiveness issues on a local dataset with roughly 700 moments for the day; even selecting a card could take a few seconds before the detail pane updated.
- [2026-07-09 23:10 UTC+1] Desktop follow-up implementation now adds shared background-shell behavior in `MomentTrace.Desktop`: Windows gets a tray icon again, and macOS now gets the equivalent menu bar status item with show, pause, resume, and exit actions. Real release-build smoke verification on both platforms still remains open.
- [2026-07-09] macOS identity and permissions follow-up: the Avalonia shell now sets the app name and assembly product metadata to MomentTrace, and failed macOS screenshot captures back off briefly so Screen Recording permission prompts are not repeated on every recorder poll while macOS is still applying or denying permission.
- [2026-07-09] `v0.1.1-beta.12` release workflow passed and published all expected assets. Downloaded macOS zip matched GitHub SHA-256, passed archive integrity, extracted with `ditto`, passed strict code-sign verification, and reported `MomentTrace` for both `CFBundleName` and `CFBundleDisplayName`. Tomorrow's smoke pass should verify the visible macOS app menu name, Screen Recording prompt behavior after full quit/reopen, Windows tray/background behavior, and dense-day responsiveness.
- [2026-07-10 10:40 UTC+1] Icon parity follow-up now routes the Avalonia shell through shared full-colour and template icon assets: the window and Windows tray use the same full-colour mark, while macOS status-item behavior uses a monochrome template icon and the macOS bundle script now generates a real `.icns` from the shared source PNG.
- [2026-07-10 11:18 UTC+1] Azure DevOps follow-up created `AB#130`, `AB#131`, and `AB#132` to cover macOS screenshot reliability work before the next implementation pass.
- [2026-07-10 21:35 UTC+1] `AB#130` implementation started: macOS screenshot capture now resolves the frontmost process CGWindowID through JXA/CoreGraphics and prefers `screencapture -l`, keeping Accessibility `-R` bounds and full-display capture as fallbacks. `LastScreenshotCaptureMethod` records which path succeeded for the upcoming diagnostics story. Real Mac verification is still required before closing the work item.
- [2026-07-12 01:13 UTC+1] `AB#133` closed: Settings moved from modal dialog to in-shell page with Overview-matching card panels, Notes-style domain text boxes, rail active state, and Esc return to the previous workspace page. Rail user profile remains commented out for later revisit.
- Windows chrome: last confirmed working with `ExtendClientAreaToDecorationsHint=True` and `WindowDecorations=BorderOnly`.
- Overview dashboard: #88 closed after build verification and Azure DevOps update.
