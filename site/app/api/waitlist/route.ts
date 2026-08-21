import { count } from "drizzle-orm";
import { getDb } from "../../../db";
import { waitlistSignups } from "../../../db/schema";

const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
const allowedPlatforms = new Set(["windows", "macos", "both"]);

function cleanText(value: unknown, maxLength: number) {
  return typeof value === "string" ? value.trim().slice(0, maxLength) : "";
}

function toRouteErrorMessage(error: unknown) {
  const message = error instanceof Error ? error.message : "Unexpected error";
  const detail =
    error instanceof Error && error.cause instanceof Error ? error.cause.message : "";
  const combined = `${message}\n${detail}`;

  if (combined.includes("no such table") || combined.includes("waitlist_signups")) {
    return "The waitlist table is unavailable. Generate and deploy the D1 migration before collecting signups.";
  }

  return message;
}

export async function GET() {
  try {
    const db = getDb();
    const [row] = await db.select({ value: count() }).from(waitlistSignups);
    return Response.json({ count: row?.value ?? 0, goal: 25 });
  } catch (error) {
    return Response.json(
      { error: toRouteErrorMessage(error) },
      { status: 500 },
    );
  }
}

export async function POST(request: Request) {
  try {
    const payload = (await request.json()) as {
      name?: string;
      email?: string;
      platform?: string;
      useCase?: string;
      source?: string;
      website?: string;
    };

    if (cleanText(payload.website, 120)) {
      return Response.json({ ok: true, count: 0, goal: 25 });
    }

    const name = cleanText(payload.name, 120);
    const email = cleanText(payload.email, 180);
    const normalizedEmail = email.toLowerCase();
    const platform = cleanText(payload.platform, 20);
    const useCase = cleanText(payload.useCase, 600);
    const source = cleanText(payload.source, 80) || "landing_page";

    if (!name) {
      return Response.json({ error: "Name is required." }, { status: 400 });
    }

    if (!emailPattern.test(email)) {
      return Response.json({ error: "A valid email is required." }, { status: 400 });
    }

    if (!allowedPlatforms.has(platform)) {
      return Response.json({ error: "Choose Windows, macOS, or both." }, { status: 400 });
    }

    if (!useCase) {
      return Response.json(
        { error: "Tell us what you want MomentTrace for." },
        { status: 400 },
      );
    }

    const db = getDb();
    await db
      .insert(waitlistSignups)
      .values({
        name,
        email,
        normalizedEmail,
        platform,
        useCase,
        source,
        referrer: request.headers.get("referer")?.slice(0, 500) ?? "",
        userAgent: request.headers.get("user-agent")?.slice(0, 500) ?? "",
      })
      .onConflictDoUpdate({
        target: waitlistSignups.normalizedEmail,
        set: {
          name,
          email,
          platform,
          useCase,
          source,
          referrer: request.headers.get("referer")?.slice(0, 500) ?? "",
          userAgent: request.headers.get("user-agent")?.slice(0, 500) ?? "",
          updatedAt: new Date().toISOString(),
        },
      });

    const [row] = await db.select({ value: count() }).from(waitlistSignups);
    return Response.json({ ok: true, count: row?.value ?? 0, goal: 25 }, { status: 201 });
  } catch (error) {
    return Response.json(
      { error: toRouteErrorMessage(error) },
      { status: 500 },
    );
  }
}
