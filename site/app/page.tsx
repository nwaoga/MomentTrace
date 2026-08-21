const betaReleaseUrl =
  "https://github.com/nwaoga/MomentTrace/releases/tag/v0.1.1-beta.15";
const signedBuildWaitlistUrl =
  "mailto:blakboi@hotmail.co.uk?subject=MomentTrace%20first%20signed%20build%20list&body=Hi%20Eric%2C%0A%0APlease%20add%20me%20to%20the%20MomentTrace%20first%20signed-build%20list.%0A%0AName%3A%20%0APlatform%3A%20Windows%20%2F%20macOS%0AWhat%20I%20want%20MomentTrace%20for%3A%20";

export default function Home() {
  return (
    <>
      <header className="topbar" aria-label="Primary">
        <div className="brand">
          <span className="mark" aria-hidden="true" /> MomentTrace
        </div>
        <nav className="nav">
          <a href="#features">Features</a>
          <a href="#privacy">Privacy</a>
          <a href="#beta">Beta</a>
          <a href="#waitlist" className="button primary">
            Join Waitlist
          </a>
        </nav>
      </header>

      <main>
        <section className="hero">
          <div className="section-inner hero-grid">
            <div className="hero-content">
              <span className="eyebrow">Early access for Windows and macOS</span>
              <h1>Find the work you were just thinking about.</h1>
              <p className="lede">
                MomentTrace is a private desktop timeline for apps, websites,
                screenshots, notes, and saved moments. It helps you recover
                context without sending your work history to a cloud account.
              </p>
              <div className="hero-actions">
                <a className="button primary" href={signedBuildWaitlistUrl}>
                  Join the Signed-Build List
                </a>
                <a className="button secondary" href="#showcase">
                  See the Timeline
                </a>
              </div>
              <div className="signal-strip" aria-label="Launch signal">
                <span>Goal</span>
                <strong>25 named yeses</strong>
                <span>before buying signing certificates</span>
              </div>
            </div>
            <div className="hero-visual" aria-label="MomentTrace app preview">
              <img
                src="/momenttrace-avalonia-shell-current.png"
                alt="MomentTrace desktop app showing the private timeline interface"
              />
              <div className="preview-badge">
                <strong>Local-first beta</strong>
                <span>Stored on your device</span>
              </div>
            </div>
          </div>
        </section>

        <section className="intro-band" aria-label="MomentTrace positioning">
          <div className="section-inner intro-grid">
            <p>
              Built for the 4pm problem: you remember the tab, folder, app, or
              screenshot existed. You just do not remember where.
            </p>
            <div className="mini-points">
              <span>Private by default</span>
              <span>No account required</span>
              <span>Delete everything locally</span>
            </div>
          </div>
        </section>

        <section id="features">
          <div className="section-inner">
            <div className="section-head">
              <h2>Recover context without manual tracking.</h2>
              <p className="section-copy">
                MomentTrace turns the day into useful moments, then gives you
                the controls to search, save, revisit, and clean up your
                history.
              </p>
            </div>
            <div className="feature-grid">
              <article className="feature">
                <strong>Timeline</strong>
                <p>
                  Scan your day as app, website, document, folder, and
                  screenshot moments.
                </p>
              </article>
              <article className="feature">
                <strong>Overview</strong>
                <p>
                  Get a visual recap of attention, top apps, focus blocks,
                  activity patterns, and key moments.
                </p>
              </article>
              <article className="feature">
                <strong>Recall</strong>
                <p>
                  Reopen captured websites, folders, and saved work from the
                  moment you found.
                </p>
              </article>
              <article className="feature">
                <strong>Privacy</strong>
                <p>
                  Keep data local, filter sensitive activity, export or delete
                  history, and clear all local app data.
                </p>
              </article>
            </div>
          </div>
        </section>

        <section id="showcase" className="showcase">
          <div className="section-inner media-row">
            <div>
              <h2>Find the moment, then get back to the work.</h2>
              <p className="section-copy">
                Search or scan through captured activity, save important
                moments, add notes, and use previews to recognize what you were
                doing before you reopen the source.
              </p>
              <ul className="list">
                <li>Capture apps, websites, screenshots, folders, and timestamps.</li>
                <li>Bookmark and pin the moments that matter.</li>
                <li>
                  Use Overview when you want the story of the day instead of a
                  raw event log.
                </li>
              </ul>
            </div>
            <img
              src="/momenttrace-timeline-detail.png"
              alt="MomentTrace timeline with screenshot detail pane"
            />
          </div>
        </section>

        <section id="privacy" className="privacy">
          <div className="section-inner privacy-band">
            <div>
              <h2>Your timeline belongs on your machine.</h2>
              <p className="section-copy">
                The beta stores activity history, screenshots, settings, and
                privacy filters locally. It does not require a cloud account.
              </p>
            </div>
            <div className="privacy-points">
              <div className="privacy-point">
                <strong>Local storage</strong>
                <span>
                  Captured data is written under your device&apos;s MomentTrace
                  app data folder.
                </span>
              </div>
              <div className="privacy-point">
                <strong>Privacy filters</strong>
                <span>
                  Block apps or websites and keep adult-site filtering enabled
                  by default.
                </span>
              </div>
              <div className="privacy-point">
                <strong>Cleanup controls</strong>
                <span>
                  Delete activity history or clear all local app data when you
                  want a clean slate.
                </span>
              </div>
              <div className="privacy-point">
                <strong>No account required</strong>
                <span>
                  The MVP is fully local and deterministic, with AI provider
                  hooks left off by default.
                </span>
              </div>
            </div>
          </div>
        </section>

        <section id="waitlist" className="waitlist">
          <div className="section-inner waitlist-band">
            <div>
              <span className="eyebrow">First signed-build list</span>
              <h2>Help decide when MomentTrace buys trust.</h2>
              <p className="section-copy">
                MomentTrace is collecting named interest before spending on
                Windows Authenticode signing and macOS Developer ID
                notarization. The trigger is simple: 25+ real people asking for
                the first signed build.
              </p>
            </div>
            <div className="signal-panel">
              <span className="signal-number">0 / 25</span>
              <strong>Named requests so far</strong>
              <p>
                Send your name, platform, and one sentence about why you want a
                private workday timeline. You will be added to the first
                signed-build list.
              </p>
              <a className="button primary" href={signedBuildWaitlistUrl}>
                Email Me In
              </a>
            </div>
          </div>
        </section>

        <section id="beta" className="beta">
          <div className="section-inner">
            <div className="section-head">
              <h2>Need to try the current beta?</h2>
              <p className="section-copy">
                The functional beta is available for people who already want to
                test it, but broad public sharing waits until the installers are
                signed and trusted by the operating system.
              </p>
            </div>
            <div className="beta-grid">
              <div className="beta-item">
                <h3>Beta checklist</h3>
                <p>
                  Install, complete onboarding, let Timeline run, check
                  Overview, save a moment, open Settings, and inspect privacy
                  controls.
                </p>
              </div>
              <div className="beta-item">
                <h3>Trust caveat</h3>
                <p>
                  Windows and macOS builds are not yet signed with public
                  release certificates, so first-run security prompts are
                  expected.
                </p>
              </div>
            </div>
            <p className="beta-action">
              <a className="button secondary" href={betaReleaseUrl}>
                Download Unsigned Beta
              </a>
            </p>
          </div>
        </section>
      </main>

      <footer className="footer">
        <span>MomentTrace beta</span>
        <span>Private workday recall, stored locally.</span>
      </footer>
    </>
  );
}
