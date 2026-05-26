namespace MomentTrace.Core;

public sealed class TimelineActivity
{
    public long Id { get; init; }

    public required string AppName { get; init; }

    public required string WindowTitle { get; init; }

    public required string ProcessName { get; init; }

    public string? ExecutablePath { get; init; }

    public string? Resource { get; init; }

    public string? ScreenshotPath { get; init; }

    public ActivityKind Kind { get; init; }

    public DateTimeOffset StartedAt { get; init; }

    public DateTimeOffset EndedAt { get; init; }

    public bool IsPinned { get; init; }

    public string? Note { get; init; }

    public bool IsBookmarked { get; init; }

    public string DisplayTitle => string.IsNullOrWhiteSpace(WindowTitle) ? AppName : WindowTitle;

    public TimeSpan Duration => EndedAt > StartedAt ? EndedAt - StartedAt : TimeSpan.Zero;
}
