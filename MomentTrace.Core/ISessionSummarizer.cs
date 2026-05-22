namespace MomentTrace.Core;

public interface ISessionSummarizer
{
    string GetSessionTitle(ActivityCategory category, IReadOnlyList<TimelineActivity> activities);

    string GetSessionDescription(ActivityCategory category, IReadOnlyList<TimelineActivity> activities);
}
