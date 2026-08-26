using MomentTrace.Core;

namespace MomentTrace.Presentation;

public sealed class WorkBlockViewModel
{
    public WorkBlockViewModel(
        DateTimeOffset startedAt,
        DateTimeOffset endedAt,
        ActivityCategory category,
        IReadOnlyList<ActivityMomentViewModel> moments)
    {
        StartedAt = startedAt;
        EndedAt = endedAt;
        Category = category;
        Moments = moments;
        Title = BuildTitle(moments, category);
        Summary = BuildSummary(moments);
        AccentBrush = GetAccentBrush(category);
    }

    public DateTimeOffset StartedAt { get; }

    public DateTimeOffset EndedAt { get; }

    public ActivityCategory Category { get; }

    public IReadOnlyList<ActivityMomentViewModel> Moments { get; }

    public long RepresentativeMomentId => Moments.FirstOrDefault()?.Id ?? 0;

    public string Title { get; }

    public string Summary { get; }

    public string TimeRange => $"{StartedAt.LocalDateTime:HH:mm} - {EndedAt.LocalDateTime:HH:mm}";

    public TimeSpan Duration => EndedAt > StartedAt ? EndedAt - StartedAt : TimeSpan.Zero;

    public string DurationLabel => OverviewPresentationBuilder.FormatDuration(Duration);

    public int MomentCount => Moments.Count;

    public string MomentCountLabel => $"{MomentCount} moment{(MomentCount == 1 ? string.Empty : "s")}";

    public int AppCount => Moments
        .Select(moment => moment.AppName)
        .Where(app => !string.IsNullOrWhiteSpace(app))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Count();

    public string AccentBrush { get; }

    private static string BuildTitle(IReadOnlyList<ActivityMomentViewModel> moments, ActivityCategory category)
    {
        var dominantHost = GetDominantHost(moments);
        if (!string.IsNullOrWhiteSpace(dominantHost))
        {
            return $"Work around {dominantHost}";
        }

        var dominantApp = GetDominantApp(moments);
        if (!string.IsNullOrWhiteSpace(dominantApp))
        {
            return $"{dominantApp} work block";
        }

        return category switch
        {
            ActivityCategory.Development => "Development work block",
            ActivityCategory.Research => "Research work block",
            ActivityCategory.Browsing => "Browsing work block",
            ActivityCategory.Documents => "Document work block",
            ActivityCategory.Communication => "Communication work block",
            _ => "Work block"
        };
    }

    private static string BuildSummary(IReadOnlyList<ActivityMomentViewModel> moments)
    {
        var apps = moments
            .Select(moment => moment.AppName)
            .Where(app => !string.IsNullOrWhiteSpace(app))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToArray();
        var appText = apps.Length == 0 ? "local activity" : string.Join(", ", apps);
        var countText = $"{moments.Count} moment{(moments.Count == 1 ? string.Empty : "s")}";

        return $"Recovered {countText} across {appText}.";
    }

    private static string? GetDominantApp(IReadOnlyList<ActivityMomentViewModel> moments)
    {
        return moments
            .Select(moment => moment.AppName.Trim())
            .Where(app => !string.IsNullOrWhiteSpace(app))
            .GroupBy(app => app, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Key)
            .FirstOrDefault();
    }

    private static string? GetDominantHost(IReadOnlyList<ActivityMomentViewModel> moments)
    {
        return moments
            .Select(moment => TryGetHost(moment.Resource, out var host) ? host : null)
            .Where(host => !string.IsNullOrWhiteSpace(host))
            .GroupBy(host => host!, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Key)
            .FirstOrDefault();
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

    private static string GetAccentBrush(ActivityCategory category)
    {
        return category switch
        {
            ActivityCategory.Browsing => "#5CA8FF",
            ActivityCategory.Research => "#7C5CFF",
            ActivityCategory.Development => "#71C562",
            ActivityCategory.Communication => "#67D4B1",
            ActivityCategory.Documents => "#E7B84E",
            ActivityCategory.ScreenshotCapture => "#B89CFF",
            _ => "#8D9BB0"
        };
    }
}
