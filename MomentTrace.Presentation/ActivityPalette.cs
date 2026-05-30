namespace MomentTrace.Presentation;

public static class ActivityPalette
{
    public const string Apps = "#8B5CF6";
    public const string Web = "#2F80ED";
    public const string Development = "#22C55E";
    public const string Documents = "#F59E0B";
    public const string SystemActivity = "#38BDF8";
    public const string Gap = "#10233F";
    public const string Empty = "#152033";

    public static string Scale(string hex, double intensity)
    {
        var color = ParseHex(hex);
        var amount = intensity > 0.66 ? 1.0 : intensity > 0.33 ? 0.72 : 0.44;
        var baseColor = ParseHex(Empty);
        return ToHex(Mix(color, baseColor, amount));
    }

    private static (byte Red, byte Green, byte Blue) ParseHex(string hex)
    {
        var value = hex.TrimStart('#');
        return (
            Convert.ToByte(value[..2], 16),
            Convert.ToByte(value.Substring(2, 2), 16),
            Convert.ToByte(value.Substring(4, 2), 16));
    }

    private static (byte Red, byte Green, byte Blue) Mix(
        (byte Red, byte Green, byte Blue) foreground,
        (byte Red, byte Green, byte Blue) background,
        double foregroundAmount)
    {
        var inverse = 1 - foregroundAmount;
        return (
            (byte)Math.Round((foreground.Red * foregroundAmount) + (background.Red * inverse)),
            (byte)Math.Round((foreground.Green * foregroundAmount) + (background.Green * inverse)),
            (byte)Math.Round((foreground.Blue * foregroundAmount) + (background.Blue * inverse)));
    }

    private static string ToHex((byte Red, byte Green, byte Blue) color)
    {
        return $"#{color.Red:X2}{color.Green:X2}{color.Blue:X2}";
    }
}

