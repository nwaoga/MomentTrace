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
        AvaloniaProperty.Register<ActivityHeatmapControl, string?>(nameof(Caption), "10 minute blocks");

    public static readonly StyledProperty<int> RowColumnCountProperty =
        AvaloniaProperty.Register<ActivityHeatmapControl, int>(nameof(RowColumnCount), 72);

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

    public int RowColumnCount
    {
        get => GetValue(RowColumnCountProperty);
        set => SetValue(RowColumnCountProperty, value);
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

        RowColumnCount = midpoint;
        FirstRowItems = items.Take(midpoint).Select(ToHeatmapCell).ToList();
        SecondRowItems = items.Skip(midpoint).Select(ToHeatmapCell).ToList();
        FirstHeatmapRow.ItemsSource = FirstRowItems;
        SecondHeatmapRow.ItemsSource = SecondRowItems;
        HeatmapLegendItems.ItemsSource = (LegendItems ?? [])
            .Select(item => new HeatmapLegendViewModel(item.Label, CreateLegendBrush(item)))
            .ToList();
        HeatmapContent.IsVisible = hasVisibleActivity;
        HeatmapEmptyState.IsVisible = !hasVisibleActivity;
    }

    private HeatmapCellViewModel ToHeatmapCell(HeatmapSegmentViewModel segment)
    {
        return new HeatmapCellViewModel(
            segment,
            CreateBrush(segment),
            segment.Tooltip);
    }

    private IBrush CreateBrush(HeatmapSegmentViewModel segment)
    {
        if (segment.Intensity <= 0d || IsGapLabel(segment.CategoryLabel))
        {
            return new SolidColorBrush(_themePalette.HeatmapEmpty);
        }

        return Brush.Parse(segment.Fill);
    }

    private IBrush CreateLegendBrush(LegendItemViewModel item)
    {
        return IsGapLabel(item.Label)
            ? new SolidColorBrush(_themePalette.HeatmapEmpty)
            : Brush.Parse(item.Fill);
    }

    private static bool IsGapLabel(string? label)
    {
        return string.Equals(label, "Gaps or idle space", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(label, "Idle / gaps", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(label, "Idle or system surfaces", System.StringComparison.OrdinalIgnoreCase);
    }

    private sealed record HeatmapCellViewModel(HeatmapSegmentViewModel Segment, IBrush Brush, string Tooltip);

    private sealed record HeatmapLegendViewModel(string Label, IBrush Brush);
}
