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
- macOS shell shortcuts have been verified on real hardware after routing Command-key handling through the main window.
- Codex/Cursor conversation history is local tooling state and should not be assumed to live in Git.
- [2026-06-27 09:28 UTC+1] Local shell mutations should invalidate Timeline, Overview, detail, and rail caches together, and reload requests made during an active reload should queue one follow-up pass.
- [2026-06-27 09:28 UTC+1] Windows Explorer folder moments should persist the captured folder path in `TimelineActivity.Resource`; the desktop opener should not fall back to launching generic Explorer when no folder path can be resolved.
- [2026-06-27 10:05 UTC+1] Timeline and Overview refresh signatures should include per-moment saved-state metadata so pin, bookmark, and note mutations cannot be skipped as cache-equivalent UI states.
- [2026-06-27 18:40 UTC+1] The detail-pane note editor should use a dedicated dark-surface textbox style so Avalonia template internals cannot fall back to a light background and low-contrast text.
- [2026-07-03 16:40 UTC+1] Accent foreground text in the desktop theme palette should be chosen by measured contrast against the accent fill rather than a simple brightness threshold so Nero and espresso theme labels stay readable.
- [2026-07-04 16:28 UTC+1] macOS shell shortcuts should be handled through a tunneling key handler on the main window so Command-key navigation still reaches the shell when child controls such as search have focus.
- [2026-07-04 16:28 UTC+1] Windows-only single-instance signaling should be annotated as Windows-supported so the cross-platform `net8.0` desktop build stays warning-free.
- [2026-07-08 16:09 UTC+1] Active rail selection should explicitly swap icon and label foregrounds to the theme accent foreground instead of relying on idle text brushes over `ActiveSurface`; this keeps Timeline, Overview, Library, and Source selections readable across Nero, espresso, and the other shipped themes.
- [2026-07-08 17:32 UTC+1] `DesktopWindowPreferences` should allow internal path overrides for tests so settings persistence and legacy migration can be verified without touching the real roaming app-data directory.
- [2026-07-08 17:46 UTC+1] Full privacy cleanup should be a separate Settings action from activity-only deletion, and it should disable startup plus close the app after deleting local timeline data, previews, icons, settings, and cached privacy blocklists so the app does not immediately recreate fresh local state in the same session.
- [2026-07-08 22:11 UTC+1] First-run onboarding should appear after the main shell is visible, persist through `DesktopWindowPreferences`, and use existing privacy/startup settings rather than introducing a separate onboarding configuration store.
- [2026-07-08 22:35 UTC+1] macOS capture now belongs in the first-class `MomentTrace.Mac` platform project and is hosted by `MomentTrace.Desktop` on the cross-platform `net8.0` target.
- [2026-07-08 22:48 UTC+1] macOS releases should ship as signed and notarized `.app` bundles inside DMGs, with Developer ID credentials stored only in release secrets.
- [2026-07-08 23:05 UTC+1] Windows release signing should be optional for local/internal builds but automatic in the release workflow when protected signing secrets are configured.
- [2026-07-09] Internal macOS beta zips should ad-hoc sign `MomentTrace.app` before packaging. This does not replace Developer ID signing/notarization, but it prevents invalid bundle-signature states that macOS can report as damaged.
- [2026-07-09 22:42 UTC+1] The Windows beta now ships the Avalonia desktop shell as the actual release app, so tray/background behavior can no longer live only in `MomentTrace.App`; Windows release builds of `MomentTrace.Desktop` must provide a real tray icon and explicit background/exit behavior.
- [2026-07-10 21:35 UTC+1] macOS screenshots should capture by CGWindowID (`screencapture -l`) first, using Accessibility rectangle crop only as fallback. Window IDs are resolved with on-screen, layer-0 windows for the active PID, preferring title matches then largest area.
- [2026-07-12 01:13 UTC+1] Settings should live as an in-shell page beside Timeline and Overview, not as a modal window; Esc returns to the previous workspace page, and Settings rail selection uses the same active-nav treatment.
- [2026-07-12 01:13 UTC+1] Settings multi-line text boxes should reuse the Notes editor (`noteBox`) surface treatment so PanelBrush/focus borders stay consistent across the shell.
- [2026-07-12 01:13 UTC+1] The left-rail user profile block is intentionally hidden for now; revisit account/identity UI later while keeping Settings as a normal rail item.
- [2026-07-17] Release tags are immutable once published. If a beta tag already exists on another commit, cut the next beta number rather than moving the old tag.
- [2026-08-08 00:01 UTC+1] `AB#134` closed by verifying the existing `v0.1.1-beta.14` GitHub Release assets rather than rerunning the failed tag-push workflow. Assets were published by workflow_dispatch run `29874931579` from `codex-direct-release-assets` (`1a57719`). Tag tip remains `a273d83`, and the published package embeds that tag commit with `BuildNumber=16.1`. Prefer keeping published Release assets when Actions upload later fails for storage-quota reasons, and avoid retagging. Merge the artifact-bypass workflow change into `main` before relying on future tag-push releases.
- [2026-08-08 00:12 UTC+1] After close-to-background on macOS, Dock/app `activate` is not enough to restore the hidden Avalonia main window; restore remains tray Show/click via `ShowMainWindow()`. Consider an explicit macOS reopen/activation handler if that UX is desired.
- [2026-07-17] GitHub Actions artifact storage quota is treated as a release-operations dependency. A `CreateArtifact` quota failure should be handled by deleting obsolete workflow artifacts/prerelease assets and rerunning the same tag workflow, not by changing app code or moving the tag.
- [2026-07-17] App build identity should remain separate from semantic assembly/file versioning: Settings may show release label, GitHub run number/attempt, and short commit SHA, while OS-level assembly/file versions stay numeric.
- [2026-07-19] Public marketing should wait until the release has an explicit signing/trust and update story. Prefer a shared Velopack updater path for Windows and macOS if it fits the final packaging model; keep Sparkle as the macOS-native fallback, and treat manual update checks as an acceptable first step before fully automatic updates.
- [2026-08-21] Public beta interest collection should run through the hosted landing page first: real screenshots, D1-backed waitlist storage, anonymous CTA metrics, and a protected owner-only metrics dashboard are enough to measure early signal before broader launch tooling is added.
- [2026-08-23] Community launch should start with one honest feedback-seeking post at a time. The first live pass is `r/SideProject` with a short native Reddit teaser video and the beta-page CTA; measure replies and `/admin/metrics` before copying the post into other communities.
- [2026-08-23] Operational continuity should not depend on a single stale Azure DevOps PAT. When ADO access is down, record launch/ops notes in `ai/` immediately and backfill ADO once PAT access is refreshed.
- [2026-08-23] Cold launch traffic should see the privacy reassurance before the product metaphor. Lead the hosted beta page with "Nothing leaves your machine unless you choose to export it" and avoid using "desktop memory timeline" as the first-frame description, because some readers initially interpret it as screen recording.

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
- [2026-07-04 16:28 UTC+1] Fixed and verified macOS desktop shortcuts for Settings, search, Timeline, Overview, and window close behavior.
- [2026-07-08 22:35 UTC+1] Replaced the old macOS probe project with `MomentTrace.Mac` and wired it into the Avalonia desktop shell.
- [2026-07-12 01:13 UTC+1] Closed `AB#133`: Settings is now an in-shell page with Overview-matching cards and Notes-style text boxes.

## Open Decisions To Revisit

- Whether `AGENT.md` should remain as a root-level compatibility handoff or become a short pointer to `ai/`.
- Whether to add automated UI/presentation tests before the broader hardening pass.
