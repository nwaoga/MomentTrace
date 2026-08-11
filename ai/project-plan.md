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
- Treat the first paid/public beta as a same-day Windows + macOS offer; do not monetize or publicly market a one-platform-only release.

---

## Current State (2026-08-09 23:00 UTC+1)

**Phase:** Beta release readiness and marketing prep.

**Working today:**

- Avalonia desktop shell with Timeline, Overview, and in-shell Settings pages.
- Settings is a third shell page (not a modal), restyled with Overview card panels and Notes-matching text boxes; rail user profile remains deferred.
- Overview daily story dashboard with summary cards, heatmap, Moments Today, activity breakdown, and focus rhythm.
- Overview drill-ins: heatmap range focus, moment timeline navigation, direct open, and pin actions.
- Left rail simplified into primary workspace views, Library filters, and Source filters.
- Windows custom chrome with resize handles and no duplicate native caption buttons.
- Immediate local mutation refresh across Timeline, Overview, detail pane, and Library filters.
- Detail-pane note editor now uses a theme-safe dark surface with readable text and focus states.
- Theme visual QA pass completed across Midnight, Starbucks Reserve, Nero Roast, Costa Afterglow, Espresso (Mono), and Hot White, with scripted screenshot capture for Timeline, Overview, and Saved Moments.
- Regression coverage now includes automated checks for privacy exclusions, settings persistence, and warning-free build verification.
- Settings now includes a full local-data cleanup action that deletes local history, previews, icons, settings, and cached privacy blocklists before shutting down the app.
- First-run onboarding now explains local-first storage, privacy filtering, startup behavior, and cleanup controls before marking onboarding complete.
- Explorer folder moments capture and reopen the captured folder path.
- README preview artwork refreshed with sanitized SVG mockups for the current shell, Overview dashboard, timeline/detail pane, and theme collage.
- macOS shell shortcut verification passed for Settings, search, Timeline, Overview, and close behavior.
- macOS foreground capture is now in the first-class `MomentTrace.Mac` platform service and is hosted by `MomentTrace.Desktop` on the cross-platform target.
- Release tags now build the Avalonia desktop shell for Windows and macOS together.
- Current beta candidate with published GitHub Release assets: `v0.1.1-beta.15`.
- Windows release artifacts include an installer and portable zip for `MomentTrace.Desktop.exe`.
- macOS release artifacts include an ad-hoc signed `MomentTrace.app` zip for `osx-arm64` until Developer ID signing/notarization is configured.
- Windows signing readiness now has an enforceable release gate: stable tags require Authenticode signing, manual prerelease runs can opt in with `require_windows_signing=true`, and the Windows job verifies executable/setup signature status before upload. Real signing certificate procurement/secrets and verified-publisher smoke are tracked as `AB#142`.
- Windows signing secret setup now has a repo helper, `scripts/configure-windows-signing-secrets.ps1`, which validates a PFX for private key, Code Signing EKU, and expiry before setting GitHub secrets. No local/GitHub signing certificate material was found on 2026-08-11, so `AB#142` is blocked on obtaining/providing the real PFX and password.
- README preview SVG rendering has been fixed and validated locally.
- Marketing launch kit and static landing page draft are in `docs/`.
- Monetization posture: public marketing and founder-license interest should wait until Windows and macOS artifacts are available from the same release tag and have both passed install/launch/background smoke.
- Shared presentation builders for timeline, overview, and detail context.
- Local SQLite activity store and Windows foreground recorder.
- Windows beta smoke testing previously found two release-follow-up issues in the Avalonia shell: dense-day performance lag and missing tray/background behavior in the shipped desktop app. The tray/background issue is fixed and verified in the published `v0.1.1-beta.15` Windows artifacts; dense-day responsiveness remains a separate follow-up.
- The desktop shell now includes shared background-shell behavior in code: Windows tray icon plus macOS menu bar status item, verified on published beta artifacts.
- The desktop shell icon path now has a shared parity pass in code: full-colour app icon for Windows/window surfaces, monochrome template icon for the macOS status item, and macOS bundle `.icns` generation from the same source artwork.
- Azure DevOps now tracks the macOS screenshot reliability follow-up as `AB#130`, `AB#131`, and `AB#132`.
- `AB#130` code path is in progress: macOS capture now prefers window-ID screenshots (`screencapture -l`) with rectangle/full-display fallbacks; real Mac visual verification passed for normal app and Safari browser crops, with multi-monitor and direct capture-method diagnostics still outstanding.
- Timeline screenshot previews now have a dedicated expand action that opens a larger in-window modal without changing normal tile selection behavior.
- `AB#134` is closed: after Actions artifact-quota failures on the `v0.1.1-beta.14` tag-push run, the release assets were published via workflow_dispatch run `29874931579` on `codex-direct-release-assets` (`1a57719`). All three expected assets are on the GitHub Release; active Actions artifacts are currently `0`. Do not retag or move `v0.1.1-beta.14`. The artifact-bypass workflow change is now on `main` and was used successfully for `v0.1.1-beta.15`.
- Published `v0.1.1-beta.15` macOS zip smoke passed (launch, status-item Show/Exit, stamped `v0.1.1-beta.15` / build `17.1` / `f4dd50e`).
- Published `v0.1.1-beta.15` Windows setup and portable smoke passed: digests matched GitHub, installed/portable builds launched as `v0.1.1-beta.15`, close-to-background retained the original process, relaunch restored the hidden main window, tray overflow showed the MomentTrace mark, and silent uninstall completed cleanly.

**Last pushed:** `v0.1.1-beta.15` tagged; macOS and Windows smoke recorded.

---

## Pick Next Work

### Active Next Work — Beta Release Verification

**Why:** The release train now creates Windows and macOS artifacts together, and `v0.1.1-beta.15` has passed the core cross-platform artifact smoke gate. Public marketing still needs a clearer trust/update story and the known dense-day responsiveness caveat handled or documented.

**Status:** Passed for `v0.1.1-beta.15` — Windows and macOS release artifacts from the same tag passed the core install/launch/background smoke gate. Dense-day responsiveness remains a follow-up before broad marketing, and signing/trust/updates are the next launch-readiness track.

**Scope:**

- Download and smoke test the Windows installer on a clean or disposable Windows profile.
- Download and inspect the macOS zip on a Mac, confirming it extracts to `MomentTrace.app` and launches far enough to show the shell/onboarding.
- Confirm Settings > Where I Live shows the current beta label with stamped build metadata.
- Update `docs/marketing-launch-kit.md` with any beta caveats discovered during install testing.
- Fix or verify any blocking shell regressions found during smoke testing before calling the beta marketable, including tray/background behavior and dense-day responsiveness.
- Verify icon consistency across the shipped Windows and macOS artifacts before public sharing.
- Keep the next macOS screenshot reliability implementation mapped in Azure DevOps before engineering starts, with capture, diagnostics, and architecture follow-up split across `AB#130`, `AB#131`, and `AB#132`.
- Consider merging the `codex-direct-release-assets` artifact-bypass workflow change into `main` before the next tagged release, so future tag pushes do not hit Actions artifact storage again.

**Acceptance criteria:**

- Release artifacts are present and named correctly.
- Windows installer launches the Avalonia shell, not the old WPF shell.
- Windows installer build preserves expected tray/background behavior for a memory-capture app.
- Windows and macOS release builds use matching MomentTrace branding across installer/app bundle, window, tray, Dock, Finder, and menu bar surfaces.
- macOS release build exposes an equivalent menu bar status item so users can tell MomentTrace is still running in the background after closing the main window.
- Windows and macOS artifacts from the same replacement beta tag both pass enough smoke testing to support a single cross-platform beta CTA.
- macOS zip contains `MomentTrace.app`.
- Any signing, Gatekeeper, SmartScreen, or first-launch caveat is documented before public sharing.
- GitHub Actions artifact storage has enough headroom for release uploads, or release packaging no longer depends on Actions artifact storage for publish.
- `stories.md` records the smoke-test result.

**Progress so far:**

- [2026-07-09] `v0.1.1-beta.6` macOS zip downloaded with the expected SHA-256 digest and passed `unzip -t`, but the extracted `MomentTrace.app` failed `codesign --verify --deep --strict` with `code has no resources but signature indicates they must be present`. Rebuild the macOS artifact after ad-hoc signing the bundle before public sharing.
- [2026-07-09] `v0.1.1-beta.7` confirmed the app bundle itself was ad-hoc signed, but strict verification still failed on unsigned nested files under `Contents/MacOS`. Sign those published files before sealing the outer bundle, then rebuild as the next replacement beta.
- [2026-07-09] `v0.1.1-beta.8` failed during CI because `MomentTrace.Desktop` was signed before sibling managed assemblies such as `System.Web.dll`. Sign sibling files first, then the app executable, then the outer bundle.
- [2026-07-09] `v0.1.1-beta.9` release workflow passed. The macOS zip digest matched the GitHub asset digest and `unzip -t` reported no archive errors. Native `ditto` extraction preserved AppleDouble signature metadata and the extracted `MomentTrace.app` passed `codesign --verify --deep --strict`; command-line `unzip` extraction still drops managed-assembly signature metadata and should not be used for launch validation.
- [2026-07-09] `v0.1.1-beta.9` Windows assets were named and stamped as beta.9, but app Settings read the numeric assembly version instead of `AssemblyInformationalVersion`, and launch/landing docs still linked to beta.6. Settings now displays the informational prerelease version and public beta links target the replacement beta.
- [2026-07-09] macOS beta.11 follow-up: the application menu still displayed the Avalonia fallback name and repeated Screen Recording prompts could continue after permission was toggled before relaunch. Set the Avalonia application name and assembly metadata to MomentTrace, and throttle failed screenshot capture attempts so a stale/denied TCC state does not prompt continuously.
- [2026-07-09] `v0.1.1-beta.12` release workflow passed and published Windows plus macOS assets. The macOS zip SHA-256 matched the GitHub release digest, `unzip -t` passed, native `ditto` extraction produced an app that passed `codesign --verify --deep --strict`, and the extracted bundle reports `CFBundleName` and `CFBundleDisplayName` as `MomentTrace`.
- [2026-07-12] Real Mac local smoke verified fresh screenshot rows and tight visual crops for GitKraken and Safari, confirming the macOS capture output is no longer full-display for those normal app/browser cases. `LastScreenshotCaptureMethod` is not persisted yet, so `AB#131` should make the exact capture path visible.
- [2026-07-12] Added a larger screenshot preview modal with a dedicated expand icon in the timeline tile detail band and detail preview surface; normal tile click behavior remains unchanged.
- [2026-07-17] `v0.1.1-beta.14` release run failed after tag push because both Windows and macOS jobs hit GitHub Actions artifact storage quota at `CreateArtifact`. Tracked as `AB#134`: delete old artifacts/obsolete prerelease assets, wait for GitHub storage usage to recalculate if necessary, then rerun the same tag workflow. Do not retag or move `v0.1.1-beta.14`.
- [2026-07-19] Cleared obsolete GitHub Actions workflow artifacts from beta.4 through beta.13, leaving published GitHub Releases intact. Reran `v0.1.1-beta.14` release run `29613812220` as attempt 2; Windows and macOS builds succeeded through packaging but still failed at `CreateArtifact` because GitHub had not recalculated artifact storage usage yet. Wait for the 6-12 hour GitHub storage recalculation window, then rerun the same workflow again.
- [2026-08-08 00:01 UTC+1] `AB#134` closed on verification. GitHub Release `v0.1.1-beta.14` has all three expected assets from workflow_dispatch run `29874931579` (`codex-direct-release-assets` / `1a57719`). Active Actions artifacts are `0`. Original failed tag-push run `29613812220` left as historical failure; no tag move. Next work is install smoke testing of the published assets, not another quota recovery.
- [2026-08-08 00:08 UTC+1] Official macOS zip smoke for `v0.1.1-beta.14` passed on this Mac: digest match, clean archive test, `ditto` extract to `MomentTrace.app`, strict ad-hoc codesign verify, launch to a visible `MomentTrace` window. Embedded identity is `v0.1.1-beta.14` / build `16.1` / commit `a273d83`. `spctl` rejects as expected until Developer ID + notarization.
- [2026-08-08 00:12 UTC+1] macOS close-to-background pass on the same published app: process retained after main-window close/hide; tray assets and `Show MomentTrace` menu strings are present in the package. Dock/`activate` does not unhide the window (restore is tray Show/click only). Menu-bar glyph visual confirmation still needs a brief human glance.
- [2026-08-08 21:39 UTC+1] macOS menu-bar status-item glance completed on published `v0.1.1-beta.14`: Accessibility confirmed a `MomentTrace` status menu with Show/Pause/Resume/Exit; Show restored and Exit quit cleanly. Pixel glyph ID among a crowded menu bar was inconclusive; functional confirmation is enough to clear the open glance item.
- [2026-08-08 11:08 UTC+1] Official Windows installer smoke for `v0.1.1-beta.14` passed install and initial launch: setup hash matched GitHub, unsigned admin Inno install completed with exit code 0 into a disposable install directory, installed `MomentTrace.Desktop.exe` reported `ProductVersion=v0.1.1-beta.14`, and launch opened a responsive `MomentTrace` window. Close-to-background retained the process after the window was closed, but relaunching the executable while hidden did not restore the window; track as a background-shell follow-up before marketing.
- [2026-08-08 11:22 UTC+1] Windows hidden-window relaunch is fixed locally: single-instance restore now calls the same `ShowMainWindow()` path used by tray/menu restore. Local Windows debug smoke passed for initial launch, close-to-background, relaunch restoring the original window, and single-process behavior. `dotnet build .\MomentTrace.Desktop\MomentTrace.Desktop.csproj -f net8.0-windows` and `dotnet test .\MomentTrace.Tests\MomentTrace.Tests.csproj` passed.
- [2026-08-08 21:50 UTC+1] Official `v0.1.1-beta.15` macOS zip smoke passed on this Mac: digest `sha256:90728321…`, strict ad-hoc codesign, launch to `MomentTrace`, status-item tooltip `MomentTrace v0.1.1-beta.15`, close-to-background then Show restore, Exit clean. Stamped build `17.1` / commit `f4dd50e` from release run `31277447681`.
- [2026-08-09 23:00 UTC+1] Official `v0.1.1-beta.15` Windows setup and portable zip smoke passed: setup digest `sha256:5cad5ca4…`, zip digest `sha256:81ffbd4a…`, both builds reported `ProductVersion=v0.1.1-beta.15`, launched to the Avalonia `MomentTrace` shell, close-to-background kept the process alive with no main window handle, relaunch restored the original hidden window while the second process exited, and hidden-icons overflow showed the MomentTrace full-colour tray mark. Silent uninstall exited 0 with no remaining app processes.
- [2026-08-09 23:20 UTC+1] Windows signing readiness gate added: stable tags now require Windows signing secrets and valid Authenticode signatures, manual prerelease runs can force the same behavior with `require_windows_signing=true`, and `docs/windows-code-signing.md` documents the public-beta readiness checklist.
- [2026-08-11] `AB#142` signing secret setup pass: GitHub currently has no `WINDOWS_SIGNING_*` secrets, no local PFX/P12 was found in the obvious checked locations, and no local CurrentUser/LocalMachine code-signing certificate was available. Added `scripts/configure-windows-signing-secrets.ps1` so the real PFX can be validated and uploaded to GitHub secrets once available.

**Pick up now:**

- `AB#142`: Configure real Windows signing certificate secrets, rerun a prerelease with `require_windows_signing=true`, and smoke the verified-publisher installer prompt.
- Continue Signing, Trust, And Updates before public marketing: Apple Developer ID/notarization and at least a manual update-check path.
- Finish the remaining `AB#130` verification on a real Mac: normal app and Safari visual crops passed locally on 2026-07-12; still confirm multi-monitor behavior and avoid closing the work item solely on visual evidence because the exact method is not persisted.
- Update Azure DevOps from a machine with ADO access: add the 2026-07-12 real Mac smoke note to `AB#130`, keep `AB#130` open for multi-monitor/exact-method confirmation, and add the preview-modal verification note to the relevant UI/release-train work item if tracking separately.
- Then `AB#131` for user-visible fallback diagnostics, including surfacing whether a moment used window-ID capture, rectangle fallback, full-display fallback, or no capture. Keep `AB#132` held unless shell capture still proves flaky.
- Still open from beta.12 smoke: macOS app menu name, Screen Recording prompt after quit/reopen, and dense-day responsiveness.
- If platform smoke tests pass after screenshot verification, move to Signing, Trust, And Updates before marketing; otherwise fix the remaining blocker and cut the next beta.

### Next After Release Verification — Signing, Trust, And Updates

**Why:** Public marketing should not send users to unsigned, hard-to-update desktop builds. The beta can remain internal/ad-hoc, but the first public push needs a clearer trust story and an update path.

**Scope:**

- Add real Windows signing certificate secrets and keep local/internal unsigned builds supported.
- Confirm Windows installer and executable show the expected verified publisher.
- Add Apple Developer ID signing and notarization secrets when available.
- Upgrade macOS output from ad-hoc signed zip toward a signed/notarized `.dmg` with a `MomentTrace.app` bundle.
- Add a self-update plan for both platforms before public sharing:
  - Prefer Velopack for a shared Windows/macOS .NET/Avalonia updater path.
  - Keep Sparkle as the macOS-native fallback if Velopack proves awkward for the final macOS packaging/signing model.
  - Start with manual update checks if full automatic update installation is too much for the first paid beta.
  - Host update metadata and packages on GitHub Releases, static hosting, or an explicit update bucket.
- Add Settings and tray/menu bar entry points for update checks once the updater backend is chosen.
- Document first-launch warnings, update behavior, rollback/recovery expectations, and known signing limitations in user-facing docs.

**Acceptance criteria:**

- Windows artifacts are signed in release workflow or signing is explicitly blocked on certificate procurement.
- macOS signing/notarization path is implemented or explicitly blocked on Apple Developer credentials.
- The app has at least a manual update check before public marketing; automatic background updating is implemented or scheduled with clear scope.
- User-facing install and update warnings are reduced and documented.
- Public marketing does not begin until trust/update caveats are acceptable for the target audience.
- Paid beta or founder-license offers do not begin until both Windows and macOS are available and smoke-tested from the same release train.

### Next After Trust/Updates — Marketing Launch Pass

**Why:** Once the artifacts are real, signed/trustworthy enough, and updateable, the marketing work should move from draft copy to public-facing launch assets.

**Scope:**

- Refresh README/landing CTA links if the final beta tag changes.
- Capture or select final sanitized screenshots from the verified build.
- Publish or export the static landing page draft from `docs/landing/index.html`.
- Prepare launch post, privacy post, and founder-story post from `docs/marketing-launch-kit.md`.
- Decide where feedback and paid-founder-license interest should go: GitHub Issues, email, form, waitlist, or payment checkout.
- Keep the launch offer cross-platform: Windows and macOS must be available together before asking for money.

**Acceptance criteria:**

- Public download link points to a green, trust-reviewed release.
- Launch page uses only sanitized images.
- Feedback route is clear.
- Known beta caveats are visible and honest.
- Paid beta/founder-license CTA is ready if monetization starts with the public launch.

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

**Status:** Completed 2026-07-01 23:34 UTC+1 on Windows. macOS shell verification later passed under Option D.

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

**Why:** Confirm the macOS shell shortcut pass after the known `Cmd+,` issue.

**Handover:** See `ai/macos-handover.md` before pulling on the Mac.

**Status:** Completed 2026-07-04 16:28 UTC+1.

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

Completed:

- [2026-06-27 09:28 UTC+1] **Option A** — Immediate refresh pass.
- [2026-07-01 23:34 UTC+1] **Option B** — Stability QA sweep on Windows.
- [2026-07-01 23:50 UTC+1] **Option C** — README preview refresh.
- [2026-07-04 16:28 UTC+1] **Option D** — macOS shortcut verification.
- [2026-07-08 17:03 UTC+1] **Theme visual QA pass** — shipped desktop theme sweep with screenshot evidence and active-nav readability fix.
- [2026-07-08 22:35 UTC+1] **AB#77 macOS platform service** — replaced `MomentTrace.MacProbe` with `MomentTrace.Mac` and wired it into `MomentTrace.Desktop`.
- [2026-07-08 22:48 UTC+1] **AB#78 macOS release plan** — documented `.app` bundling, Developer ID signing, notarization, DMG creation, credentials, permissions, and pipeline outline.
- [2026-07-08 23:05 UTC+1] **AB#70 Windows verified publisher flow** — added optional `signtool` signing for Windows app/installer artifacts, wired release secrets, and documented certificate handling.
- [2026-07-08 23:22 UTC+1] **Prerelease workflow** — release tags such as `v0.1.1-beta.1` now produce GitHub prereleases with beta-labelled artifacts.
- [2026-07-08 23:34 UTC+1] **Marketing launch kit** — added launch positioning, website copy, social posts, demo video script, screenshot checklist, and beta feedback prompts.
- [2026-07-08 23:45 UTC+1] **Landing page draft** — added a static product landing page at `docs/landing/index.html` using sanitized product visuals and beta CTAs.
- [2026-07-08 23:58 UTC+1] **Win/Mac release train** — release tags now build Windows installer/portable zip and macOS app zip together from `MomentTrace.Desktop`.
- [2026-07-09 00:18 UTC+1] **Release follow-up fixes** — fixed README SVG rendering and `v0.1.1-beta.6` release publishing by giving the no-checkout publish job explicit GitHub repository context.
- [2026-07-09 22:42 UTC+1] **Windows beta smoke findings** — local install verification of `v0.1.1-beta.10` found missing tray/background behavior and dense-day interaction lag in the shipped Avalonia desktop shell.

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
