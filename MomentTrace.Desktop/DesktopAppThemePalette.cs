using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace MomentTrace.Desktop;

public sealed record DesktopAppThemePalette(
    string Key,
    string DisplayName,
    Color Base,
    Color Surface,
    Color Text,
    Color MutedText,
    Color Accent,
    Color Stroke)
{
    public static readonly DesktopAppThemePalette Midnight = new(
        "Midnight",
        "Midnight (Dark)",
        FromHex("#08111F"),
        FromHex("#121D2F"),
        FromHex("#F4F7FB"),
        FromHex("#A5B0C3"),
        FromHex("#8B5CF6"),
        FromHex("#26344D"));

    public static readonly DesktopAppThemePalette Coffee = new(
        "Coffee",
        "Coffee (Light)",
        FromHex("#FFF7ED"),
        FromHex("#FFF1E3"),
        FromHex("#2A1B12"),
        FromHex("#7B604D"),
        FromHex("#8A4B25"),
        FromHex("#E2C2A6"));

    public static readonly DesktopAppThemePalette AmazonGreen = new(
        "AmazonGreen",
        "Amazon Green (Dark)",
        FromHex("#061713"),
        FromHex("#0D221B"),
        FromHex("#F1FFF8"),
        FromHex("#9CC8B7"),
        FromHex("#00A862"),
        FromHex("#245342"));

    public static readonly DesktopAppThemePalette Light = new(
        "Light",
        "White Light",
        FromHex("#F6F8FB"),
        FromHex("#FFFFFF"),
        FromHex("#172033"),
        FromHex("#64748B"),
        FromHex("#2563EB"),
        FromHex("#CBD5E1"));

    public Color RaisedSurface => Mix(Surface, Base, 0.58);
    public Color AccentSoft => Mix(Accent, Surface, 0.78);
    public Color Rail => Surface;
    public Color TopCommand => Mix(Base, Surface, 0.36);
    public Color Detail => Surface;
    public Color AccentText => FromHex("#FFFFFF");
    public Color SubtleText => Mix(MutedText, Base, 0.72);
    public Color Toolbar => Mix(Surface, Base, 0.5);
    public Color ToolbarHover => Mix(Surface, Accent, 0.18);
    public Color Chip => Mix(Surface, Base, 0.42);
    public Color PanelEdgeStrong => Mix(Stroke, Accent, 0.2);
    public Color ActiveSurface => Accent;
    public Color ActiveEdge => Accent;
    public Color ScrollThumb => Accent;
    public Color ScrollThumbHover => Mix(Accent, Text, 0.18);
    public Color ScrollTrack => Mix(Base, Surface, 0.5);
    public Color Danger => Mix(FromHex("#8E3755"), Accent, 0.12);
    public Color DangerFill => Mix(Base, FromHex("#3D1F2C"), 0.35);

    public static IReadOnlyList<DesktopAppThemePalette> All { get; } =
    [
        Midnight,
        Coffee,
        AmazonGreen,
        Light
    ];

    public static DesktopAppThemePalette Find(string? key)
    {
        return All.FirstOrDefault(theme => string.Equals(theme.Key, key, StringComparison.OrdinalIgnoreCase)
            || string.Equals(theme.DisplayName, key, StringComparison.OrdinalIgnoreCase))
            ?? Midnight;
    }

    public void ApplyTo(StyledElement target)
    {
        SetBrush(target, "PageBackgroundBrush", Base);
        SetBrush(target, "RailBrush", Rail);
        SetBrush(target, "PanelBrush", Surface);
        SetBrush(target, "SubtlePanelBrush", RaisedSurface);
        SetBrush(target, "ElevatedPanelBrush", Mix(Surface, Base, 0.66));
        SetBrush(target, "PanelEdgeBrush", Stroke);
        SetBrush(target, "PanelEdgeStrongBrush", PanelEdgeStrong);
        SetBrush(target, "TextBrush", Text);
        SetBrush(target, "MutedTextBrush", MutedText);
        SetBrush(target, "SubtleTextBrush", SubtleText);
        SetBrush(target, "AccentBrush", Accent);
        SetBrush(target, "AccentSoftBrush", AccentSoft);
        SetBrush(target, "AccentTextBrush", AccentText);
        SetBrush(target, "ChipBrush", Chip);
        SetBrush(target, "ToolbarBrush", Toolbar);
        SetBrush(target, "ToolbarHoverBrush", ToolbarHover);
        SetBrush(target, "DangerBrush", Danger);
        SetBrush(target, "DangerFillBrush", DangerFill);
        SetBrush(target, "ScrollThumbBrush", ScrollThumb);
        SetBrush(target, "ScrollThumbHoverBrush", ScrollThumbHover);
        SetBrush(target, "ScrollTrackBrush", ScrollTrack);
    }

    public Color WithAlpha(Color color, double opacity)
    {
        var strength = Math.Clamp(opacity, 0.0, 1.0);
        return Color.FromArgb((byte)Math.Round(strength * 255), color.R, color.G, color.B);
    }

    public override string ToString() => DisplayName;

    private static void SetBrush(StyledElement target, string key, Color color)
    {
        target.Resources[key] = new SolidColorBrush(color);
    }

    private static Color Mix(Color foreground, Color background, double foregroundAmount)
    {
        var amount = Math.Clamp(foregroundAmount, 0, 1);
        var inverse = 1 - amount;
        return Color.FromRgb(
            (byte)Math.Round((foreground.R * amount) + (background.R * inverse)),
            (byte)Math.Round((foreground.G * amount) + (background.G * inverse)),
            (byte)Math.Round((foreground.B * amount) + (background.B * inverse)));
    }

    private static bool IsLight(Color color)
    {
        var luminance = (0.2126 * color.R) + (0.7152 * color.G) + (0.0722 * color.B);
        return luminance > 150;
    }

    private static Color FromHex(string hex) => Color.Parse(hex);
}
