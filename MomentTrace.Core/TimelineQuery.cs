namespace MomentTrace.Core;

public sealed class TimelineQuery
{
    public string SearchText { get; init; } = string.Empty;

    public ActivityKind? Kind { get; init; }

    public bool? IsPinned { get; init; }

    public bool? IsBookmarked { get; init; }

    public int Limit { get; init; } = 250;
}
