const betaReleaseUrl =
  "https://github.com/nwaoga/MomentTrace/releases/tag/v0.1.1-beta.15";

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
          <a href="#beta" className="button primary">
            Download Beta
          </a>
        </nav>
      </header>

      <main>
        <section className="hero">
          <div className="hero-content">
            <span className="eyebrow">Windows and macOS beta now available</span>
            <h1>Your private memory timeline for the workday.</h1>
            <p className="lede">
              MomentTrace helps you retrace apps, websites, screenshots, notes,
              and saved moments from your day, with activity history stored
              locally on your device.
            </p>
            <div className="hero-actions">
              <a className="button primary" href={betaReleaseUrl}>
                Download Beta
              </a>
              <a className="button secondary" href="#showcase">
                See how it works
              </a>
            </div>
            <p className="hero-note">
              Local-first. Private by default. Built for people who need to
              recover context fast.
            </p>
          </div>
        </section>

        <section className="poster" aria-label="MomentTrace promotional overview">
          <div className="section-inner poster-frame">
            <img
              src="/momenttrace-promotional-poster.png"
              alt="MomentTrace promotional overview showing Overview, Timeline, recall, and privacy features"
            />
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
                  See your workday as a timeline of real app, website, document,
                  and screenshot moments.
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
                  Reopen captured websites, folders, and saved work so you can
                  get back to the thing itself.
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
            <img
              src="/momenttrace-timeline-detail.png"
              alt="MomentTrace timeline with screenshot detail pane"
            />
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

        <section id="beta" className="beta">
          <div className="section-inner">
            <div className="section-head">
              <h2>Try the desktop beta.</h2>
              <p className="section-copy">
                Install it, use it for a real work session, then tell us what
                helped you recover context and what felt too sensitive or noisy.
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
                <h3>Known release note</h3>
                <p>
                  The verified-publisher flow is ready, but public installers
                  need a real Windows signing certificate configured in release
                  secrets.
                </p>
              </div>
            </div>
            <p className="beta-action">
              <a className="button primary" href={betaReleaseUrl}>
                Download Beta
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
