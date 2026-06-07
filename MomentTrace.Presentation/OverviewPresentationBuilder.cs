using MomentTrace.Core;

namespace MomentTrace.Presentation;

public sealed class OverviewPresentationBuilder(TimelinePresentationBuilder timelineBuilder)
{
    private readonly TimelinePresentationBuilder _timelineBuilder = timelineBuilder;
    private static readonly ActivityCategory[] BreakdownOrder =
    [
        ActivityCategory.Other,
        ActivityCategory.Browsing,
        ActivityCategory.Development,
        ActivityCategory.Documents,
        ActivityCategory.System
    ];

    public OverviewPresentationModel Build(IReadOnlyList<TimelineActivity> activities, DateTime selectedDate)
    {
        var dayActivities = activities
            .Where(activity => activity.StartedAt.LocalDateTime.Date == selectedDate)
            .OrderBy(activity => activity.StartedAt)
            .ToList();
        var totalActive = dayActivities.Aggregate(TimeSpan.Zero, (total, activity) => total + activity.Duration);
        var sessions = _timelineBuilder.BuildSessions(dayActivities)
            .OrderBy(activity => activity.SessionStartedAt)
            .ToList();
        var topApplications = BuildTopApplications(dayActivities, totalActive).ToList();
        var focusBlocks = BuildFocusBlocks(sessions);
        var longestFocus = focusBlocks.Where(block => !block.IsGap).OrderByDescending(block => block.Duration).FirstOrDefault();
        var keyMoments = BuildKeyMoments(sessions).ToList();
        var categoryBreakdown = BuildCategoryBreakdown(dayActivities, totalActive).ToList();

        return new OverviewPresentationModel
        {
            DateLabel = selectedDate.ToString("dddd, d MMMM"),
            TotalActive = totalActive,
            Sessions = sessions,
            TopApplications = topApplications,
            FocusBlocks = focusBlocks,
            HeatmapSegments = BuildHeatmap(dayActivities, selectedDate).ToList(),
            KeyMoments = keyMoments,
            CategoryBreakdown = categoryBreakdown,
            Legend =
            [
                new("Apps and tools", ActivityPalette.Apps),
                new("Web browsing", ActivityPalette.Web),
                new("Development work", ActivityPalette.Development),
                new("Documents", ActivityPalette.Documents),
                new("Gaps or idle space", ActivityPalette.Gap)
            ],
            LongestFocusDuration = longestFocus is null ? "None yet" : FormatDuration(longestFocus.Duration),
            DaySummaryMetrics = BuildDaySummaryMetrics(selectedDate, sessions, totalActive, topApplications, longestFocus, dayActivities),
            SummaryMetrics =
            [
                new("Remembered Span", FormatDuration(totalActive), "Foreground time captured locally"),
                new("Moments", sessions.Count.ToString(), "Grouped from related activity"),
                new("Most Seen App", topApplications.FirstOrDefault()?.AppName ?? "No activity yet", topApplications.FirstOrDefault()?.Duration ?? "Waiting for activity"),
                new("Longest Visible Run", longestFocus is null ? "No run yet" : FormatDuration(longestFocus.Duration), longestFocus?.Label ?? "Visible blocks appear here")
            ]
        };
    }

    public static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
        {
            return $"{(int)duration.TotalHours}h {duration.Minutes}m";
        }

        if (duration.TotalMinutes >= 1)
        {
            return $"{Math.Max(1, (int)Math.Round(duration.TotalMinutes))}m";
        }

        return duration.TotalSeconds > 0 ? $"{Math.Max(1, (int)Math.Round(duration.TotalSeconds))}s" : "0m";
    }

    private static IReadOnlyList<DaySummaryMetricViewModel> BuildDaySummaryMetrics(
        DateTime selectedDate,
        IReadOnlyList<ActivityMomentViewModel> sessions,
        TimeSpan totalActive,
        IReadOnlyList<TopApplicationViewModel> topApplications,
        FocusBlockViewModel? longestFocus,
        IReadOnlyList<TimelineActivity> dayActivities)
    {
        var dayStart = selectedDate.Date;
        var dayEnd = dayStart.AddDays(1);
        var appsUsed = dayActivities
            .Select(activity => activity.AppName)
            .Where(app => !string.IsNullOrWhiteSpace(app))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        var idleTime = dayEnd - dayStart - totalActive;
        if (idleTime < TimeSpan.Zero)
        {
            idleTime = TimeSpan.Zero;
        }

        var busiestMoment = sessions
            .OrderByDescending(session => session.SessionEndedAt - session.SessionStartedAt)
            .ThenByDescending(session => session.OccurrenceCount)
            .FirstOrDefault();

        return
        [
            new("Selected date", selectedDate.ToString("ddd, d MMM"), "The day currently being summarised", ActivityPalette.Apps, "◴"),
            new("Moments captured", sessions.Count.ToString(), "Grouped local moments for this day", ActivityPalette.Web, "◫"),
            new("Active time", FormatDuration(totalActive), busiestMoment is null ? "No activity detected" : $"Mostly around {busiestMoment.CardTime}", ActivityPalette.Development, "∿"),
            new("Applications used", appsUsed.ToString(), topApplications.FirstOrDefault()?.AppName ?? "Waiting for activity", ActivityPalette.Documents, "⌘"),
            new("Longest activity", longestFocus is null ? "0m" : FormatDuration(longestFocus.Duration), longestFocus?.Label ?? "No visible run yet", ActivityPalette.SystemActivity, "◎"),
            new("Idle time", FormatDuration(idleTime), idleTime > TimeSpan.Zero ? "No activity detected" : "Fully active day", ActivityPalette.Gap, "☾")
        ];
    }

    private static IEnumerable<HeatmapSegmentViewModel> BuildHeatmap(IReadOnlyList<TimelineActivity> activities, DateTime day)
    {
        var blockLength = TimeSpan.FromMinutes(15);
        var maxSeconds = blockLength.TotalSeconds;

        for (var index = 0; index < 96; index++)
        {
            var start = day.AddTicks(blockLength.Ticks * index);
            var end = start.Add(blockLength);
            var overlapping = activities
                .Select(activity => new
                {
                    Activity = activity,
                    Seconds = GetOverlapSeconds(activity.StartedAt.LocalDateTime, activity.EndedAt.LocalDateTime, start, end)
                })
                .Where(item => item.Seconds > 0)
                .ToList();

            var activeSeconds = overlapping.Sum(item => item.Seconds);
            var dominant = overlapping
                .GroupBy(item => item.Activity.Kind)
                .OrderByDescending(group => group.Sum(item => item.Seconds))
                .FirstOrDefault()?.Key;
            var topApp = overlapping
                .GroupBy(item => item.Activity.AppName)
                .OrderByDescending(group => group.Sum(item => item.Seconds))
                .FirstOrDefault()?.Key ?? "No activity";
            var intensity = Math.Clamp(activeSeconds / maxSeconds, 0, 1);
            var categoryLabel = GetHeatmapCategoryLabel(dominant);
            var tooltip = $"{start:HH:mm} - {end:HH:mm}\n{topApp}\n{categoryLabel}\nSeen {FormatDuration(TimeSpan.FromSeconds(activeSeconds))}\n{overlapping.Count} captures";

            yield return new HeatmapSegmentViewModel(
                GetHeatmapFill(dominant, intensity),
                tooltip,
                start,
                end,
                categoryLabel,
                intensity);
        }
    }

    private static IEnumerable<TopApplicationViewModel> BuildTopApplications(IReadOnlyList<TimelineActivity> activities, TimeSpan totalActive)
    {
        return activities
            .GroupBy(activity => activity.AppName)
            .Select(group =>
            {
                var duration = group.Aggregate(TimeSpan.Zero, (total, activity) => total + activity.Duration);
                var representative = group.OrderByDescending(activity => activity.EndedAt).First();
                var percent = totalActive.TotalSeconds <= 0 ? 0 : duration.TotalSeconds / totalActive.TotalSeconds;
                return new TopApplicationViewModel(representative, duration, group.Count(), percent);
            })
            .OrderByDescending(app => app.ActiveDuration)
            .Take(8);
    }

    private static IReadOnlyList<FocusBlockViewModel> BuildFocusBlocks(IReadOnlyList<ActivityMomentViewModel> sessions)
    {
        var blocks = new List<FocusBlockViewModel>();
        ActivityMomentViewModel? previous = null;

        foreach (var session in sessions)
        {
            if (previous is not null)
            {
                var gap = session.SessionStartedAt - previous.SessionEndedAt;
                if (gap.TotalMinutes >= 5)
                {
                    blocks.Add(FocusBlockViewModel.Gap(gap));
                }
            }

            blocks.Add(FocusBlockViewModel.Active(session));
            previous = session;
        }

        return blocks.Take(26).ToList();
    }

    private static IEnumerable<KeyMomentViewModel> BuildKeyMoments(IReadOnlyList<ActivityMomentViewModel> sessions)
    {
        return sessions
            .OrderByDescending(session => session.SessionEndedAt - session.SessionStartedAt)
            .ThenByDescending(session => session.OccurrenceCount)
            .ThenByDescending(session => session.HasSavedIndicator)
            .Take(6)
            .Select(session =>
            {
                var previewPath = !string.IsNullOrWhiteSpace(session.ThumbnailPath)
                    ? session.ThumbnailPath
                    : session.ScreenshotPath;

                return new KeyMomentViewModel(
                    session.Id,
                    session.Title,
                    session.AppName,
                    session.CardTime,
                    session.Duration,
                    session.CategoryLabel,
                    GetAccentFill(session.Activity.Kind),
                    "#FFFFFF",
                    string.IsNullOrWhiteSpace(session.Resource) ? session.TechnicalSource : session.Resource,
                    session.ScreenshotPath,
                    previewPath,
                    session.IsPinned);
            });
    }

    private static IEnumerable<CategoryBreakdownItemViewModel> BuildCategoryBreakdown(
        IReadOnlyList<TimelineActivity> activities,
        TimeSpan totalActive)
    {
        foreach (var category in BreakdownOrder)
        {
            var categoryActivities = activities
                .Where(activity => ClassifyForBreakdown(activity) == category)
                .ToList();
            var duration = categoryActivities.Aggregate(TimeSpan.Zero, (total, activity) => total + activity.Duration);
            var percent = totalActive.TotalSeconds <= 0 ? 0 : duration.TotalSeconds / totalActive.TotalSeconds;

            yield return new CategoryBreakdownItemViewModel(
                GetBreakdownLabel(category),
                GetBreakdownFill(category),
                duration,
                percent,
                categoryActivities.Count);
        }
    }

    private static double GetOverlapSeconds(DateTime activityStart, DateTime activityEnd, DateTime blockStart, DateTime blockEnd)
    {
        var start = activityStart > blockStart ? activityStart : blockStart;
        var end = activityEnd < blockEnd ? activityEnd : blockEnd;
        return end > start ? (end - start).TotalSeconds : 0;
    }

    private static string GetHeatmapCategoryLabel(ActivityKind? kind)
    {
        return kind switch
        {
            ActivityKind.Browser => "Web browsing",
            ActivityKind.Development => "Development work",
            ActivityKind.Document => "Documents",
            ActivityKind.System => "Idle or system surfaces",
            _ => "Apps and tools"
        };
    }

    private static ActivityCategory ClassifyForBreakdown(TimelineActivity activity)
    {
        var category = RuleBasedActivityClassifier.Default.Classify(activity).Category;
        return category switch
        {
            ActivityCategory.Browsing or ActivityCategory.Research => ActivityCategory.Browsing,
            ActivityCategory.Communication => ActivityCategory.Other,
            ActivityCategory.ScreenshotCapture => ActivityCategory.Other,
            _ => category
        };
    }

    private static string GetBreakdownLabel(ActivityCategory category)
    {
        return category switch
        {
            ActivityCategory.Browsing => "Web browsing",
            ActivityCategory.Development => "Development work",
            ActivityCategory.Documents => "Documents",
            ActivityCategory.System => "Idle / gaps",
            _ => "Apps and tools"
        };
    }

    private static string GetBreakdownFill(ActivityCategory category)
    {
        return category switch
        {
            ActivityCategory.Browsing => ActivityPalette.Web,
            ActivityCategory.Development => ActivityPalette.Development,
            ActivityCategory.Documents => ActivityPalette.Documents,
            ActivityCategory.System => ActivityPalette.Gap,
            _ => ActivityPalette.Apps
        };
    }

    private static string GetAccentFill(ActivityKind kind)
    {
        return kind switch
        {
            ActivityKind.Browser => ActivityPalette.Web,
            ActivityKind.Development => ActivityPalette.Development,
            ActivityKind.Document => ActivityPalette.Documents,
            ActivityKind.System => ActivityPalette.SystemActivity,
            _ => ActivityPalette.Apps
        };
    }

    private static string GetHeatmapFill(ActivityKind? kind, double intensity)
    {
        if (intensity <= 0)
        {
            return ActivityPalette.Empty;
        }

        return kind switch
        {
            ActivityKind.Browser => ActivityPalette.Scale(ActivityPalette.Web, intensity),
            ActivityKind.Development => ActivityPalette.Scale(ActivityPalette.Development, intensity),
            ActivityKind.Document => ActivityPalette.Scale(ActivityPalette.Documents, intensity),
            ActivityKind.System => ActivityPalette.Scale(ActivityPalette.SystemActivity, intensity),
            _ => ActivityPalette.Scale(ActivityPalette.Apps, intensity)
        };
    }
}
