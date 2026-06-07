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

        var representativeTitle = GetRepresentativeTitle(activities);
        var dominantApp = GetDominantApp(activities);
        var dominantHost = GetDominantHost(activities);

        return category switch
        {
            ActivityCategory.Browsing when !string.IsNullOrWhiteSpace(dominantHost) => $"Browsing {dominantHost}",
            ActivityCategory.Browsing when !string.IsNullOrWhiteSpace(dominantApp) => $"Browsing in {dominantApp}",
            ActivityCategory.Browsing => "Website browsing",

            ActivityCategory.Development when IsSpecificRepresentativeTitle(representativeTitle, dominantApp, "Development work") => representativeTitle,
            ActivityCategory.Development when !string.IsNullOrWhiteSpace(dominantApp) => $"{dominantApp} work",
            ActivityCategory.Development => "Development work",

            ActivityCategory.Communication when !string.IsNullOrWhiteSpace(dominantApp) => $"{dominantApp} conversations",
            ActivityCategory.Communication => "Communication",

            ActivityCategory.Documents when IsSpecificRepresentativeTitle(representativeTitle, dominantApp, "Document review") => representativeTitle,
            ActivityCategory.Documents when !string.IsNullOrWhiteSpace(dominantApp) => $"{dominantApp} documents",
            ActivityCategory.Documents => "Document review",

            ActivityCategory.Research when !string.IsNullOrWhiteSpace(dominantHost) => $"Research on {dominantHost}",
            ActivityCategory.Research when !string.IsNullOrWhiteSpace(dominantApp) => $"Research in {dominantApp}",
            ActivityCategory.Research => "Research session",

            ActivityCategory.ScreenshotCapture when !string.IsNullOrWhiteSpace(dominantApp) => $"{dominantApp} capture",
            ActivityCategory.ScreenshotCapture => "Screenshot capture",

            ActivityCategory.System when !string.IsNullOrWhiteSpace(dominantApp) => $"{dominantApp} activity",
            ActivityCategory.System => "System activity",

            _ => representativeTitle
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
        var hosts = GetHosts(activities)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToArray();
        var hostText = hosts.Length == 0 ? string.Empty : string.Join(", ", hosts);
        var activityText = $"{activities.Count} capture{(activities.Count == 1 ? string.Empty : "s")}";

        return category switch
        {
            ActivityCategory.Browsing when hosts.Length > 0 => $"Browsed {activityText} across {hostText}.",
            ActivityCategory.Browsing => $"Browsed pages and websites in {appText}.",

            ActivityCategory.Development when activities.Count == 1 => $"Worked in {appText} during a focused development moment.",
            ActivityCategory.Development => $"Worked through code, tooling, or project context in {appText}.",

            ActivityCategory.Communication => $"Moved through conversations, messages, or meetings in {appText}.",

            ActivityCategory.Documents when activities.Count == 1 => $"Reviewed or edited a document moment in {appText}.",
            ActivityCategory.Documents => $"Reviewed or edited documents and reference material in {appText}.",

            ActivityCategory.Research when hosts.Length > 0 => $"Researched and compared information across {hostText}.",
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

    private static string? GetDominantApp(IReadOnlyList<TimelineActivity> activities)
    {
        return activities
            .Select(activity => activity.AppName?.Trim())
            .Where(app => !string.IsNullOrWhiteSpace(app))
            .GroupBy(app => app, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Key)
            .FirstOrDefault();
    }

    private static string? GetDominantHost(IReadOnlyList<TimelineActivity> activities)
    {
        return GetHosts(activities)
            .GroupBy(host => host, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Key)
            .FirstOrDefault();
    }

    private static IEnumerable<string> GetHosts(IReadOnlyList<TimelineActivity> activities)
    {
        foreach (var activity in activities)
        {
            if (TryGetHost(activity.Resource, out var host))
            {
                yield return host;
            }
        }
    }

    private static bool TryGetHost(string? resource, out string host)
    {
        host = string.Empty;
        if (string.IsNullOrWhiteSpace(resource))
        {
            return false;
        }

        var candidate = resource.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? resource : $"https://{resource}";
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri) || string.IsNullOrWhiteSpace(uri.Host))
        {
            return false;
        }

        host = uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? uri.Host[4..] : uri.Host;
        return true;
    }

    private static bool IsSpecificRepresentativeTitle(string representativeTitle, string? dominantApp, string fallbackTitle)
    {
        return !string.IsNullOrWhiteSpace(representativeTitle)
            && !string.Equals(representativeTitle, fallbackTitle, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(representativeTitle, dominantApp, StringComparison.OrdinalIgnoreCase);
    }
}
