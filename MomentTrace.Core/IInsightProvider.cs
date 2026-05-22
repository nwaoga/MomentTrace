namespace MomentTrace.Core;

public interface IInsightProvider
{
    IReadOnlyList<string> GetLocalInsights(IReadOnlyList<TimelineActivity> activities);
}
