namespace MomentTrace.Desktop;

public sealed class PrivacyChoiceViewModel(string title, string subtitle, string value, bool isBlocked)
{
    public string Title { get; } = title;
    public string Subtitle { get; } = subtitle;
    public string Value { get; } = value;
    public bool IsBlocked { get; set; } = isBlocked;
}
