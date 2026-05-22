namespace MomentTrace.Core;

public sealed class RuleBasedActivityClassifier : IActivityClassifier
{
    public static RuleBasedActivityClassifier Default { get; } = new();

    private static readonly HashSet<string> BrowserProcesses =
    [
        "msedge",
        "chrome",
        "firefox",
        "brave",
        "opera",
        "vivaldi",
        "iexplore"
    ];

    private static readonly HashSet<string> DevelopmentProcesses =
    [
        "code",
        "devenv",
        "rider64",
        "rider",
        "webstorm64",
        "pycharm64",
        "idea64",
        "terminal",
        "windowsterminal",
        "powershell",
        "pwsh",
        "cmd"
    ];

    private static readonly HashSet<string> CommunicationProcesses =
    [
        "teams",
        "slack",
        "discord",
        "outlook",
        "olk",
        "zoom",
        "linkedin"
    ];

    private static readonly HashSet<string> DocumentProcesses =
    [
        "winword",
        "excel",
        "powerpnt",
        "onenote",
        "acrord32",
        "acrobat",
        "notepad",
        "notepad++"
    ];

    public ActivityClassification Classify(TimelineActivity activity)
    {
        var process = Normalize(activity.ProcessName);
        var app = Normalize(activity.AppName);
        var title = Normalize(activity.WindowTitle);
        var combined = $"{process} {app} {title}";

        if (activity.Kind == ActivityKind.Development || DevelopmentProcesses.Contains(process))
        {
            return new ActivityClassification(
                ActivityCategory.Development,
                "Development work",
                "Worked across code, terminal, debugging, or local project tools.",
                "#71C562",
                "</>");
        }

        if (IsScreenshotCapture(combined))
        {
            return new ActivityClassification(
                ActivityCategory.ScreenshotCapture,
                "Screenshot capture",
                "Captured or reviewed a visual snapshot.",
                "#B89CFF",
                "◱");
        }

        if (activity.Kind == ActivityKind.Browser || BrowserProcesses.Contains(process))
        {
            return new ActivityClassification(
                IsResearch(activity) ? ActivityCategory.Research : ActivityCategory.Browsing,
                IsResearch(activity) ? "Research session" : "Website browsing",
                IsResearch(activity)
                    ? "Compared pages, searched, or reviewed information across the web."
                    : "Visited websites and browser pages.",
                "#5CA8FF",
                "◎");
        }

        if (CommunicationProcesses.Contains(process) || ContainsAny(combined, "teams", "slack", "chat", "meeting", "message", "linkedin"))
        {
            return new ActivityClassification(
                ActivityCategory.Communication,
                "Communication",
                "Reviewed messages, meetings, or conversations.",
                "#67D4B1",
                "◌");
        }

        if (activity.Kind == ActivityKind.Document || DocumentProcesses.Contains(process) || ContainsAny(combined, ".pdf", "document", "spreadsheet", "presentation"))
        {
            return new ActivityClassification(
                ActivityCategory.Documents,
                "Document review",
                "Reviewed or edited files, notes, documents, or reference material.",
                "#E7B84E",
                "▤");
        }

        if (activity.Kind == ActivityKind.System)
        {
            return new ActivityClassification(
                ActivityCategory.System,
                "System activity",
                "Used a Windows surface or utility.",
                "#8D9BB0",
                "⚙");
        }

        return new ActivityClassification(
            ActivityCategory.Other,
            "App activity",
            "Used a desktop app or workspace surface.",
            "#8B5CF6",
            "□");
    }

    private static bool IsScreenshotCapture(string value)
    {
        return ContainsAny(value, "snipping", "screenshot", "screen sketch", "snip");
    }

    private static bool IsResearch(TimelineActivity activity)
    {
        var value = Normalize($"{activity.WindowTitle} {activity.Resource}");
        return ContainsAny(value, "search", "google", "bing", "docs", "documentation", "pricing", "review", "compare", "github", "stackoverflow");
    }

    private static bool ContainsAny(string value, params string[] needles)
    {
        return needles.Any(needle => value.Contains(needle, StringComparison.OrdinalIgnoreCase));
    }

    private static string Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
    }
}
