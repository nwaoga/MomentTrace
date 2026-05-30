using MomentTrace.Presentation;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MomentTrace.App;

public sealed class WpfAppIconProvider : IAppIconProvider<ImageSource>
{
    private readonly Dictionary<string, ImageSource?> _cache = new(StringComparer.OrdinalIgnoreCase);

    public ImageSource? GetIcon(string? executablePath, string? appName, string? processName)
    {
        var path = executablePath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        if (_cache.TryGetValue(path, out var cached))
        {
            return cached;
        }

        try
        {
            using var icon = Icon.ExtractAssociatedIcon(path);
            if (icon is null)
            {
                _cache[path] = null;
                return null;
            }

            var source = Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromWidthAndHeight(32, 32));
            source.Freeze();
            _cache[path] = source;
            return source;
        }
        catch
        {
            _cache[path] = null;
            return null;
        }
    }
}
