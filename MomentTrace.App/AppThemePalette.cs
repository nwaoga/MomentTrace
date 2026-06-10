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
        "Midnight",
        FromHex("#08111F"),
        FromHex("#121D2F"),
        FromHex("#F4F7FB"),
        FromHex("#A5B0C3"),
        FromHex("#8B5CF6"),
        FromHex("#26344D"));

    public static readonly AppThemePalette GitHubLight = new(
        "GitHubLight",
        "GitHub Light",
        FromHex("#F6F8FA"),
        FromHex("#FFFFFF"),
        FromHex("#24292F"),
        FromHex("#57606A"),
        FromHex("#0969DA"),
        FromHex("#D0D7DE"));

    public static readonly AppThemePalette StarbucksReserve = new(
        "StarbucksReserve",
        "Matcha Reserve",
        FromHex("#0F1512"),
        FromHex("#17201C"),
        FromHex("#F6F5F2"),
        FromHex("#AAB6AF"),
        FromHex("#00754A"),
        FromHex("#2E4D40"));

    public static readonly AppThemePalette CostaAfterglow = new(
        "CostaAfterglow",
        "Costa Afterglow",
        FromHex("#231019"),
        FromHex("#311722"),
        FromHex("#FFF5F7"),
        FromHex("#D8B7C0"),
        FromHex("#C64176"),
        FromHex("#5E3044"));

    public static readonly AppThemePalette EspressoMono = new(
        "EspressoMono",
        "Espresso (Mono)",
        FromHex("#18110B"),
        FromHex("#22170F"),
        FromHex("#F8F1E7"),
        FromHex("#C6B29C"),
        FromHex("#C6844C"),
        FromHex("#5E432B"));

    public static readonly AppThemePalette Light = new(
        "Light",
        "Hot White",
        FromHex("#F6F8FB"),
        FromHex("#FFFFFF"),
        FromHex("#172033"),
        FromHex("#64748B"),
        FromHex("#2563EB"),
        FromHex("#CBD5E1"));

    public WpfColor RaisedSurface => IsGitHubLight ? Mix(Surface, Base, 0.92) : Mix(Surface, Base, 0.58);

    public WpfColor Card => Mix(Surface, Base, 0.78);

    public WpfColor CardHover => Mix(Surface, Accent, 0.88);

    public WpfColor AccentSoft => Mix(Accent, Surface, 0.78);

    public WpfColor AccentGlow => WithAlpha(Accent, 0.28);

    public WpfColor Overlay => WithAlpha(Base, 0.72);

    public WpfColor FaintStroke => WithAlpha(Stroke, 0.45);

    public WpfColor Rail => IsGitHubLight ? Base : Surface;

    public WpfColor TopCommand => IsGitHubLight ? Surface : Mix(Base, Surface, 0.36);

    public WpfColor Detail => Card;

    public WpfColor AccentText => IsLight(Accent) ? FromHex("#111827") : FromHex("#FFFFFF");

    public WpfColor SubtleText => IsGitHubLight
        ? Mix(MutedText, Surface, 0.92)
        : IsStarbucksReserve
            ? Mix(MutedText, Base, 0.82)
            : Mix(MutedText, Base, 0.72);

    public WpfColor Toolbar => IsGitHubLight
        ? Surface
        : IsStarbucksReserve
            ? Mix(Surface, Base, 0.64)
            : Mix(Surface, Base, 0.5);

    public WpfColor ToolbarHover => IsGitHubLight ? Mix(Surface, Accent, 0.08) : CardHover;

    public WpfColor Chip => IsGitHubLight
        ? Mix(Base, Accent, 0.06)
        : IsStarbucksReserve
            ? Mix(Surface, Accent, 0.12)
            : Mix(Surface, Base, 0.42);

    public WpfColor PanelEdgeStrong => IsGitHubLight
        ? Stroke
        : IsStarbucksReserve
            ? Mix(Stroke, Accent, 0.12)
            : Mix(Stroke, Accent, 0.2);

    public WpfColor ActiveSurface => IsGitHubLight
        ? Mix(Surface, Accent, 0.10)
        : IsStarbucksReserve
            ? Mix(Surface, Accent, 0.22)
            : Accent;

    public WpfColor ActiveEdge => IsGitHubLight ? Mix(Stroke, Accent, 0.45) : Accent;

    public WpfColor ScrollTrack => IsGitHubLight ? Mix(Base, Surface, 0.82) : Mix(Base, Surface, 0.5);

    public WpfColor Success => FromHex("#22C55E");

    public WpfColor Info => FromHex("#3B82F6");

    public WpfColor Warning => FromHex("#F59E0B");

    public static IReadOnlyList<AppThemePalette> All { get; } =
    [
        Midnight,
        GitHubLight,
        StarbucksReserve,
        CostaAfterglow,
        EspressoMono,
        Light
    ];

    public static AppThemePalette Find(string? key)
    {
        if (string.Equals(key, "Coffee", StringComparison.OrdinalIgnoreCase))
        {
            return GitHubLight;
        }

        if (string.Equals(key, "AmazonGreen", StringComparison.OrdinalIgnoreCase))
        {
            return StarbucksReserve;
        }

        if (string.Equals(key, "MatchaReserve", StringComparison.OrdinalIgnoreCase)
            || string.Equals(key, "Matcha Reserve", StringComparison.OrdinalIgnoreCase))
        {
            return StarbucksReserve;
        }

        if (string.Equals(key, "Costa", StringComparison.OrdinalIgnoreCase)
            || string.Equals(key, "Costa Afterglow", StringComparison.OrdinalIgnoreCase))
        {
            return CostaAfterglow;
        }

        if (string.Equals(key, "Espresso", StringComparison.OrdinalIgnoreCase)
            || string.Equals(key, "Brown", StringComparison.OrdinalIgnoreCase)
            || string.Equals(key, "Espresso (Mono)", StringComparison.OrdinalIgnoreCase))
        {
            return EspressoMono;
        }

        if (string.Equals(key, "White Light", StringComparison.OrdinalIgnoreCase))
        {
            return Light;
        }

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
        SetBrush(target, "ToolbarBrush", Toolbar);
        SetBrush(target, "ToolbarHoverBrush", ToolbarHover);
        SetBrush(target, "ChipBrush", Chip);
        SetBrush(target, "PanelEdgeStrongBrush", PanelEdgeStrong);
        SetBrush(target, "SubtleTextBrush", SubtleText);
        SetBrush(target, "ScrollTrackBrush", ScrollTrack);
        SetBrush(target, "ActiveSurfaceBrush", ActiveSurface);
        SetBrush(target, "ActiveEdgeBrush", ActiveEdge);
        SetBrush(target, "CardBrush", Card);
        SetBrush(target, "CardHoverBrush", CardHover);
        SetBrush(target, "BorderBrush", Stroke);
        SetBrush(target, "FaintBorderBrush", FaintStroke);
        SetBrush(target, "TextBrush", Text);
        SetBrush(target, "MutedTextBrush", MutedText);
        SetBrush(target, "AccentBrush", Accent);
        SetBrush(target, "AccentSoftBrush", AccentSoft);
        SetBrush(target, "AccentGlowBrush", AccentGlow);
        SetBrush(target, "AccentTextBrush", AccentText);
        SetBrush(target, "OverlayBrush", Overlay);
        SetBrush(target, "SuccessBrush", Success);
        SetBrush(target, "InfoBrush", Info);
        SetBrush(target, "WarningBrush", Warning);
    }

    public WpfColor WithAlpha(WpfColor color, double opacity)
    {
        var strength = Math.Clamp(opacity, 0.0, 1.0);
        return WpfColor.FromArgb((byte)Math.Round(strength * 255), color.R, color.G, color.B);
    }

    public override string ToString() => DisplayName;

    private bool IsGitHubLight => string.Equals(Key, GitHubLight.Key, StringComparison.OrdinalIgnoreCase);

    private bool IsStarbucksReserve => string.Equals(Key, StarbucksReserve.Key, StringComparison.OrdinalIgnoreCase);

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
