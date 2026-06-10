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
        "Midnight",
        FromHex("#08111F"),
        FromHex("#121D2F"),
        FromHex("#F4F7FB"),
        FromHex("#A5B0C3"),
        FromHex("#8B5CF6"),
        FromHex("#26344D"));

    public static readonly DesktopAppThemePalette GitHubLight = new(
        "GitHubLight",
        "GitHub Light",
        FromHex("#F6F8FA"),
        FromHex("#FFFFFF"),
        FromHex("#24292F"),
        FromHex("#57606A"),
        FromHex("#0969DA"),
        FromHex("#D0D7DE"));

    public static readonly DesktopAppThemePalette StarbucksReserve = new(
        "StarbucksReserve",
        "Starbucks Reserve",
        FromHex("#0F1512"),
        FromHex("#17201C"),
        FromHex("#F6F5F2"),
        FromHex("#AAB6AF"),
        FromHex("#00754A"),
        FromHex("#2E4D40"));

    public static readonly DesktopAppThemePalette Light = new(
        "Light",
        "Light",
        FromHex("#F6F8FB"),
        FromHex("#FFFFFF"),
        FromHex("#172033"),
        FromHex("#64748B"),
        FromHex("#2563EB"),
        FromHex("#CBD5E1"));

    public Color RaisedSurface => IsGitHubLight ? Mix(Surface, Base, 0.92) : Mix(Surface, Base, 0.58);
    public Color Card => Mix(Surface, Base, 0.78);
    public Color CardHover => Mix(Surface, Accent, 0.88);
    public Color AccentSoft => Mix(Accent, Surface, 0.78);
    public Color AccentGlow => WithAlpha(Accent, 0.28);
    public Color Overlay => WithAlpha(Base, 0.72);
    public Color FaintStroke => WithAlpha(Stroke, 0.45);
    public Color Rail => IsGitHubLight ? Base : Surface;
    public Color TopCommand => IsGitHubLight ? Surface : Mix(Base, Surface, 0.36);
    public Color Detail => Card;
    public Color AccentText => IsLight(Accent) ? FromHex("#111827") : FromHex("#FFFFFF");
    public Color SubtleText => IsGitHubLight
        ? Mix(MutedText, Surface, 0.92)
        : IsStarbucksReserve
            ? Mix(MutedText, Base, 0.82)
            : Mix(MutedText, Base, 0.72);
    public Color Toolbar => IsGitHubLight
        ? Surface
        : IsStarbucksReserve
            ? Mix(Surface, Base, 0.64)
            : Mix(Surface, Base, 0.5);
    public Color ToolbarHover => IsGitHubLight ? Mix(Surface, Accent, 0.08) : CardHover;
    public Color Chip => IsGitHubLight
        ? Mix(Base, Accent, 0.06)
        : IsStarbucksReserve
            ? Mix(Surface, Accent, 0.12)
            : Mix(Surface, Base, 0.42);
    public Color PanelEdgeStrong => IsGitHubLight
        ? Stroke
        : IsStarbucksReserve
            ? Mix(Stroke, Accent, 0.12)
            : Mix(Stroke, Accent, 0.2);
    public Color ActiveSurface => IsGitHubLight
        ? Mix(Surface, Accent, 0.10)
        : IsStarbucksReserve
            ? Mix(Surface, Accent, 0.22)
            : Accent;
    public Color ActiveEdge => IsGitHubLight ? Mix(Stroke, Accent, 0.45) : Accent;
    public Color ScrollThumb => Accent;
    public Color ScrollThumbHover => Mix(Accent, Text, 0.18);
    public Color ScrollTrack => IsGitHubLight ? Mix(Base, Surface, 0.82) : Mix(Base, Surface, 0.5);
    public Color Danger => Mix(FromHex("#8E3755"), Accent, 0.12);
    public Color DangerFill => Mix(Base, FromHex("#3D1F2C"), 0.35);
    public Color Success => FromHex("#22C55E");
    public Color Info => FromHex("#3B82F6");
    public Color Warning => FromHex("#F59E0B");

    public static IReadOnlyList<DesktopAppThemePalette> All { get; } =
    [
        Midnight,
        GitHubLight,
        StarbucksReserve,
        Light
    ];

    public static DesktopAppThemePalette Find(string? key)
    {
        if (string.Equals(key, "Coffee", StringComparison.OrdinalIgnoreCase))
        {
            return GitHubLight;
        }

        if (string.Equals(key, "AmazonGreen", StringComparison.OrdinalIgnoreCase))
        {
            return StarbucksReserve;
        }

        if (string.Equals(key, "White Light", StringComparison.OrdinalIgnoreCase))
        {
            return Light;
        }

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
        SetBrush(target, "ElevatedPanelBrush", Card);
        SetBrush(target, "CardBrush", Card);
        SetBrush(target, "CardHoverBrush", CardHover);
        SetBrush(target, "PanelEdgeBrush", Stroke);
        SetBrush(target, "FaintBorderBrush", FaintStroke);
        SetBrush(target, "PanelEdgeStrongBrush", PanelEdgeStrong);
        SetBrush(target, "TextBrush", Text);
        SetBrush(target, "MutedTextBrush", MutedText);
        SetBrush(target, "SubtleTextBrush", SubtleText);
        SetBrush(target, "AccentBrush", Accent);
        SetBrush(target, "AccentSoftBrush", AccentSoft);
        SetBrush(target, "AccentGlowBrush", AccentGlow);
        SetBrush(target, "AccentTextBrush", AccentText);
        SetBrush(target, "OverlayBrush", Overlay);
        SetBrush(target, "ChipBrush", Chip);
        SetBrush(target, "ToolbarBrush", Toolbar);
        SetBrush(target, "ToolbarHoverBrush", ToolbarHover);
        SetBrush(target, "SuccessBrush", Success);
        SetBrush(target, "InfoBrush", Info);
        SetBrush(target, "WarningBrush", Warning);
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

    private bool IsGitHubLight => string.Equals(Key, GitHubLight.Key, StringComparison.OrdinalIgnoreCase);

    private bool IsStarbucksReserve => string.Equals(Key, StarbucksReserve.Key, StringComparison.OrdinalIgnoreCase);

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
