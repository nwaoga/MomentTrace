namespace MomentTrace.Core;

public interface IActivityStore
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<long> AddAsync(TimelineActivity activity, CancellationToken cancellationToken = default);

    Task UpdateEndAsync(long id, DateTimeOffset endedAt, CancellationToken cancellationToken = default);

    Task SetPinnedAsync(long id, bool isPinned, CancellationToken cancellationToken = default);

    Task SetNoteAsync(long id, string? note, CancellationToken cancellationToken = default);

    Task SetBookmarkedAsync(long id, bool isBookmarked, CancellationToken cancellationToken = default);

    Task DeleteAsync(long id, CancellationToken cancellationToken = default);

    Task DeleteAllAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TimelineActivity>> QueryAsync(TimelineQuery query, CancellationToken cancellationToken = default);
}
