namespace MomentTrace.Core;

public static class ActivityExclusionFilter
{
    private static readonly HashSet<string> ExcludedProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "momenttrace",
        "momenttrace.app",
        "momenttrace.desktop",
        "lockapp",
        "logonui",
        "winlogon"
    };

    public static bool IsExcluded(TimelineActivity activity)
    {
        return IsExcluded(activity.ProcessName, activity.AppName, activity.WindowTitle, activity.ExecutablePath);
    }

    public static bool IsExcluded(string processName, string appName, string windowTitle, string? executablePath = null)
    {
        return IsExcludedProcess(processName)
            || IsMomentTraceApp(appName)
            || IsMomentTraceExecutable(executablePath)
            || ContainsLockScreenText(appName)
            || ContainsLockScreenText(windowTitle);
    }

    private static bool IsExcludedProcess(string processName)
    {
        var normalizedProcessName = NormalizeExecutableName(processName);
        return ExcludedProcesses.Contains(normalizedProcessName);
    }

    private static bool IsMomentTraceApp(string appName)
    {
        return string.Equals(appName, "MomentTrace", StringComparison.OrdinalIgnoreCase)
            || string.Equals(appName, "MomentTrace Desktop", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMomentTraceExecutable(string? executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return false;
        }

        return IsExcludedProcess(Path.GetFileNameWithoutExtension(executablePath));
    }

    private static string NormalizeExecutableName(string value)
    {
        var trimmed = value.Trim();
        return trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? trimmed[..^4]
            : trimmed;
    }

    private static bool ContainsLockScreenText(string value)
    {
        return value.Contains("lock screen", StringComparison.OrdinalIgnoreCase)
            || value.Contains("default lockscreen", StringComparison.OrdinalIgnoreCase)
            || value.Contains("default lock screen", StringComparison.OrdinalIgnoreCase);
    }
}
