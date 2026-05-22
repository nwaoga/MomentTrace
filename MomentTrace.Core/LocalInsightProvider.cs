namespace MomentTrace.Core;

public sealed class LocalInsightProvider : IInsightProvider
{
    public static LocalInsightProvider Default { get; } = new();

    public IReadOnlyList<string> GetLocalInsights(IReadOnlyList<TimelineActivity> activities)
    {
        if (activities.Count == 0)
        {
            return [];
        }

        var total = activities.Aggregate(TimeSpan.Zero, (duration, activity) => duration + activity.Duration);
        var apps = activities
            .GroupBy(activity => activity.AppName)
            .OrderByDescending(group => group.Sum(activity => activity.Duration.TotalSeconds))
            .Take(3)
            .Select(group => group.Key)
            .ToArray();

        return
        [
            $"Stored locally. {activities.Count} moments covering {FormatDuration(total)}.",
            apps.Length == 0 ? "No dominant apps yet." : $"Most visible sources: {string.Join(", ", apps)}."
        ];
    }

    private static string FormatDuration(TimeSpan duration)
    {
        return duration.TotalHours >= 1
            ? $"{(int)duration.TotalHours}h {duration.Minutes}m"
            : $"{Math.Max(1, (int)Math.Round(duration.TotalMinutes))}m";
    }
}
