namespace MomentTrace.Core;

public sealed class RuleBasedSessionSummarizer(IActivityClassifier classifier) : ISessionSummarizer
{
    private static readonly HashSet<string> BrowserSuffixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Microsoft Edge",
        "Google Chrome",
        "Chrome",
        "Firefox",
        "Safari",
        "Brave",
        "Opera",
        "Vivaldi"
    };

    private static readonly HashSet<string> BrowserProfiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Personal",
        "Profile 1",
        "Profile 2",
        "Default",
        "Work"
    };

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
        var hosts = GetHosts(activities)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(2)
            .ToArray();

        return category switch
        {
            ActivityCategory.Browsing when IsSpecificRepresentativeTitle(representativeTitle, dominantApp, "Website browsing") => representativeTitle,
            ActivityCategory.Browsing when !string.IsNullOrWhiteSpace(dominantHost) => $"Browsing {dominantHost}",
            ActivityCategory.Browsing when !string.IsNullOrWhiteSpace(dominantApp) => $"Browsing in {dominantApp}",
            ActivityCategory.Browsing => "Website browsing",

            ActivityCategory.Development when IsSpecificRepresentativeTitle(representativeTitle, dominantApp, "Development work") => representativeTitle,
            ActivityCategory.Development when !string.IsNullOrWhiteSpace(dominantApp) => $"{dominantApp} work",
            ActivityCategory.Development => "Development work",

            ActivityCategory.Communication when IsSpecificRepresentativeTitle(representativeTitle, dominantApp, "Communication") => representativeTitle,
            ActivityCategory.Communication when !string.IsNullOrWhiteSpace(dominantApp) => $"{dominantApp} conversations",
            ActivityCategory.Communication => "Communication",

            ActivityCategory.Documents when IsSpecificRepresentativeTitle(representativeTitle, dominantApp, "Document review") => representativeTitle,
            ActivityCategory.Documents when !string.IsNullOrWhiteSpace(dominantApp) => $"{dominantApp} documents",
            ActivityCategory.Documents => "Document review",

            ActivityCategory.Research when IsSpecificRepresentativeTitle(representativeTitle, dominantApp, "Research session") => representativeTitle,
            ActivityCategory.Research when hosts.Length > 1 => $"Research across {string.Join(" and ", hosts)}",
            ActivityCategory.Research when !string.IsNullOrWhiteSpace(dominantHost) => $"Research on {dominantHost}",
            ActivityCategory.Research when !string.IsNullOrWhiteSpace(dominantApp) => $"Research in {dominantApp}",
            ActivityCategory.Research => "Research session",

            ActivityCategory.ScreenshotCapture when IsSpecificRepresentativeTitle(representativeTitle, dominantApp, "Screenshot capture") => representativeTitle,
            ActivityCategory.ScreenshotCapture when !string.IsNullOrWhiteSpace(dominantApp) => $"{dominantApp} capture",
            ActivityCategory.ScreenshotCapture => "Screenshot capture",

            ActivityCategory.System when IsSpecificRepresentativeTitle(representativeTitle, dominantApp, "System activity") => representativeTitle,
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
            .Select(GetSpecificActivityTitle)
            .Where(title => !string.IsNullOrWhiteSpace(title))
            .GroupBy(title => title, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault()?.Key;

        if (!string.IsNullOrWhiteSpace(representative))
        {
            return representative;
        }

        representative = activities
            .Select(activity => classifier.Classify(activity))
            .GroupBy(classification => classification.Title, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
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

    private static string? GetSpecificActivityTitle(TimelineActivity activity)
    {
        var title = CleanTitle(activity.DisplayTitle);
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        if (string.Equals(title, activity.AppName, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return title;
    }

    private static string CleanTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return string.Empty;
        }

        var segments = title
            .Split(" - ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        while (segments.Count > 0 && LooksLikeTrailingShellSegment(segments[^1]))
        {
            segments.RemoveAt(segments.Count - 1);
        }

        return segments.Count == 0
            ? title.Trim()
            : string.Join(" - ", segments).Trim();
    }

    private static bool LooksLikeTrailingShellSegment(string segment)
    {
        if (BrowserSuffixes.Contains(segment) || BrowserProfiles.Contains(segment))
        {
            return true;
        }

        return segment.Contains("Edge", StringComparison.OrdinalIgnoreCase)
            || segment.Contains("Chrome", StringComparison.OrdinalIgnoreCase)
            || segment.Contains("Firefox", StringComparison.OrdinalIgnoreCase)
            || segment.Contains("Safari", StringComparison.OrdinalIgnoreCase)
            || segment.Contains("Visual Studio Code", StringComparison.OrdinalIgnoreCase);
    }
}
