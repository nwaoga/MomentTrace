namespace MomentTrace.Core;

public interface IActivityStore
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<long> AddAsync(TimelineActivity activity, CancellationToken cancellationToken = default);

    Task UpdateEndAsync(long id, DateTimeOffset endedAt, CancellationToken cancellationToken = default);

    Task SetPinnedAsync(long id, bool isPinned, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TimelineActivity>> QueryAsync(TimelineQuery query, CancellationToken cancellationToken = default);
}
