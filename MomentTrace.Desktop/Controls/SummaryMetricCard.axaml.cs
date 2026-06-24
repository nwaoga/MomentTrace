using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace MomentTrace.Desktop.Controls;

public partial class SummaryMetricCard : UserControl
{
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<SummaryMetricCard, string?>(nameof(Label));

    public static readonly StyledProperty<string?> ValueProperty =
        AvaloniaProperty.Register<SummaryMetricCard, string?>(nameof(Value));

    public static readonly StyledProperty<string?> DetailProperty =
        AvaloniaProperty.Register<SummaryMetricCard, string?>(nameof(Detail));

    public static readonly StyledProperty<string?> AccentFillProperty =
        AvaloniaProperty.Register<SummaryMetricCard, string?>(nameof(AccentFill));

    public static readonly StyledProperty<string?> AccentGlyphProperty =
        AvaloniaProperty.Register<SummaryMetricCard, string?>(nameof(AccentGlyph));

    static SummaryMetricCard()
    {
        ValueProperty.Changed.AddClassHandler<SummaryMetricCard>((control, _) => control.UpdateQuietValueStyle());
    }

    public SummaryMetricCard()
    {
        InitializeComponent();
        UpdateQuietValueStyle();
    }

    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string? Detail
    {
        get => GetValue(DetailProperty);
        set => SetValue(DetailProperty, value);
    }

    public string? AccentFill
    {
        get => GetValue(AccentFillProperty);
        set => SetValue(AccentFillProperty, value);
    }

    public string? AccentGlyph
    {
        get => GetValue(AccentGlyphProperty);
        set => SetValue(AccentGlyphProperty, value);
    }

    private void UpdateQuietValueStyle()
    {
        if (this.FindControl<TextBlock>("ValueTextBlock") is not { } valueBlock)
        {
            return;
        }

        var isQuiet = string.IsNullOrWhiteSpace(Value)
            || Value is "0"
            || Value.Equals("0m", StringComparison.OrdinalIgnoreCase)
            || Value.Equals("None yet", StringComparison.OrdinalIgnoreCase);

        valueBlock.Foreground = isQuiet
            ? this.FindResource("SubtleTextBrush") as IBrush ?? Brushes.Gray
            : this.FindResource("TextBrush") as IBrush ?? Brushes.White;
    }
}
