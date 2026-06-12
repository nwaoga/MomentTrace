using System.Collections.Generic;
using System.Linq;
using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using MomentTrace.Presentation;

namespace MomentTrace.Desktop.Controls;

public partial class CategoryBreakdownControl : UserControl
{
    public static readonly StyledProperty<IEnumerable<CategoryBreakdownItemViewModel>?> ItemsProperty =
        AvaloniaProperty.Register<CategoryBreakdownControl, IEnumerable<CategoryBreakdownItemViewModel>?>(nameof(Items));

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<CategoryBreakdownControl, string?>(nameof(Title), "Activity breakdown");

    static CategoryBreakdownControl()
    {
        ItemsProperty.Changed.AddClassHandler<CategoryBreakdownControl>((control, _) => control.UpdateChart());
    }

    public CategoryBreakdownControl()
    {
        InitializeComponent();
        UpdateChart();
    }

    public IEnumerable<CategoryBreakdownItemViewModel>? Items
    {
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    private void UpdateChart()
    {
        var items = Items?.Where(item => item.PercentOfActive > 0).ToList() ?? [];
        var hasItems = items.Count > 0;

        BreakdownCenterValueText.Text = items.Count == 0
            ? "0m"
            : OverviewPresentationBuilder.FormatDuration(TimeSpan.FromTicks(items.Sum(item => item.Duration.Ticks)));
        BreakdownCenterLabelText.Text = "total active";
        BreakdownContent.IsVisible = hasItems;
        BreakdownEmptyState.IsVisible = !hasItems;
        BreakdownDetailsItems.ItemsSource = items;
        BreakdownLegendItems.ItemsSource = items;

        if (!hasItems)
        {
            BreakdownChartItems.ItemsSource = null;
            return;
        }

        const double center = 72d;
        const double radius = 60d;
        const double strokeThickness = 16d;
        var startAngle = -90d;
        var segments = new List<BreakdownChartSegment>();

        foreach (var item in items)
        {
            var sweep = Math.Max(4d, item.PercentOfActive * 360d);
            var geometry = BuildArcGeometry(center, center, radius, startAngle, sweep);
            segments.Add(new BreakdownChartSegment(geometry, Brush.Parse(item.Fill), strokeThickness));
            startAngle += sweep;
        }

        BreakdownChartItems.ItemsSource = segments;
    }

    private static Geometry BuildArcGeometry(double centerX, double centerY, double radius, double startAngle, double sweepAngle)
    {
        var startRadians = Math.PI * startAngle / 180d;
        var endRadians = Math.PI * (startAngle + sweepAngle) / 180d;
        var startPoint = new Point(centerX + radius * Math.Cos(startRadians), centerY + radius * Math.Sin(startRadians));
        var endPoint = new Point(centerX + radius * Math.Cos(endRadians), centerY + radius * Math.Sin(endRadians));
        var isLargeArc = sweepAngle > 180d;

        var geometry = new StreamGeometry();
        using var context = geometry.Open();
        context.BeginFigure(startPoint, isFilled: false);
        context.ArcTo(endPoint, new Size(radius, radius), 0, isLargeArc, SweepDirection.Clockwise);
        return geometry;
    }

    private sealed record BreakdownChartSegment(Geometry Data, IBrush Stroke, double StrokeThickness);
}
