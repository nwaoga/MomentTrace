using System.Windows;
using System.Windows.Media;
using WpfColor = System.Windows.Media.Color;

namespace MomentTrace.App;

public sealed record AppThemePalette(
    string Key,
    string DisplayName,
    WpfColor Base,
    WpfColor Surface,
    WpfColor Text,
    WpfColor MutedText,
    WpfColor Accent,
    WpfColor Stroke)
{
    public static readonly AppThemePalette Midnight = new(
        "Midnight",
        "Midnight (Dark)",
        FromHex("#08111F"),
        FromHex("#121D2F"),
        FromHex("#F4F7FB"),
        FromHex("#A5B0C3"),
        FromHex("#8B5CF6"),
        FromHex("#26344D"));

    public static readonly AppThemePalette Coffee = new(
        "Coffee",
        "Coffee (Light)",
        FromHex("#FFF7ED"),
        FromHex("#FFF1E3"),
        FromHex("#2A1B12"),
        FromHex("#7B604D"),
        FromHex("#8A4B25"),
        FromHex("#E2C2A6"));

    public static readonly AppThemePalette AmazonGreen = new(
        "AmazonGreen",
        "Amazon Green (Dark)",
        FromHex("#061713"),
        FromHex("#0D221B"),
        FromHex("#F1FFF8"),
        FromHex("#9CC8B7"),
        FromHex("#00A862"),
        FromHex("#245342"));

    public static readonly AppThemePalette Light = new(
        "Light",
        "White Light",
        FromHex("#F6F8FB"),
        FromHex("#FFFFFF"),
        FromHex("#172033"),
        FromHex("#64748B"),
        FromHex("#2563EB"),
        FromHex("#CBD5E1"));

    public WpfColor RaisedSurface => Mix(Surface, Base, 0.58);

    public WpfColor AccentSoft => Mix(Accent, Surface, 0.78);

    public WpfColor Rail => Surface;

    public WpfColor TopCommand => Mix(Base, Surface, 0.36);

    public WpfColor Detail => Surface;

    public WpfColor AccentText => IsLight(Accent) ? FromHex("#111827") : FromHex("#FFFFFF");

    public static IReadOnlyList<AppThemePalette> All { get; } =
    [
        Midnight,
        Coffee,
        AmazonGreen,
        Light
    ];

    public static AppThemePalette Find(string? key)
    {
        return All.FirstOrDefault(theme => string.Equals(theme.Key, key, StringComparison.OrdinalIgnoreCase)
            || string.Equals(theme.DisplayName, key, StringComparison.OrdinalIgnoreCase))
            ?? Midnight;
    }

    public void ApplyTo(FrameworkElement target)
    {
        SetBrush(target, "PageBackgroundBrush", Base);
        SetBrush(target, "RailBrush", Rail);
        SetBrush(target, "PanelBrush", Surface);
        SetBrush(target, "SubtlePanelBrush", RaisedSurface);
        SetBrush(target, "BorderBrush", Stroke);
        SetBrush(target, "TextBrush", Text);
        SetBrush(target, "MutedTextBrush", MutedText);
        SetBrush(target, "AccentBrush", Accent);
        SetBrush(target, "AccentSoftBrush", AccentSoft);
        SetBrush(target, "AccentTextBrush", AccentText);
    }

    public WpfColor WithAlpha(WpfColor color, double opacity)
    {
        var strength = Math.Clamp(opacity, 0.55, 1.0);
        return WpfColor.FromArgb((byte)Math.Round(strength * 255), color.R, color.G, color.B);
    }

    public override string ToString() => DisplayName;

    private static WpfColor Mix(WpfColor foreground, WpfColor background, double foregroundAmount)
    {
        var amount = Math.Clamp(foregroundAmount, 0, 1);
        var inverse = 1 - amount;
        return WpfColor.FromRgb(
            (byte)Math.Round((foreground.R * amount) + (background.R * inverse)),
            (byte)Math.Round((foreground.G * amount) + (background.G * inverse)),
            (byte)Math.Round((foreground.B * amount) + (background.B * inverse)));
    }

    private static bool IsLight(WpfColor color)
    {
        var luminance = (0.2126 * color.R) + (0.7152 * color.G) + (0.0722 * color.B);
        return luminance > 150;
    }

    private static void SetBrush(FrameworkElement target, string key, WpfColor color)
    {
        if (target.Resources[key] is SolidColorBrush { IsFrozen: false } brush)
        {
            brush.Color = color;
            return;
        }

        target.Resources[key] = new SolidColorBrush(color);
    }

    private static WpfColor FromHex(string hex)
    {
        return (WpfColor)System.Windows.Media.ColorConverter.ConvertFromString(hex);
    }
}
