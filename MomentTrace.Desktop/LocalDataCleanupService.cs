using System.Linq;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MomentTrace.Core;

namespace MomentTrace.Desktop;

public sealed class LocalDataCleanupService(
    string appDataDirectory,
    string legacyAppDataDirectory,
    string databasePath,
    string settingsPath,
    string privacyBlocklistDirectory,
    string iconsDirectory,
    string screenshotsDirectory,
    string thumbnailsDirectory)
{
    public static LocalDataCleanupService CreateDefault()
    {
        return new LocalDataCleanupService(
            MomentTracePaths.AppDataDirectory,
            MomentTracePaths.LegacyAppDataDirectory,
            MomentTracePaths.DatabasePath,
            DesktopWindowPreferences.PreferencesPath,
            MomentTracePaths.PrivacyBlocklistsDirectory,
            MomentTracePaths.IconsDirectory,
            MomentTracePaths.ScreenshotsDirectory,
            MomentTracePaths.ThumbnailsDirectory);
    }

    public async Task DeleteAllLocalDataAsync(IActivityStore store, CancellationToken cancellationToken = default)
    {
        await store.DeleteAllAsync(cancellationToken);

        DeleteDirectoryContents(screenshotsDirectory, recreate: false);
        DeleteDirectoryContents(thumbnailsDirectory, recreate: false);
        DeleteDirectoryContents(iconsDirectory, recreate: false);
        DeleteDirectoryContents(privacyBlocklistDirectory, recreate: false);
        DeleteFileIfExists(databasePath);
        DeleteFileIfExists(settingsPath);

        TryDeleteDirectoryIfEmpty(appDataDirectory);
        TryDeleteLegacyDirectory();
    }

    private void TryDeleteLegacyDirectory()
    {
        if (!Directory.Exists(legacyAppDataDirectory))
        {
            return;
        }

        try
        {
            Directory.Delete(legacyAppDataDirectory, recursive: true);
        }
        catch
        {
            // Best effort only. Legacy cleanup should not fail the primary delete flow.
        }
    }

    private static void DeleteDirectoryContents(string targetDirectory, bool recreate)
    {
        var fullTarget = Path.GetFullPath(targetDirectory);
        if (!Directory.Exists(fullTarget))
        {
            return;
        }

        Directory.Delete(fullTarget, recursive: true);
        if (recreate)
        {
            Directory.CreateDirectory(fullTarget);
        }
    }

    private static void DeleteFileIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private void TryDeleteDirectoryIfEmpty(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        try
        {
            if (!Directory.EnumerateFileSystemEntries(directory).Any())
            {
                Directory.Delete(directory);
            }
        }
        catch
        {
            // Best effort only. The main cleanup targets are already deleted.
        }
    }
}
