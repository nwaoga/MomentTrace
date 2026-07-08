using MomentTrace.Core;
using MomentTrace.Desktop;

namespace MomentTrace.Tests;

public sealed class LocalDataCleanupServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "MomentTrace.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task DeleteAllLocalDataAsync_RemovesAppDataArtifactsAndClearsStore()
    {
        var appDataDirectory = Path.Combine(_root, "MomentTrace");
        var legacyDirectory = Path.Combine(_root, "NwaogaTimeline");
        var databasePath = Path.Combine(appDataDirectory, "timeline.db");
        var settingsPath = Path.Combine(appDataDirectory, "settings.json");
        var blocklistDirectory = Path.Combine(appDataDirectory, "privacy-blocklists");
        var iconsDirectory = Path.Combine(appDataDirectory, "icons");
        var screenshotsDirectory = Path.Combine(appDataDirectory, "screenshots");
        var thumbnailsDirectory = Path.Combine(appDataDirectory, "thumbnails");

        Directory.CreateDirectory(appDataDirectory);
        Directory.CreateDirectory(blocklistDirectory);
        Directory.CreateDirectory(iconsDirectory);
        Directory.CreateDirectory(screenshotsDirectory);
        Directory.CreateDirectory(thumbnailsDirectory);
        Directory.CreateDirectory(legacyDirectory);

        await File.WriteAllTextAsync(databasePath, "db");
        await File.WriteAllTextAsync(settingsPath, "{}");
        await File.WriteAllTextAsync(Path.Combine(blocklistDirectory, "adult-domains.txt"), "example.com");
        await File.WriteAllTextAsync(Path.Combine(iconsDirectory, "icon.bin"), "icon");
        await File.WriteAllTextAsync(Path.Combine(screenshotsDirectory, "shot.png"), "shot");
        await File.WriteAllTextAsync(Path.Combine(thumbnailsDirectory, "thumb.png"), "thumb");
        await File.WriteAllTextAsync(Path.Combine(legacyDirectory, "settings.json"), "{}");

        var store = new FakeActivityStore();
        var cleanup = new LocalDataCleanupService(
            appDataDirectory,
            legacyDirectory,
            databasePath,
            settingsPath,
            blocklistDirectory,
            iconsDirectory,
            screenshotsDirectory,
            thumbnailsDirectory);

        await cleanup.DeleteAllLocalDataAsync(store);

        Assert.True(store.DeleteAllCalled);
        Assert.False(File.Exists(databasePath));
        Assert.False(File.Exists(settingsPath));
        Assert.False(Directory.Exists(blocklistDirectory));
        Assert.False(Directory.Exists(iconsDirectory));
        Assert.False(Directory.Exists(screenshotsDirectory));
        Assert.False(Directory.Exists(thumbnailsDirectory));
        Assert.False(Directory.Exists(legacyDirectory));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch
        {
        }
    }

    private sealed class FakeActivityStore : IActivityStore
    {
        public bool DeleteAllCalled { get; private set; }

        public Task<long> AddAsync(TimelineActivity activity, CancellationToken cancellationToken = default) => Task.FromResult(0L);
        public Task DeleteAsync(long id, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAllAsync(CancellationToken cancellationToken = default)
        {
            DeleteAllCalled = true;
            return Task.CompletedTask;
        }
        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<TimelineActivity>> QueryAsync(TimelineQuery query, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<TimelineActivity>>([]);
        public Task SetBookmarkedAsync(long id, bool isBookmarked, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SetNoteAsync(long id, string? note, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SetPinnedAsync(long id, bool isPinned, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateEndAsync(long id, DateTimeOffset endedAt, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
