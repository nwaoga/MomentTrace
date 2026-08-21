import { count } from "drizzle-orm";
import { env } from "cloudflare:workers";
import {
  chatGPTSignOutPath,
  requireChatGPTUser,
} from "../../chatgpt-auth";
import { getDb } from "../../../db";
import { analyticsDailyMetrics, waitlistSignups } from "../../../db/schema";

export const dynamic = "force-dynamic";

type EventName =
  | "page_view"
  | "join_waitlist_click"
  | "see_timeline_click"
  | "download_beta_click"
  | "waitlist_submit_success";

const eventLabels: Record<EventName, string> = {
  page_view: "Page views",
  join_waitlist_click: "Join waitlist clicks",
  see_timeline_click: "Timeline clicks",
  download_beta_click: "Beta download clicks",
  waitlist_submit_success: "Successful waitlist submissions",
};

function adminEmails() {
  return String(env.METRICS_ADMIN_EMAILS ?? "")
    .split(",")
    .map((email) => email.trim().toLowerCase())
    .filter(Boolean);
}

function formatRate(numerator: number, denominator: number) {
  if (!denominator) {
    return "0.0%";
  }

  return `${((numerator / denominator) * 100).toFixed(1)}%`;
}

export default async function MetricsPage() {
  const user = await requireChatGPTUser("/admin/metrics");
  const allowedEmails = adminEmails();
  const isAllowed =
    allowedEmails.length === 0 ||
    allowedEmails.includes(user.email.toLowerCase());

  if (!isAllowed) {
    return (
      <main className="admin-shell">
        <section className="admin-denied">
          <span className="eyebrow">Admin only</span>
          <h1>Metrics are private.</h1>
          <p>
            You are signed in as {user.email}, but this account is not on the
            MomentTrace metrics allowlist.
          </p>
          <a className="button secondary" href={chatGPTSignOutPath("/admin/metrics")}>
            Sign out
          </a>
        </section>
      </main>
    );
  }

  const db = getDb();
  const [signupRow, metricRows] = await Promise.all([
    db.select({ value: count() }).from(waitlistSignups),
    db
      .select({
        metricDate: analyticsDailyMetrics.metricDate,
        eventName: analyticsDailyMetrics.eventName,
        count: analyticsDailyMetrics.count,
      })
      .from(analyticsDailyMetrics),
  ]);

  const totals = Object.keys(eventLabels).reduce(
    (accumulator, eventName) => {
      accumulator[eventName as EventName] = 0;
      return accumulator;
    },
    {} as Record<EventName, number>,
  );

  const dailyTotals = new Map<string, Record<EventName, number>>();

  for (const row of metricRows) {
    const eventName = row.eventName as EventName;

    if (!(eventName in eventLabels)) {
      continue;
    }

    totals[eventName] += row.count;

    const day =
      dailyTotals.get(row.metricDate) ??
      ({
        page_view: 0,
        join_waitlist_click: 0,
        see_timeline_click: 0,
        download_beta_click: 0,
        waitlist_submit_success: 0,
      } satisfies Record<EventName, number>);

    day[eventName] += row.count;
    dailyTotals.set(row.metricDate, day);
  }

  const signupCount = signupRow[0]?.value ?? 0;
  const waitlistConversion = formatRate(
    totals.waitlist_submit_success,
    totals.page_view,
  );
  const joinClickConversion = formatRate(
    totals.join_waitlist_click,
    totals.page_view,
  );
  const downloadConversion = formatRate(
    totals.download_beta_click,
    totals.page_view,
  );
  const sortedDays = [...dailyTotals.entries()].sort(([a], [b]) =>
    b.localeCompare(a),
  );

  return (
    <main className="admin-shell">
      <section className="admin-header">
        <div>
          <span className="eyebrow">MomentTrace admin</span>
          <h1>Launch Metrics</h1>
          <p>
            Anonymous daily counters for the public beta page. Signed in as{" "}
            <strong>{user.email}</strong>.
          </p>
        </div>
        <a className="button secondary" href="/">
          Back to site
        </a>
      </section>

      <section className="admin-kpis" aria-label="Launch metrics summary">
        <article>
          <span>Named waitlist</span>
          <strong>{signupCount} / 25</strong>
          <p>{25 - Math.min(signupCount, 25)} more before the signing trigger.</p>
        </article>
        <article>
          <span>Page views</span>
          <strong>{totals.page_view}</strong>
          <p>Anonymous landing page loads.</p>
        </article>
        <article>
          <span>Waitlist conversion</span>
          <strong>{waitlistConversion}</strong>
          <p>Successful submissions from page views.</p>
        </article>
        <article>
          <span>Download intent</span>
          <strong>{downloadConversion}</strong>
          <p>Unsigned beta clicks from page views.</p>
        </article>
      </section>

      <section className="admin-grid">
        <article className="admin-panel">
          <div className="admin-panel-head">
            <h2>Totals</h2>
            <p>What people are doing on the landing page.</p>
          </div>
          <div className="metric-list">
            {(Object.keys(eventLabels) as EventName[]).map((eventName) => (
              <div className="metric-row" key={eventName}>
                <span>{eventLabels[eventName]}</span>
                <strong>{totals[eventName]}</strong>
              </div>
            ))}
          </div>
        </article>

        <article className="admin-panel">
          <div className="admin-panel-head">
            <h2>Signals</h2>
            <p>The checks that tell us when signing starts to make sense.</p>
          </div>
          <div className="metric-list">
            <div className="metric-row">
              <span>Join click rate</span>
              <strong>{joinClickConversion}</strong>
            </div>
            <div className="metric-row">
              <span>Download click rate</span>
              <strong>{downloadConversion}</strong>
            </div>
            <div className="metric-row">
              <span>Named requests remaining</span>
              <strong>{Math.max(25 - signupCount, 0)}</strong>
            </div>
          </div>
        </article>
      </section>

      <section className="admin-panel admin-table-panel">
        <div className="admin-panel-head">
          <h2>Daily Detail</h2>
          <p>Recent daily counters, newest first.</p>
        </div>
        <div className="admin-table-wrap">
          <table className="admin-table">
            <thead>
              <tr>
                <th>Date</th>
                <th>Views</th>
                <th>Join clicks</th>
                <th>Timeline</th>
                <th>Downloads</th>
                <th>Submissions</th>
              </tr>
            </thead>
            <tbody>
              {sortedDays.length ? (
                sortedDays.map(([date, day]) => (
                  <tr key={date}>
                    <td>{date}</td>
                    <td>{day.page_view}</td>
                    <td>{day.join_waitlist_click}</td>
                    <td>{day.see_timeline_click}</td>
                    <td>{day.download_beta_click}</td>
                    <td>{day.waitlist_submit_success}</td>
                  </tr>
                ))
              ) : (
                <tr>
                  <td colSpan={6}>No metrics have been collected yet.</td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </section>
    </main>
  );
}
