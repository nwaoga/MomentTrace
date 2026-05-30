using MomentTrace.Core;
using MomentTrace.Presentation;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MomentTrace.App;

public sealed class ActivityItemViewModel : ActivityMomentViewModel
{
    private static readonly WpfAppIconProvider AppIconProvider = new();

    public ActivityItemViewModel(TimelineActivity activity)
        : base(activity)
    {
    }

    public ActivityItemViewModel(
        TimelineActivity activity,
        DateTimeOffset sessionStartedAt,
        DateTimeOffset sessionEndedAt,
        int occurrenceCount)
        : base(activity, sessionStartedAt, sessionEndedAt, occurrenceCount)
    {
    }

    public ImageSource? ScreenshotPreview => ImageCache.GetImage(Activity.ThumbnailPath) ?? ImageCache.GetImage(Activity.ScreenshotPath);

    public ImageSource? ProgramIcon => AppIconProvider.GetIcon(Activity.ExecutablePath, Activity.AppName, Activity.ProcessName);
}

file static class ImageCache
{
    private static readonly Dictionary<string, ImageSource?> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static ImageSource? GetImage(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        if (Cache.TryGetValue(path, out var cached))
        {
            return cached;
        }

        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 420;
            image.UriSource = new Uri(path, UriKind.Absolute);
            image.EndInit();
            image.Freeze();

            Cache[path] = image;
            return image;
        }
        catch
        {
            Cache[path] = null;
            return null;
        }
    }
}
