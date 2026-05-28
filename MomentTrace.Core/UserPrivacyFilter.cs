namespace MomentTrace.Core;

public sealed class UserPrivacyFilter(PrivacyFilterOptions options)
{
    private readonly DomainPrivacyFilter _domainFilter = new(options);
    private readonly HashSet<string> _blockedApps = NormalizeApps(options.CustomBlockedApps);

    public bool ShouldExclude(TimelineActivity activity)
    {
        return _domainFilter.ShouldExclude(activity)
            || ShouldExclude(activity.ProcessName, activity.AppName, activity.ExecutablePath);
    }

    public bool ShouldExclude(string? resource)
    {
        return _domainFilter.ShouldExclude(resource);
    }

    public bool ShouldExclude(string? resource, string processName, string appName, string? executablePath)
    {
        return ShouldExclude(resource) || ShouldExclude(processName, appName, executablePath);
    }

    public bool ShouldExclude(string processName, string appName, string? executablePath)
    {
        if (_blockedApps.Count == 0)
        {
            return false;
        }

        return MatchesApp(processName)
            || MatchesApp(appName)
            || MatchesApp(string.IsNullOrWhiteSpace(executablePath) ? null : Path.GetFileNameWithoutExtension(executablePath))
            || MatchesApp(string.IsNullOrWhiteSpace(executablePath) ? null : Path.GetFileName(executablePath));
    }

    public static string NormalizeApp(string app)
    {
        return app
            .Trim()
            .Trim('"')
            .Trim()
            .ToLowerInvariant();
    }

    private bool MatchesApp(string? app)
    {
        if (string.IsNullOrWhiteSpace(app))
        {
            return false;
        }

        var normalized = NormalizeApp(app);
        return _blockedApps.Contains(normalized)
            || _blockedApps.Contains(Path.GetFileNameWithoutExtension(normalized))
            || _blockedApps.Contains(Path.GetFileName(normalized));
    }

    private static HashSet<string> NormalizeApps(IEnumerable<string> apps)
    {
        return apps
            .Select(NormalizeApp)
            .Where(app => !string.IsNullOrWhiteSpace(app))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
