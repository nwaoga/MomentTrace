using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using MomentTrace.Presentation;

namespace MomentTrace.Desktop.Controls;

public partial class ActivityHeatmapControl : UserControl
{
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

    public IEnumerable<HeatmapSegmentViewModel> FirstRowItems { get; private set; } = [];

    public IEnumerable<HeatmapSegmentViewModel> SecondRowItems { get; private set; } = [];

    private void UpdateRows()
    {
        var items = Items?.ToList() ?? [];
        var midpoint = (int)System.Math.Ceiling(items.Count / 2d);
        FirstRowItems = items.Take(midpoint).ToList();
        SecondRowItems = items.Skip(midpoint).ToList();
        FirstHeatmapRow.ItemsSource = FirstRowItems;
        SecondHeatmapRow.ItemsSource = SecondRowItems;
    }
}
