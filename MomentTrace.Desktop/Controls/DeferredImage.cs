using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using System;
using System.Collections.Generic;
using System.IO;

namespace MomentTrace.Desktop.Controls;

public sealed class DeferredImage : Image
{
    public static readonly StyledProperty<string?> SourcePathProperty =
        AvaloniaProperty.Register<DeferredImage, string?>(nameof(SourcePath));

    private static readonly Dictionary<string, Bitmap> BitmapCache = new(StringComparer.OrdinalIgnoreCase);
    private bool _isAttached;

    static DeferredImage()
    {
        SourcePathProperty.Changed.AddClassHandler<DeferredImage>((image, _) => image.UpdateSource());
    }

    public string? SourcePath
    {
        get => GetValue(SourcePathProperty);
        set => SetValue(SourcePathProperty, value);
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _isAttached = true;
        UpdateSource();
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        _isAttached = false;
        Source = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void UpdateSource()
    {
        if (!_isAttached)
        {
            return;
        }

        Source = null;

        var path = SourcePath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        try
        {
            if (!BitmapCache.TryGetValue(path, out var bitmap))
            {
                bitmap = new Bitmap(path);
                BitmapCache[path] = bitmap;
            }

            Source = bitmap;
        }
        catch
        {
            Source = null;
        }
    }
}
