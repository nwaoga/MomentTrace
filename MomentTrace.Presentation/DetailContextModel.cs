using MomentTrace.Core;

namespace MomentTrace.Presentation;

public sealed class DetailContextModel
{
    public required string Narrative { get; init; }

    public required IReadOnlyList<DetailActivityRowViewModel> RawEntries { get; init; }

    public required string RawEntriesSummary { get; init; }

    public required IReadOnlyList<RelatedMomentViewModel> RelatedMoments { get; init; }

    public required string RelatedSummary { get; init; }
}

public sealed class DetailActivityRowViewModel
{
    public DetailActivityRowViewModel(TimelineActivity activity)
    {
        var item = new ActivityMomentViewModel(activity);
        TimeRange = activity.StartedAt.LocalDateTime.ToString("t");
        Title = item.Title;
        Source = string.IsNullOrWhiteSpace(activity.Resource)
            ? item.TechnicalSource
            : activity.Resource;
        Duration = $"Seen {item.Duration}";
    }

    public string TimeRange { get; }

    public string Title { get; }

    public string Source { get; }

    public string Duration { get; }
}

public sealed class RelatedMomentViewModel
{
    public RelatedMomentViewModel(ActivityMomentViewModel activity, string reason)
    {
        Activity = activity;
        Title = activity.Title;
        Detail = $"{reason} · {activity.CardTime} · seen {activity.Duration}";
    }

    public ActivityMomentViewModel Activity { get; }

    public string Title { get; }

    public string Detail { get; }
}
