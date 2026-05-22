namespace MomentTrace.Core;

public sealed class RuleBasedSessionSummarizer(IActivityClassifier classifier) : ISessionSummarizer
{
    public static RuleBasedSessionSummarizer Default { get; } = new(RuleBasedActivityClassifier.Default);

    public string GetSessionTitle(ActivityCategory category, IReadOnlyList<TimelineActivity> activities)
    {
        if (activities.Count == 0)
        {
            return "Moment";
        }

        return category switch
        {
            ActivityCategory.Browsing => "Website browsing",
            ActivityCategory.Development => "Development work",
            ActivityCategory.Communication => "Communication",
            ActivityCategory.Documents => "Document review",
            ActivityCategory.Research => "Research session",
            ActivityCategory.ScreenshotCapture => "Screenshot capture",
            ActivityCategory.System => "System activity",
            _ => GetRepresentativeTitle(activities)
        };
    }

    public string GetSessionDescription(ActivityCategory category, IReadOnlyList<TimelineActivity> activities)
    {
        var apps = activities
            .Select(activity => activity.AppName)
            .Where(app => !string.IsNullOrWhiteSpace(app))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToArray();
        var appText = apps.Length == 0 ? "this workspace" : string.Join(", ", apps);

        return category switch
        {
            ActivityCategory.Browsing => $"Browsed pages and websites in {appText}.",
            ActivityCategory.Development => $"Worked through code, tooling, or project context in {appText}.",
            ActivityCategory.Communication => $"Moved through conversations, messages, or meetings in {appText}.",
            ActivityCategory.Documents => $"Reviewed or edited documents and reference material in {appText}.",
            ActivityCategory.Research => $"Researched and compared information across {appText}.",
            ActivityCategory.ScreenshotCapture => $"Captured visual context using {appText}.",
            ActivityCategory.System => $"Used Windows or system surfaces in {appText}.",
            _ => $"Revisited activity from {appText}."
        };
    }

    private string GetRepresentativeTitle(IReadOnlyList<TimelineActivity> activities)
    {
        var representative = activities
            .Select(activity => classifier.Classify(activity))
            .GroupBy(classification => classification.Title)
            .OrderByDescending(group => group.Count())
            .FirstOrDefault()?.Key;

        return string.IsNullOrWhiteSpace(representative) ? "App activity" : representative;
    }
}
