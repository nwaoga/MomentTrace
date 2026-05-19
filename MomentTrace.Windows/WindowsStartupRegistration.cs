using Microsoft.Win32;
using MomentTrace.Core;

namespace MomentTrace.Windows;

public sealed class WindowsStartupRegistration(string appName, string executablePath, params string[] legacyAppNames) : IStartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private readonly string _appName = appName;
    private readonly string _executablePath = executablePath;
    private readonly string[] _legacyAppNames = legacyAppNames;

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        var value = key?.GetValue(_appName) as string;
        return string.Equals(value, Command, StringComparison.OrdinalIgnoreCase);
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);

        if (enabled)
        {
            key.SetValue(_appName, Command, RegistryValueKind.String);
            DeleteLegacyValues(key);
        }
        else
        {
            key.DeleteValue(_appName, throwOnMissingValue: false);
            DeleteLegacyValues(key);
        }
    }

    private string Command => $"\"{_executablePath}\" --minimized";

    private void DeleteLegacyValues(RegistryKey key)
    {
        foreach (var legacyAppName in _legacyAppNames)
        {
            key.DeleteValue(legacyAppName, throwOnMissingValue: false);
        }
    }
}
