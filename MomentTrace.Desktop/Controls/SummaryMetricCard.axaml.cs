using Avalonia;
using Avalonia.Controls;

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

    public SummaryMetricCard()
    {
        InitializeComponent();
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
}
