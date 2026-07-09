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

## Current State (2026-07-09 00:30 UTC+1)

**Phase:** Beta release readiness and marketing prep.

**Working today:**

- Avalonia desktop shell with Timeline and Overview.
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
- Latest beta tag pushed: `v0.1.1-beta.9`.
- Windows release artifacts should include an installer and portable zip for `MomentTrace.Desktop.exe`.
- macOS release artifacts should include an ad-hoc signed `MomentTrace.app` zip for `osx-arm64` until Developer ID signing/notarization is configured.
- README preview SVG rendering has been fixed and validated locally.
- Marketing launch kit and static landing page draft are in `docs/`.
- Shared presentation builders for timeline, overview, and detail context.
- Local SQLite activity store and Windows foreground recorder.
- Azure DevOps currently has no open MomentTrace work items.

**Last pushed:** `04464ce` — update plan for beta release follow-ups.

---

## Pick Next Work

### Active Next Work — Beta Release Verification

**Why:** The release train now creates Windows and macOS artifacts together, but the beta should not be marketed until the generated assets are confirmed.

**Status:** Next.

**Scope:**

- Verify the `v0.1.1-beta.6` GitHub Actions release run finishes green.
- Confirm the GitHub Release has all expected artifacts:
  - `MomentTrace-0.1.1-beta.6-win-x64-setup.exe`
  - `MomentTrace-0.1.1-beta.6-win-x64.zip`
  - `MomentTrace-0.1.1-beta.6-macos-arm64.zip`
- Download and smoke test the Windows installer on a clean or disposable Windows profile.
- Download and inspect the macOS zip on a Mac, confirming it extracts to `MomentTrace.app` and launches far enough to show the shell/onboarding.
- Update `docs/marketing-launch-kit.md` with any beta caveats discovered during install testing.

**Acceptance criteria:**

- Release artifacts are present and named correctly.
- Windows installer launches the Avalonia shell, not the old WPF shell.
- macOS zip contains `MomentTrace.app`.
- Any signing, Gatekeeper, SmartScreen, or first-launch caveat is documented before public sharing.
- `stories.md` records the smoke-test result.

**Progress so far:**

- [2026-07-09] `v0.1.1-beta.6` macOS zip downloaded with the expected SHA-256 digest and passed `unzip -t`, but the extracted `MomentTrace.app` failed `codesign --verify --deep --strict` with `code has no resources but signature indicates they must be present`. Rebuild the macOS artifact after ad-hoc signing the bundle before public sharing.
- [2026-07-09] `v0.1.1-beta.7` confirmed the app bundle itself was ad-hoc signed, but strict verification still failed on unsigned nested files under `Contents/MacOS`. Sign those published files before sealing the outer bundle, then rebuild as the next replacement beta.
- [2026-07-09] `v0.1.1-beta.8` failed during CI because `MomentTrace.Desktop` was signed before sibling managed assemblies such as `System.Web.dll`. Sign sibling files first, then the app executable, then the outer bundle.

### Next After Release Verification — Marketing Launch Pass

**Why:** Once the artifacts are real, the marketing work should move from draft copy to public-facing launch assets.

**Scope:**

- Refresh README/landing CTA links if the final beta tag changes.
- Capture or select final sanitized screenshots from the beta build.
- Publish or export the static landing page draft from `docs/landing/index.html`.
- Prepare launch post, privacy post, and founder-story post from `docs/marketing-launch-kit.md`.
- Decide where feedback should go: GitHub Issues, email, form, or waitlist.

**Acceptance criteria:**

- Public download link points to a green release.
- Launch page uses only sanitized images.
- Feedback route is clear.
- Known beta caveats are visible and honest.

### Next After Marketing — Signing And Trust

**Why:** Unsigned artifacts are acceptable for an internal beta, but public distribution needs trust polish.

**Scope:**

- Add real Windows signing certificate secrets.
- Confirm Windows installer shows the expected verified publisher.
- Add Apple Developer ID signing and notarization secrets when available.
- Upgrade macOS output from unsigned zip toward signed/notarized DMG.

**Acceptance criteria:**

- Windows artifacts are signed in release workflow.
- macOS signing/notarization path is implemented or explicitly scheduled.
- User-facing install warnings are reduced and documented.

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
