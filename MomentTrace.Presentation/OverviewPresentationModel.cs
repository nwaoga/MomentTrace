using MomentTrace.Core;

namespace MomentTrace.Presentation;

public sealed class OverviewPresentationModel
{
    public required TimeSpan TotalActive { get; init; }

    public required IReadOnlyList<ActivityMomentViewModel> Sessions { get; init; }

    public required IReadOnlyList<OverviewMetricViewModel> SummaryMetrics { get; init; }

    public required IReadOnlyList<HeatmapSegmentViewModel> HeatmapSegments { get; init; }

    public required IReadOnlyList<TopApplicationViewModel> TopApplications { get; init; }

    public required IReadOnlyList<FocusBlockViewModel> FocusBlocks { get; init; }

    public required IReadOnlyList<LegendItemViewModel> Legend { get; init; }

    public required string LongestFocusDuration { get; init; }
}

public sealed record OverviewMetricViewModel(string Label, string Value, string Detail);

public sealed record HeatmapSegmentViewModel(string Fill, string Tooltip);

public sealed record LegendItemViewModel(string Label, string Fill);

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
    private FocusBlockViewModel(string label, TimeSpan duration, string fill, bool isGap)
    {
        Label = label;
        Duration = duration;
        Fill = fill;
        IsGap = isGap;
    }

    public string Label { get; }

    public TimeSpan Duration { get; }

    public string Fill { get; }

    public bool IsGap { get; }

    public double Width => Math.Clamp(Duration.TotalMinutes * 1.8, 18, 96);

    public string Tooltip => $"{Label}\nSeen {OverviewPresentationBuilder.FormatDuration(Duration)}";

    public static FocusBlockViewModel Active(string appName, TimeSpan duration, ActivityKind kind)
    {
        var fill = kind switch
        {
            ActivityKind.Browser => ActivityPalette.Web,
            ActivityKind.Development => ActivityPalette.Development,
            ActivityKind.Document => ActivityPalette.Documents,
            ActivityKind.System => ActivityPalette.SystemActivity,
            _ => ActivityPalette.Apps
        };

        return new FocusBlockViewModel(appName, duration, fill, isGap: false);
    }

    public static FocusBlockViewModel Gap(TimeSpan duration)
    {
        return new FocusBlockViewModel("Gap or idle space", duration, ActivityPalette.Gap, isGap: true);
    }
}

