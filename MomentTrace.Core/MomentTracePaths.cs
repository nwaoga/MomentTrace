namespace MomentTrace.Core;

public static class MomentTracePaths
{
    public static string AppDataDirectory
    {
        get
        {
            if (OperatingSystem.IsMacOS())
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return Path.Combine(home, "Library", "Application Support", "MomentTrace");
            }

            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MomentTrace");
        }
    }

    public static string LegacyAppDataDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NwaogaTimeline");

    public static string DatabasePath => Path.Combine(AppDataDirectory, "timeline.db");

    public static string ScreenshotsDirectory => Path.Combine(AppDataDirectory, "screenshots");

    public static string ThumbnailsDirectory => Path.Combine(AppDataDirectory, "thumbnails");

    public static string IconsDirectory => Path.Combine(AppDataDirectory, "icons");

    public static string PrivacyBlocklistsDirectory => Path.Combine(AppDataDirectory, "privacy-blocklists");
}
