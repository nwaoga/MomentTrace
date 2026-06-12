using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using MomentTrace.Desktop;
using MomentTrace.Presentation;

namespace MomentTrace.Desktop.Controls;

public partial class ActivityHeatmapControl : UserControl
{
    private DesktopAppThemePalette _themePalette = DesktopAppThemePalette.Midnight;

    public static readonly StyledProperty<IEnumerable<HeatmapSegmentViewModel>?> ItemsProperty =
        AvaloniaProperty.Register<ActivityHeatmapControl, IEnumerable<HeatmapSegmentViewModel>?>(nameof(Items));

    public static readonly StyledProperty<IEnumerable<LegendItemViewModel>?> LegendItemsProperty =
        AvaloniaProperty.Register<ActivityHeatmapControl, IEnumerable<LegendItemViewModel>?>(nameof(LegendItems));

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<ActivityHeatmapControl, string?>(nameof(Title), "Activity heatmap");

    public static readonly StyledProperty<string?> CaptionProperty =
        AvaloniaProperty.Register<ActivityHeatmapControl, string?>(nameof(Caption), "15 minute blocks");

    public static readonly StyledProperty<string?> SubtitleProperty =
        AvaloniaProperty.Register<ActivityHeatmapControl, string?>(nameof(Subtitle), "Click a block to jump into that part of the day.");

    public static readonly StyledProperty<ICommand?> ItemCommandProperty =
        AvaloniaProperty.Register<ActivityHeatmapControl, ICommand?>(nameof(ItemCommand));

    static ActivityHeatmapControl()
    {
        ItemsProperty.Changed.AddClassHandler<ActivityHeatmapControl>((control, _) => control.UpdateRows());
        LegendItemsProperty.Changed.AddClassHandler<ActivityHeatmapControl>((control, _) => control.UpdateRows());
    }

    public ActivityHeatmapControl()
    {
        InitializeComponent();
        UpdateRows();
    }

    public IEnumerable<HeatmapSegmentViewModel>? Items
    {
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? Caption
    {
        get => GetValue(CaptionProperty);
        set => SetValue(CaptionProperty, value);
    }

    public string? Subtitle
    {
        get => GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public IEnumerable<LegendItemViewModel>? LegendItems
    {
        get => GetValue(LegendItemsProperty);
        set => SetValue(LegendItemsProperty, value);
    }

    public ICommand? ItemCommand
    {
        get => GetValue(ItemCommandProperty);
        set => SetValue(ItemCommandProperty, value);
    }

    private IEnumerable<HeatmapCellViewModel> FirstRowItems { get; set; } = [];

    private IEnumerable<HeatmapCellViewModel> SecondRowItems { get; set; } = [];

    public void ApplyThemePalette(DesktopAppThemePalette palette)
    {
        _themePalette = palette;
        UpdateRows();
    }

    private void UpdateRows()
    {
        var items = Items?.ToList() ?? [];
        var midpoint = (int)System.Math.Ceiling(items.Count / 2d);
        var hasVisibleActivity = items.Any(item => item.Intensity > 0d);

        FirstRowItems = items.Take(midpoint).Select(ToHeatmapCell).ToList();
        SecondRowItems = items.Skip(midpoint).Select(ToHeatmapCell).ToList();
        FirstHeatmapRow.ItemsSource = FirstRowItems;
        SecondHeatmapRow.ItemsSource = SecondRowItems;
        HeatmapLegendItems.ItemsSource = (LegendItems ?? [])
            .Select(item => new HeatmapLegendViewModel(item.Label, CreateBrush(GetCategoryColor(item.Label), 1d)))
            .ToList();
        HeatmapContent.IsVisible = hasVisibleActivity;
        HeatmapEmptyState.IsVisible = !hasVisibleActivity;
    }

    private HeatmapCellViewModel ToHeatmapCell(HeatmapSegmentViewModel segment)
    {
        return new HeatmapCellViewModel(
            segment,
            CreateBrush(GetCategoryColor(segment.CategoryLabel), segment.Intensity),
            segment.Tooltip);
    }

    private IBrush CreateBrush(Color categoryColor, double intensity)
    {
        if (intensity <= 0d)
        {
            return new SolidColorBrush(_themePalette.HeatmapEmpty);
        }

        var amount = intensity > 0.66d ? 1.0d : intensity > 0.33d ? 0.76d : 0.52d;
        return new SolidColorBrush(Mix(categoryColor, _themePalette.HeatmapEmpty, amount));
    }

    private Color GetCategoryColor(string? categoryLabel)
    {
        return categoryLabel switch
        {
            "Web browsing" => _themePalette.HeatmapWeb,
            "Development work" => _themePalette.HeatmapDevelopment,
            "Documents" => _themePalette.HeatmapDocuments,
            "Idle or system surfaces" => _themePalette.HeatmapGap,
            "Gaps or idle space" => _themePalette.HeatmapGap,
            "Idle / gaps" => _themePalette.HeatmapGap,
            _ => _themePalette.HeatmapApps
        };
    }

    private static Color Mix(Color foreground, Color background, double foregroundAmount)
    {
        var amount = System.Math.Clamp(foregroundAmount, 0d, 1d);
        var inverse = 1d - amount;

        return Color.FromRgb(
            (byte)System.Math.Round((foreground.R * amount) + (background.R * inverse)),
            (byte)System.Math.Round((foreground.G * amount) + (background.G * inverse)),
            (byte)System.Math.Round((foreground.B * amount) + (background.B * inverse)));
    }

    private sealed record HeatmapCellViewModel(HeatmapSegmentViewModel Segment, IBrush Brush, string Tooltip);

    private sealed record HeatmapLegendViewModel(string Label, IBrush Brush);
}
