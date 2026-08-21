import { and, count, desc, eq, sql } from "drizzle-orm";
import { env } from "cloudflare:workers";
import { getChatGPTUser } from "../../chatgpt-auth";
import { getDb } from "../../../db";
import { analyticsDailyMetrics } from "../../../db/schema";

const allowedEvents = new Set([
  "page_view",
  "join_waitlist_click",
  "see_timeline_click",
  "download_beta_click",
  "waitlist_submit_success",
]);

function metricDate() {
  return new Date().toISOString().slice(0, 10);
}

function cleanEventName(value: unknown) {
  return typeof value === "string" && allowedEvents.has(value) ? value : "";
}

function adminEmails() {
  return String(env.METRICS_ADMIN_EMAILS ?? "")
    .split(",")
    .map((email) => email.trim().toLowerCase())
    .filter(Boolean);
}

async function requireMetricsAccess() {
  const user = await getChatGPTUser();

  if (!user) {
    return Response.json({ error: "Sign in is required." }, { status: 401 });
  }

  const allowedEmails = adminEmails();
  if (
    allowedEmails.length > 0 &&
    !allowedEmails.includes(user.email.toLowerCase())
  ) {
    return Response.json({ error: "Metrics access denied." }, { status: 403 });
  }

  return null;
}

function toRouteErrorMessage(error: unknown) {
  const message = error instanceof Error ? error.message : "Unexpected error";
  const detail =
    error instanceof Error && error.cause instanceof Error ? error.cause.message : "";
  const combined = `${message}\n${detail}`;

  if (
    combined.includes("no such table") ||
    combined.includes("analytics_daily_metrics")
  ) {
    return "The analytics table is unavailable. Generate and deploy the D1 migration before collecting metrics.";
  }

  return message;
}

export async function GET() {
  try {
    const accessError = await requireMetricsAccess();
    if (accessError) {
      return accessError;
    }

    const db = getDb();
    const rows = await db
      .select({
        metricDate: analyticsDailyMetrics.metricDate,
        eventName: analyticsDailyMetrics.eventName,
        count: analyticsDailyMetrics.count,
      })
      .from(analyticsDailyMetrics)
      .orderBy(desc(analyticsDailyMetrics.metricDate));

    const [signupRow] = await db.select({ value: count() }).from(analyticsDailyMetrics);

    return Response.json({
      events: rows,
      metricRows: signupRow?.value ?? 0,
    });
  } catch (error) {
    return Response.json({ error: toRouteErrorMessage(error) }, { status: 500 });
  }
}

export async function POST(request: Request) {
  try {
    const payload = (await request.json()) as { eventName?: string };
    const eventName = cleanEventName(payload.eventName);

    if (!eventName) {
      return Response.json({ error: "Unknown analytics event." }, { status: 400 });
    }

    const db = getDb();
    const today = metricDate();

    await db
      .insert(analyticsDailyMetrics)
      .values({
        metricDate: today,
        eventName,
        count: 1,
      })
      .onConflictDoUpdate({
        target: [analyticsDailyMetrics.metricDate, analyticsDailyMetrics.eventName],
        set: {
          count: sql`${analyticsDailyMetrics.count} + 1`,
          updatedAt: new Date().toISOString(),
        },
      });

    const [row] = await db
      .select({ value: analyticsDailyMetrics.count })
      .from(analyticsDailyMetrics)
      .where(
        and(
          eq(analyticsDailyMetrics.metricDate, today),
          eq(analyticsDailyMetrics.eventName, eventName),
        ),
      )
      .limit(1);

    return Response.json({ ok: true, count: row?.value ?? 1 });
  } catch (error) {
    return Response.json({ error: toRouteErrorMessage(error) }, { status: 500 });
  }
}
