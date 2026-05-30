namespace MomentTrace.Core;

public static class ActivityClassifier
{
    private static readonly HashSet<string> BrowserProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "chrome", "msedge", "firefox", "brave", "opera", "vivaldi",
        "safari", "googlechrome", "microsoftedge"
    };

    private static readonly HashSet<string> DocumentProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "winword", "excel", "powerpnt", "acrord32", "onenote", "notepad", "notepad++", "wordpad",
        "microsoftword", "microsoftexcel", "microsoftpowerpoint", "preview", "pages", "numbers", "keynote"
    };

    private static readonly HashSet<string> DevelopmentProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "devenv", "code", "rider64", "idea64", "webstorm64", "powershell", "pwsh", "cmd", "windowsterminal",
        "visualstudiocode", "xcode", "terminal", "iterm2", "rider", "intellijidea", "webstorm"
    };

    public static ActivityKind Classify(string processName)
    {
        if (BrowserProcesses.Contains(processName))
        {
            return ActivityKind.Browser;
        }

        if (DocumentProcesses.Contains(processName))
        {
            return ActivityKind.Document;
        }

        if (DevelopmentProcesses.Contains(processName))
        {
            return ActivityKind.Development;
        }

        return ActivityKind.App;
    }
}
