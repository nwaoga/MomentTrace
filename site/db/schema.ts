import { sql } from "drizzle-orm";
import { integer, sqliteTable, text, uniqueIndex } from "drizzle-orm/sqlite-core";

export const waitlistSignups = sqliteTable(
  "waitlist_signups",
  {
    id: integer("id").primaryKey({ autoIncrement: true }),
    name: text("name").notNull(),
    email: text("email").notNull(),
    normalizedEmail: text("normalized_email").notNull(),
    platform: text("platform").notNull(),
    useCase: text("use_case").notNull(),
    source: text("source").notNull().default("landing_page"),
    referrer: text("referrer").notNull().default(""),
    userAgent: text("user_agent").notNull().default(""),
    createdAt: text("created_at").notNull().default(sql`CURRENT_TIMESTAMP`),
    updatedAt: text("updated_at").notNull().default(sql`CURRENT_TIMESTAMP`),
  },
  (table) => ({
    normalizedEmailIdx: uniqueIndex("idx_waitlist_signups_normalized_email").on(
      table.normalizedEmail,
    ),
  }),
);

export const analyticsDailyMetrics = sqliteTable(
  "analytics_daily_metrics",
  {
    id: integer("id").primaryKey({ autoIncrement: true }),
    metricDate: text("metric_date").notNull(),
    eventName: text("event_name").notNull(),
    count: integer("count").notNull().default(0),
    updatedAt: text("updated_at").notNull().default(sql`CURRENT_TIMESTAMP`),
  },
  (table) => ({
    eventDateIdx: uniqueIndex("idx_analytics_daily_metrics_event_date").on(
      table.metricDate,
      table.eventName,
    ),
  }),
);
