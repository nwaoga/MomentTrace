using MomentTrace.Core;

namespace MomentTrace.Presentation;

public sealed class OverviewPresentationBuilder(TimelinePresentationBuilder timelineBuilder)
{
    private readonly TimelinePresentationBuilder _timelineBuilder = timelineBuilder;

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

        return new OverviewPresentationModel
        {
            TotalActive = totalActive,
            Sessions = sessions,
            TopApplications = topApplications,
            FocusBlocks = focusBlocks,
            HeatmapSegments = BuildHeatmap(dayActivities, selectedDate).ToList(),
            Legend =
            [
                new("Apps and tools", ActivityPalette.Apps),
                new("Web browsing", ActivityPalette.Web),
                new("Development work", ActivityPalette.Development),
                new("Documents", ActivityPalette.Documents),
                new("Gaps or idle space", ActivityPalette.Gap)
            ],
            LongestFocusDuration = longestFocus is null ? "None yet" : FormatDuration(longestFocus.Duration),
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
            var tooltip = $"{start:HH:mm} - {end:HH:mm}\n{topApp}\nSeen {FormatDuration(TimeSpan.FromSeconds(activeSeconds))}\n{overlapping.Count} captures";

            yield return new HeatmapSegmentViewModel(GetHeatmapFill(dominant, intensity), tooltip);
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

            blocks.Add(FocusBlockViewModel.Active(session.AppName, session.SessionEndedAt - session.SessionStartedAt, session.Activity.Kind));
            previous = session;
        }

        return blocks.Take(26).ToList();
    }

    private static double GetOverlapSeconds(DateTime activityStart, DateTime activityEnd, DateTime blockStart, DateTime blockEnd)
    {
        var start = activityStart > blockStart ? activityStart : blockStart;
        var end = activityEnd < blockEnd ? activityEnd : blockEnd;
        return end > start ? (end - start).TotalSeconds : 0;
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
