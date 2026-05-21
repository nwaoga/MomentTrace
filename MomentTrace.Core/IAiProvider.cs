namespace MomentTrace.Core;

public interface IAiProvider
{
    Task<string> GetDailySummaryAsync(IReadOnlyList<TimelineActivity> activities, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetProductivityInsightsAsync(IReadOnlyList<TimelineActivity> activities, CancellationToken cancellationToken = default);
}
