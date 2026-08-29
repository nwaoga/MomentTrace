# MomentTrace Marketing Launch Kit

This kit is for the first public beta push. It keeps the message simple: MomentTrace helps you recover the work you were just doing, while keeping your history local to your machine.

## Positioning

MomentTrace helps people remember what they did, where they were, and how to get back to important work without sending their activity history to a cloud service.

## One-Liner

MomentTrace is a local-first desktop recall app that helps you retrace apps, websites, screenshots, notes, and work moments from your day.

## Short Description

MomentTrace quietly captures a private local history of your desktop activity so you can recover context, reopen work, and understand where your time went. Nothing leaves your machine unless you choose to export it, and the app includes privacy filtering, local cleanup, screenshots, bookmarks, and notes.

## Long Description

MomentTrace is a desktop app for people who lose time and context jumping between apps, documents, browser tabs, and tasks. It turns your workday into a searchable local timeline with screenshots, app and website context, saved moments, notes, and a daily Overview.

The first beta is focused on local-first recall. MomentTrace stores data on your device, provides privacy exclusions and adult-site filtering, and includes a full local-data cleanup option. It is built for users who want memory and recovery without turning their desktop history into a cloud product.

Public paid-beta or founder-license offers should wait until Windows and macOS artifacts are available from the same beta tag and have both passed install, launch, and background-running smoke tests. The launch can still explain platform-specific trust caveats, but the commercial offer should feel cross-platform from day one.

## Core Messages

- Recover the work you were just doing without manual tracking.
- Recover the app, website, document, or screenshot you were looking at.
- Keep your timeline local to your machine.
- Filter sensitive activity and delete all local data when needed.
- Use Overview to understand the day at a glance, then jump into exact moments.

## Early Feedback Adjustments

First public community feedback on 2026-08-23 highlighted two messaging changes worth keeping:

- Lead with local/private reassurance above the fold. "Nothing leaves your machine" should appear before people have time to imagine cloud tracking or screen recording.
- Avoid using "desktop memory timeline" as the first-frame description in cold traffic. Some readers briefly interpret it as screen recording, which raises the wrong trust concern before they understand the product.

The same feedback also pointed to a product direction: the raw app-by-app capture layer may need a higher-level "job" or "session" grouping mode so people can recover a block of work the next morning instead of revisiting every individual moment.

## Audience

- Solo founders and operators juggling many tasks.
- Developers, designers, and consultants who need to recover context fast.
- Knowledge workers who spend the day across apps, documents, meetings, and browser tabs.
- People who want a personal memory tool but dislike cloud-based activity tracking.

## Landing Page Outline

A static landing page draft is available at `docs/landing/index.html`. It uses sanitized product visuals from `docs/assets/` and points the beta CTA at the GitHub Releases page until the replacement beta tag is cut.

### Hero

Headline:

```text
Recover the work you were just doing.
```

Subhead:

```text
MomentTrace is a local-first desktop recall app for apps, websites, screenshots, notes, and saved moments. It helps you get back to the right tab, file, app, or screenshot without sending your work history to a cloud account.
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
Nothing leaves your machine unless you choose to export it.
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

- `docs/assets/momenttrace-promotional-poster.png`
- `docs/assets/momenttrace-avalonia-shell-current.svg`
- `docs/assets/momenttrace-avalonia-shell-current.png`
- `docs/assets/momenttrace-timeline-detail.svg`
- `docs/assets/momenttrace-timeline-detail.png`
- `docs/assets/momenttrace-settings-privacy-preview.svg`
- `docs/assets/momenttrace-settings-privacy-preview.png`
- `docs/assets/momenttrace-theme-collage.svg`
- `docs/assets/momenttrace-theme-collage.png`
- `docs/assets/momenttrace-reddit-teaser.mp4`

Recommended poster usage:

- Use `docs/assets/momenttrace-promotional-poster.png` as the canonical launch poster for README sharing, the landing page visual section, and social posts where one image needs to explain the product.
- Use tighter product screenshots for release verification, install notes, or places where exact build identity must be inspected.
- Refresh the poster after the next replacement beta if the mocked build label becomes prominent in final public crops.

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

## Pre-Investment Hype (before signing spend)

Do not Product Hunt, Show HN, or cold-traffic a Download Beta link until Windows is Authenticode-signed and macOS is Developer ID notarized. First-run SmartScreen / Gatekeeper will write the public story for you.

This phase is demand signal: founder posts, a waitlist of named people, GitHub stars. Offer the current unsigned `v0.1.1-beta.15` only to people who already asked, with the trust caveat in the same message.

**Primary ask:** reply / DM / email to join the first signed-build list.

**Primary image:** `docs/assets/momenttrace-promotional-poster.png`

**Do not ask for money.** Paid/founder-license stays blocked until both platforms ship from the same trusted tag.

First Reddit signal on 2026-08-29: the `r/SideProject` teaser reached about 1.1K views after 6 days, with 6 upvotes, a 100% upvote ratio, 2 comments, 3 shares, and 0 reposts. The matching landing-page metrics snapshot showed 26 page views, 1 join-waitlist click, 1 successful waitlist signup, 1 beta download click, 0 timeline clicks, 3.8% waitlist conversion, and 3.8% download intent. Read this as early awareness plus one real named signal, not enough demand for paid/founder-license promotion. Next post should test a sharper founder-story or privacy-contrast angle rather than repeat the same teaser.

### Next Marketing Handoff (2026-08-29)

Recommended next channel: LinkedIn founder/operator post. Use X only if the personal audience there is meaningfully stronger. LinkedIn is the better next soft test because it can produce named replies/DMs from people who feel the lost-context problem at work, without creating broad cold-download pressure around the current unsigned/not-notarized builds.

Angle: lost work context plus privacy contrast. Lead with the problem, then make the local-first posture explicit before mentioning the beta.

Post copy:

```text
I keep losing the thread of what I was doing.

Not the big project. The tiny handoff moments:

- which tab had the thing
- which folder I was in
- which screenshot explained the bug
- which app I switched away from before the meeting

Most tools that solve this want to upload your work history somewhere.

I am testing the opposite idea with MomentTrace: a private desktop timeline that stays on your machine. Apps, websites, screenshots, notes, saved moments, and a daily recap, with privacy filters and full local cleanup.

The Windows + macOS beta exists, but I am not broadly pushing downloads until signing, notarization, and updates are stronger.

Right now I am looking for signal from people who lose context across tabs/apps all day:

Would you want a local-first recall tool like this, or is desktop activity history a hard no even if it stays on-device?
```

Success bar for this post: 3+ named replies/DMs or waitlist signups from people who can describe a real lost-context use case, plus no recurring confusion that MomentTrace is cloud surveillance or continuous public screen recording. A strong secondary signal is 50+ landing-page visits with at least 2 waitlist joins and 1+ direct qualitative objection about privacy, signing, or update trust. Do not judge success by beta downloads yet.

Follow-up if it works: reply manually, ask what they were trying to recover, and invite them onto the first signed-build list. Do not ask for money and do not move to Product Hunt, Show HN, or paid-founder-license messaging.

Posting update: the LinkedIn version should include the public beta link in the post body and attach `docs/assets/momenttrace-promotional-poster.png`. This is acceptable for the soft test because the copy still frames the ask as feedback/signal, says downloads are not being broadly pushed yet, and avoids any paid/founder-license CTA.

After posting, capture a 24-48 hour snapshot covering LinkedIn reactions/comments/reposts, named replies/DMs, landing-page views, waitlist joins, beta download clicks, and recurring privacy/trust objections.

### Founder Story Post

```text
I kept losing context across apps, tabs, documents, and tasks.

So I built MomentTrace: a local-first desktop memory timeline. It quietly records the workday as moments you can search, reopen, and recap.

No cloud account. History stays on your machine.

Windows + macOS beta exists. I am collecting early interest before I spend on code signing / notarization.

If this is a problem you have, reply "timeline" and I will add you to the first signed-build list.
```

### Privacy Contrast Post

```text
Most activity tools assume your work history should live in the cloud.

MomentTrace starts from the opposite assumption: your timeline should live on your machine.

Local storage. Privacy filters. Adult-site filtering on by default. Export, delete, or wipe everything.

I am not asking you to install an unsigned build. I am asking whether you want this class of tool at all.
```

### 4pm Problem Post

```text
It is 4pm. You know you had the right tab, the right folder, the right screenshot.

You do not remember which app it was in.

That is the product.

MomentTrace turns the day into a private timeline of apps, websites, screenshots, and notes so you can get back to the work itself.
```

### Indie Hackers Post

```text
Building in public: MomentTrace.

Problem: I lose work context all day (apps, sites, folders, screenshots).

Solution: a local-first desktop timeline. Windows + macOS. No account. Data never leaves the machine unless you export it.

Current status: functional beta. Not publicly pushing downloads yet because Windows is unsigned and macOS is ad-hoc (Gatekeeper). I want demand signal before I pay for Authenticode + Apple Developer ID.

Question for IH: would you install a memory-timeline app if it stayed 100% local? What would make it a hard no?
```

### Two-week cadence

1. Founder story (LinkedIn or X) + poster.
2. Privacy contrast + Settings privacy crop.
3. 4pm problem + timeline/detail screenshot.
4. Indie Hackers long post.
5. Repeat founder story on the other network; ask for a GitHub star.
6-7. Reply to every comment. No new post.
8. Overview as the daily story.
9. What stays local.
10. 45-60s demo from the script below.
11. Quiet: move yeses onto a named list.
12. If 25+ named yeses, say so.
13-14. Decide whether to buy certs.

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

- GitHub prerelease exists for the latest verified beta tag.
- GitHub Actions artifact storage quota has enough headroom for Windows and macOS release uploads.
- Windows installer, Windows portable zip, and macOS app zip are attached to the release.
- Windows and macOS artifacts come from the same beta tag before any paid/founder-license CTA is promoted.
- Settings > Where I Live shows the expected release label, build number, and short commit.
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
