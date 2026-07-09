# MomentTrace Marketing Launch Kit

This kit is for the first public beta push. It keeps the message simple: MomentTrace is a private, local-first memory timeline for your workday.

## Positioning

MomentTrace helps people remember what they did, where they were, and how to get back to important work without sending their activity history to a cloud service.

## One-Liner

MomentTrace is a local-first desktop memory timeline that helps you retrace apps, websites, screenshots, notes, and work moments from your day.

## Short Description

MomentTrace quietly builds a private timeline of your desktop activity so you can recover context, reopen work, and understand where your time went. Your history stays on your machine, with controls for privacy filtering, local cleanup, screenshots, bookmarks, and notes.

## Long Description

MomentTrace is a desktop app for people who lose time and context jumping between apps, documents, browser tabs, and tasks. It turns your workday into a searchable local timeline with screenshots, app and website context, saved moments, notes, and a daily Overview.

The first beta is focused on local-first recall. MomentTrace stores data on your device, provides privacy exclusions and adult-site filtering, and includes a full local-data cleanup option. It is built for users who want memory and recovery without turning their desktop history into a cloud product.

## Core Messages

- Remember your workday without manually tracking it.
- Recover the app, website, document, or screenshot you were looking at.
- Keep your timeline local to your machine.
- Filter sensitive activity and delete all local data when needed.
- Use Overview to understand the day at a glance, then jump into exact moments.

## Audience

- Solo founders and operators juggling many tasks.
- Developers, designers, and consultants who need to recover context fast.
- Knowledge workers who spend the day across apps, documents, meetings, and browser tabs.
- People who want a personal memory tool but dislike cloud-based activity tracking.

## Landing Page Outline

A static landing page draft is available at `docs/landing/index.html`. It uses sanitized product visuals from `docs/assets/` and points the beta CTA at `v0.1.1-beta.11`.

### Hero

Headline:

```text
Your private memory timeline for the workday.
```

Subhead:

```text
MomentTrace helps you retrace apps, websites, screenshots, notes, and saved moments from your day, with activity history stored locally on your device.
```

Primary CTA:

```text
Download Beta
```

Secondary CTA:

```text
See how it works
```

Trust line:

```text
Local-first. Private by default. Built for people who need to recover context fast.
```

### Feature Blocks

Timeline:

```text
See your day as a timeline of real work moments, not raw telemetry.
```

Overview:

```text
Get a visual recap of where your attention went, including top apps, focus blocks, and meaningful moments.
```

Recall:

```text
Reopen captured websites, folders, and saved moments so you can get back to the work itself.
```

Privacy:

```text
Keep data on-device, filter sensitive activity, and delete all local app data when you want a clean slate.
```

### Privacy Section

Headline:

```text
Your timeline belongs on your machine.
```

Body:

```text
MomentTrace stores activity history, screenshots, settings, and privacy filters locally. The beta does not require a cloud account, and cleanup controls let you remove captured history, previews, icons, settings, and cached privacy blocklists from the device.
```

## Screenshot Checklist

Use sanitized data only. Avoid showing real client names, private documents, personal browser tabs, email, chats, addresses, tokens, or finance/health content.

- Timeline with rich moment cards and detail pane.
- Overview with summary cards, heatmap, focus rhythm, and moments today.
- Saved Moments or Bookmarks view.
- Settings privacy controls.
- First-run onboarding.
- Full local-data cleanup confirmation.
- Theme collage using shipped themes.

Existing sanitized assets:

- `docs/assets/momenttrace-avalonia-shell-current.svg`
- `docs/assets/momenttrace-overview-dashboard.svg`
- `docs/assets/momenttrace-settings-privacy-preview.svg`
- `docs/assets/momenttrace-theme-collage.svg`

## Demo Video Script

Length: 45 to 60 seconds.

1. Open MomentTrace on Timeline.
2. Say: "MomentTrace turns your workday into a private timeline."
3. Scroll through a few moments and open the detail pane.
4. Show a screenshot preview and saved/bookmarked moment.
5. Switch to Overview.
6. Say: "Overview gives you a quick recap of your day, including top apps, focus rhythm, and key moments."
7. Search or filter to a website/app moment.
8. Open Settings privacy controls.
9. Say: "Your history stays local, with privacy filters and full local-data cleanup."
10. End on the download CTA.

## Social Posts

### Launch Post

```text
I am launching the first desktop beta of MomentTrace for Windows and macOS.

It is a local-first desktop memory timeline: apps, websites, screenshots, notes, saved moments, and a visual recap of your day.

Built for people who need to recover context fast without sending their activity history to the cloud.
```

### Privacy Post

```text
Most activity tools assume your work history should live in the cloud.

MomentTrace starts from the opposite assumption: your timeline should live on your machine.

The beta includes local storage, privacy filters, adult-site filtering, export/delete controls, and full local-data cleanup.
```

### Founder Story Post

```text
I built MomentTrace because I kept losing context across apps, browser tabs, documents, and tasks.

The app quietly builds a private timeline of the day so you can answer: what was I doing, where was that thing, and how do I get back to it?
```

## Product Hunt Copy

Tagline:

```text
A private desktop memory timeline for your workday
```

Description:

```text
MomentTrace helps you retrace apps, websites, screenshots, notes, and saved moments from your day. It is local-first, built for fast context recovery, and includes privacy filters plus local cleanup controls.
```

Topics:

```text
Productivity, Windows, Privacy, Developer Tools, Personal Analytics
```

## Beta Release Checklist

- GitHub prerelease exists for `v0.1.1-beta.11`.
- Windows installer, Windows portable zip, and macOS app zip are attached to the release.
- Clean Windows install smoke test passes.
- macOS app zip extracts to `MomentTrace.app`.
- App launches after install.
- First-run onboarding appears.
- Timeline starts recording.
- Settings opens.
- Full local-data cleanup confirmation opens but is not executed during public demo.
- README download link points to the latest beta release.
- Known caveat is stated clearly: Windows verified publisher requires signing certificate secrets, and macOS is ad-hoc signed but not Developer ID signed or notarized yet.

## Website CTA Copy

Primary:

```text
Download Beta
```

Secondary:

```text
Read the privacy notes
```

Footer:

```text
MomentTrace is in beta. Your feedback can shape the recall, privacy, and release experience before the first stable launch.
```

## Feedback Questions

- What were you trying to remember when MomentTrace helped or failed?
- Did any captured item feel too sensitive?
- Was the local cleanup/privacy model clear?
- Which moment views were actually useful after a few hours of use?
- What would make this worth keeping installed?
