using MomentTrace.Core;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MomentTrace.App;

public sealed class ActivityItemViewModel(TimelineActivity activity)
{
    public TimelineActivity Activity { get; private set; } = activity;

    private readonly DateTimeOffset _sessionStartedAt = activity.StartedAt;
    private readonly DateTimeOffset _sessionEndedAt = activity.EndedAt;
    private readonly int _occurrenceCount = 1;

    public ActivityItemViewModel(
        TimelineActivity activity,
        DateTimeOffset sessionStartedAt,
        DateTimeOffset sessionEndedAt,
        int occurrenceCount)
        : this(activity)
    {
        _sessionStartedAt = sessionStartedAt;
        _sessionEndedAt = sessionEndedAt;
        _occurrenceCount = Math.Max(1, occurrenceCount);
    }

    public long Id => Activity.Id;

    public string Title => Activity.DisplayTitle;

    public string AppName => Activity.AppName;

    public string ProcessName => Activity.ProcessName;

    public string? ExecutablePath => Activity.ExecutablePath;

    public string? Resource => Activity.Resource;

    public string? ScreenshotPath => Activity.ScreenshotPath;

    public ImageSource? ScreenshotPreview => ImageCache.GetImage(Activity.ScreenshotPath);

    public ImageSource? ProgramIcon => IconCache.GetIcon(Activity.ExecutablePath);

    public string Kind => Activity.Kind.ToString();

    public string KindLabel => Activity.Kind switch
    {
        ActivityKind.Browser => "Website",
        ActivityKind.Development => "Dev",
        _ => Activity.Kind.ToString()
    };

    public string IconGlyph => Activity.Kind switch
    {
        ActivityKind.Browser => "🌐",
        ActivityKind.Document => "W",
        ActivityKind.Development => "</>",
        ActivityKind.System => "⚙",
        _ => "□"
    };

    public string IconBackground => Activity.Kind switch
    {
        ActivityKind.Browser => "#E7F4FF",
        ActivityKind.Document => "#E7F0FF",
        ActivityKind.Development => "#EFE7FF",
        ActivityKind.System => "#EEF2F7",
        _ => "#E8F3FF"
    };

    public string IconForeground => Activity.Kind switch
    {
        ActivityKind.Browser => "#0A66C2",
        ActivityKind.Document => "#185ABD",
        ActivityKind.Development => "#6F3CC3",
        ActivityKind.System => "#475569",
        _ => "#0078D4"
    };

    public string KindBackground => Activity.Kind switch
    {
        ActivityKind.Browser => "#E7F4FF",
        ActivityKind.Document => "#E8F3FF",
        ActivityKind.Development => "#F0E8FF",
        ActivityKind.System => "#EEF2F7",
        _ => "#F0ECFF"
    };

    public string KindForeground => Activity.Kind switch
    {
        ActivityKind.Browser => "#0A66C2",
        ActivityKind.Document => "#185ABD",
        ActivityKind.Development => "#6F3CC3",
        ActivityKind.System => "#475569",
        _ => "#5B3DB3"
    };

    public string Location => Activity.Kind switch
    {
        ActivityKind.Browser => string.IsNullOrWhiteSpace(Activity.Resource) ? "Web activity" : Activity.Resource,
        ActivityKind.Document => "Documents",
        ActivityKind.Development => "Developer workspace",
        ActivityKind.System => "Windows",
        _ => Activity.ProcessName
    };

    public string DateGroup
    {
        get
        {
            var localDate = Activity.StartedAt.LocalDateTime.Date;
            var today = DateTime.Today;

            if (localDate == today)
            {
                return "Today";
            }

            if (localDate == today.AddDays(-1))
            {
                return "Yesterday";
            }

            if (localDate >= today.AddDays(-7))
            {
                return "Earlier this week";
            }

            return Activity.StartedAt.LocalDateTime.ToString("dddd, d MMMM yyyy");
        }
    }

    public string StartTime => Activity.StartedAt.LocalDateTime.ToString("h:mm tt");

    public DateTimeOffset SessionStartedAt => _sessionStartedAt;

    public DateTimeOffset SessionEndedAt => _sessionEndedAt;

    public string TimeRange => $"{_sessionStartedAt.LocalDateTime:t} - {_sessionEndedAt.LocalDateTime:t}";

    public string CardTime => _occurrenceCount > 1 ? TimeRange : StartTime;

    public string Timestamp => _sessionStartedAt.LocalDateTime.ToString("dddd, d MMM yyyy HH:mm:ss");

    public string Duration
    {
        get
        {
            var duration = _sessionEndedAt > _sessionStartedAt ? _sessionEndedAt - _sessionStartedAt : Activity.Duration;

            if (duration.TotalMinutes >= 1)
            {
                return $"{Math.Max(1, (int)Math.Round(duration.TotalMinutes))} min";
            }

            return $"{Math.Max(1, (int)Math.Round(duration.TotalSeconds))} sec";
        }
    }

    public string OccurrenceLabel => _occurrenceCount > 1 ? $"{_occurrenceCount} entries" : KindLabel;

    public string PinMark => Activity.IsPinned ? "PINNED" : string.Empty;

    public string PinActionLabel => Activity.IsPinned ? "Unpin" : "Pin";
}

file static class IconCache
{
    private static readonly Dictionary<string, ImageSource?> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static ImageSource? GetIcon(string? executablePath)
    {
        var path = executablePath;
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
            using var icon = Icon.ExtractAssociatedIcon(path);
            if (icon is null)
            {
                Cache[path] = null;
                return null;
            }

            var source = Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromWidthAndHeight(32, 32));
            source.Freeze();
            Cache[path] = source;
            return source;
        }
        catch
        {
            Cache[path] = null;
            return null;
        }
    }
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
