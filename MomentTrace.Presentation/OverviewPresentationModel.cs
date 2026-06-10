using MomentTrace.Core;

namespace MomentTrace.Presentation;

public sealed class OverviewPresentationModel
{
    public required string DateLabel { get; init; }

    public required TimeSpan TotalActive { get; init; }

    public required IReadOnlyList<ActivityMomentViewModel> Sessions { get; init; }

    public required IReadOnlyList<OverviewMetricViewModel> SummaryMetrics { get; init; }

    public required IReadOnlyList<DaySummaryMetricViewModel> DaySummaryMetrics { get; init; }

    public required IReadOnlyList<HeatmapSegmentViewModel> HeatmapSegments { get; init; }

    public required IReadOnlyList<KeyMomentViewModel> KeyMoments { get; init; }

    public required IReadOnlyList<CategoryBreakdownItemViewModel> CategoryBreakdown { get; init; }

    public required IReadOnlyList<TopApplicationViewModel> TopApplications { get; init; }

    public required IReadOnlyList<FocusBlockViewModel> FocusBlocks { get; init; }

    public required IReadOnlyList<LegendItemViewModel> Legend { get; init; }

    public required string LongestFocusDuration { get; init; }
}

public sealed record OverviewMetricViewModel(string Label, string Value, string Detail);

public sealed record DaySummaryMetricViewModel(
    string Label,
    string Value,
    string Detail,
    string AccentFill,
    string AccentGlyph);

public sealed record HeatmapSegmentViewModel(
    string Fill,
    string Tooltip,
    DateTime BlockStart,
    DateTime BlockEnd,
    string CategoryLabel,
    double Intensity);

public sealed record LegendItemViewModel(string Label, string Fill);

public sealed record KeyMomentViewModel(
    long Id,
    string Title,
    string Subtitle,
    string Timestamp,
    string Duration,
    string Category,
    string AccentFill,
    string AccentText,
    string? Source,
    string? ScreenshotPath,
    string? ThumbnailPath,
    bool IsPinned);

public sealed record CategoryBreakdownItemViewModel(
    string Label,
    string Fill,
    TimeSpan Duration,
    double PercentOfActive,
    int SessionCount)
{
    public string DurationLabel => OverviewPresentationBuilder.FormatDuration(Duration);

    public string PercentLabel => $"{PercentOfActive:P0}";

    public double BarWidth => SessionCount == 0
        ? 0
        : Math.Max(PercentOfActive * 220d, 12d);
}

public sealed class TopApplicationViewModel
{
    public TopApplicationViewModel(TimelineActivity representative, TimeSpan activeDuration, int sessionCount, double percent)
    {
        Representative = representative;
        ActiveDuration = activeDuration;
        SessionCount = sessionCount;
        Percent = percent;
    }

    public TimelineActivity Representative { get; }

    public string AppName => Representative.AppName;

    public TimeSpan ActiveDuration { get; }

    public int SessionCount { get; }

    public double Percent { get; }

    public string Duration => OverviewPresentationBuilder.FormatDuration(ActiveDuration);

    public string Sessions => SessionCount.ToString();

    public string PercentOfDay => $"{Percent:P0}";

    public string Trend => "stable";
}

public sealed class FocusBlockViewModel
{
    private FocusBlockViewModel(string label, TimeSpan duration, string fill, bool isGap, DateTimeOffset? startedAt, long? momentId)
    {
        Label = label;
        Duration = duration;
        Fill = fill;
        IsGap = isGap;
        StartedAt = startedAt;
        MomentId = momentId;
    }

    public string Label { get; }

    public TimeSpan Duration { get; }

    public string Fill { get; }

    public bool IsGap { get; }

    public DateTimeOffset? StartedAt { get; }

    public long? MomentId { get; }

    public double Width => Math.Clamp(Duration.TotalMinutes * 1.35, 20, 84);

    public string StartedAtLabel => StartedAt?.LocalDateTime.ToString("h:mm tt") ?? string.Empty;

    public string DurationLabel => OverviewPresentationBuilder.FormatDuration(Duration);

    public string Tooltip => $"{Label}\nSeen {OverviewPresentationBuilder.FormatDuration(Duration)}";

    public static FocusBlockViewModel Active(ActivityMomentViewModel moment)
    {
        var fill = moment.Activity.Kind switch
        {
            ActivityKind.Browser => ActivityPalette.Web,
            ActivityKind.Development => ActivityPalette.Development,
            ActivityKind.Document => ActivityPalette.Documents,
            ActivityKind.System => ActivityPalette.SystemActivity,
            _ => ActivityPalette.Apps
        };

        var duration = moment.SessionEndedAt > moment.SessionStartedAt
            ? moment.SessionEndedAt - moment.SessionStartedAt
            : moment.Activity.Duration;

        return new FocusBlockViewModel(
            moment.Title,
            duration,
            fill,
            isGap: false,
            moment.SessionStartedAt,
            moment.Id);
    }

    public static FocusBlockViewModel Gap(TimeSpan duration)
    {
        return new FocusBlockViewModel("Gap or idle space", duration, ActivityPalette.Gap, isGap: true, startedAt: null, momentId: null);
    }
}
