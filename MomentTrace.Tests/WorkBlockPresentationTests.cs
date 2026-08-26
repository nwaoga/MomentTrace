using MomentTrace.Core;
using MomentTrace.Presentation;

namespace MomentTrace.Tests;

public sealed class WorkBlockPresentationTests
{
    private readonly TimelinePresentationBuilder _builder = new(
        RuleBasedActivityClassifier.Default,
        RuleBasedSessionSummarizer.Default);

    [Fact]
    public void BuildWorkBlocks_groups_nearby_related_moments_into_recoverable_block()
    {
        var moments = new[]
        {
            Moment(1, "Visual Studio Code", "code", ActivityKind.Development, "MomentTrace", "2026-08-26T09:00:00+01:00", 10),
            Moment(2, "Google Chrome", "chrome", ActivityKind.Browser, "GitHub docs - Google Chrome", "2026-08-26T09:12:00+01:00", 6, "https://github.com/openai/codex"),
            Moment(3, "Word", "winword", ActivityKind.Document, "Launch notes.docx", "2026-08-26T09:22:00+01:00", 8)
        };

        var blocks = _builder.BuildWorkBlocks(moments);

        var block = Assert.Single(blocks);
        Assert.Equal(3, block.MomentCount);
        Assert.Equal("09:00 - 09:30", block.TimeRange);
        Assert.Equal("Recovered 3 moments across Visual Studio Code, Google Chrome, Word.", block.Summary);
    }

    [Fact]
    public void BuildWorkBlocks_splits_when_gap_is_too_large()
    {
        var moments = new[]
        {
            Moment(1, "Visual Studio Code", "code", ActivityKind.Development, "MomentTrace", "2026-08-26T09:00:00+01:00", 10),
            Moment(2, "Slack", "slack", ActivityKind.App, "Team messages", "2026-08-26T09:45:00+01:00", 10)
        };

        var blocks = _builder.BuildWorkBlocks(moments);

        Assert.Equal(2, blocks.Count);
    }

    [Fact]
    public void BuildWorkBlocks_splits_at_local_day_boundary()
    {
        var moments = new[]
        {
            Moment(1, "Visual Studio Code", "code", ActivityKind.Development, "MomentTrace", "2026-08-26T23:50:00+01:00", 8),
            Moment(2, "Visual Studio Code", "code", ActivityKind.Development, "MomentTrace", "2026-08-27T00:03:00+01:00", 8)
        };

        var blocks = _builder.BuildWorkBlocks(moments);

        Assert.Equal(2, blocks.Count);
    }

    [Fact]
    public void BuildWorkBlocks_groups_same_browser_host_within_gap()
    {
        var moments = new[]
        {
            Moment(1, "Google Chrome", "chrome", ActivityKind.Browser, "Pricing - Google Chrome", "2026-08-26T10:00:00+01:00", 4, "https://example.com/pricing"),
            Moment(2, "Google Chrome", "chrome", ActivityKind.Browser, "Docs - Google Chrome", "2026-08-26T10:21:00+01:00", 5, "https://example.com/docs")
        };

        var blocks = _builder.BuildWorkBlocks(moments);

        var block = Assert.Single(blocks);
        Assert.Equal("Work around example.com", block.Title);
        Assert.Equal(2, block.MomentCount);
    }

    private static ActivityMomentViewModel Moment(
        long id,
        string app,
        string process,
        ActivityKind kind,
        string title,
        string startedAt,
        int minutes,
        string? resource = null)
    {
        var start = DateTimeOffset.Parse(startedAt);
        var activity = new TimelineActivity
        {
            Id = id,
            AppName = app,
            ProcessName = process,
            WindowTitle = title,
            Kind = kind,
            Resource = resource,
            StartedAt = start,
            EndedAt = start.AddMinutes(minutes)
        };

        return new ActivityMomentViewModel(activity);
    }
}
