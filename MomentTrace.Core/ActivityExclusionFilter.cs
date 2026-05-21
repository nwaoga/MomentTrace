namespace MomentTrace.Core;

public static class ActivityExclusionFilter
{
    private static readonly HashSet<string> ExcludedProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "momenttrace",
        "momenttrace.app",
        "lockapp",
        "logonui",
        "winlogon"
    };

    public static bool IsExcluded(TimelineActivity activity)
    {
        return IsExcluded(activity.ProcessName, activity.AppName, activity.WindowTitle);
    }

    public static bool IsExcluded(string processName, string appName, string windowTitle)
    {
        return ExcludedProcesses.Contains(processName)
            || ContainsLockScreenText(appName)
            || ContainsLockScreenText(windowTitle);
    }

    private static bool ContainsLockScreenText(string value)
    {
        return value.Contains("lock screen", StringComparison.OrdinalIgnoreCase)
            || value.Contains("default lockscreen", StringComparison.OrdinalIgnoreCase)
            || value.Contains("default lock screen", StringComparison.OrdinalIgnoreCase);
    }
}
