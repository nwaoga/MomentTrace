CREATE TABLE `analytics_daily_metrics` (
	`id` integer PRIMARY KEY AUTOINCREMENT NOT NULL,
	`metric_date` text NOT NULL,
	`event_name` text NOT NULL,
	`count` integer DEFAULT 0 NOT NULL,
	`updated_at` text DEFAULT CURRENT_TIMESTAMP NOT NULL
);
--> statement-breakpoint
CREATE UNIQUE INDEX `idx_analytics_daily_metrics_event_date` ON `analytics_daily_metrics` (`metric_date`,`event_name`);