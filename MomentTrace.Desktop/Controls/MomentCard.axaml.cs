using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using System.IO;

namespace MomentTrace.Desktop.Controls;

public partial class MomentCard : UserControl
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<MomentCard, string?>(nameof(Title));

    public static readonly StyledProperty<string?> SubtitleProperty =
        AvaloniaProperty.Register<MomentCard, string?>(nameof(Subtitle));

    public static readonly StyledProperty<string?> TimestampProperty =
        AvaloniaProperty.Register<MomentCard, string?>(nameof(Timestamp));

    public static readonly StyledProperty<string?> DurationProperty =
        AvaloniaProperty.Register<MomentCard, string?>(nameof(Duration));

    public static readonly StyledProperty<string?> CategoryProperty =
        AvaloniaProperty.Register<MomentCard, string?>(nameof(Category));

    public static readonly StyledProperty<string?> AccentFillProperty =
        AvaloniaProperty.Register<MomentCard, string?>(nameof(AccentFill));

    public static readonly StyledProperty<string?> AccentTextProperty =
        AvaloniaProperty.Register<MomentCard, string?>(nameof(AccentText), "#FFFFFF");

    public static readonly StyledProperty<string?> SourceProperty =
        AvaloniaProperty.Register<MomentCard, string?>(nameof(Source));

    public static readonly StyledProperty<string?> PreviewPathProperty =
        AvaloniaProperty.Register<MomentCard, string?>(nameof(PreviewPath));

    static MomentCard()
    {
        PreviewPathProperty.Changed.AddClassHandler<MomentCard>((card, _) => card.UpdatePreviewImage());
    }

    public MomentCard()
    {
        InitializeComponent();
        UpdatePreviewImage();
    }

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? Subtitle
    {
        get => GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public string? Timestamp
    {
        get => GetValue(TimestampProperty);
        set => SetValue(TimestampProperty, value);
    }

    public string? Duration
    {
        get => GetValue(DurationProperty);
        set => SetValue(DurationProperty, value);
    }

    public string? Category
    {
        get => GetValue(CategoryProperty);
        set => SetValue(CategoryProperty, value);
    }

    public string? AccentFill
    {
        get => GetValue(AccentFillProperty);
        set => SetValue(AccentFillProperty, value);
    }

    public string? AccentText
    {
        get => GetValue(AccentTextProperty);
        set => SetValue(AccentTextProperty, value);
    }

    public string? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public string? PreviewPath
    {
        get => GetValue(PreviewPathProperty);
        set => SetValue(PreviewPathProperty, value);
    }

    private void UpdatePreviewImage()
    {
        PreviewImage.Source = null;
        PreviewImage.IsVisible = false;

        var path = PreviewPath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        try
        {
            PreviewImage.Source = new Bitmap(path);
            PreviewImage.IsVisible = true;
        }
        catch
        {
            PreviewImage.Source = null;
            PreviewImage.IsVisible = false;
        }
    }
}
