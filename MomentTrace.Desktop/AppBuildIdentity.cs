using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace MomentTrace.Desktop;

internal static class AppBuildIdentity
{
    private static readonly Assembly Assembly = typeof(AppBuildIdentity).Assembly;

    public static string ShortVersion =>
        Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? Assembly.GetName().Version?.ToString()
        ?? "Development";

    public static string FullVersion
    {
        get
        {
            var parts = new List<string> { ShortVersion };

            if (!string.IsNullOrWhiteSpace(BuildNumber))
            {
                parts.Add($"build {BuildNumber}");
            }

            if (!string.IsNullOrWhiteSpace(CommitSha))
            {
                parts.Add(ShortCommitSha(CommitSha));
            }

            return string.Join(" · ", parts);
        }
    }

    public static string? BuildNumber => GetAssemblyMetadata("BuildNumber");

    public static string? CommitSha => GetAssemblyMetadata("CommitSha");

    public static string SupportInfo(
        string installPath,
        string appDataPath,
        string databasePath,
        string settingsPath)
    {
        return string.Join(Environment.NewLine,
            "MomentTrace build info",
            $"Version: {FullVersion}",
            $"Install path: {installPath}",
            $"App data: {appDataPath}",
            $"Database: {databasePath}",
            $"Settings file: {settingsPath}");
    }

    private static string? GetAssemblyMetadata(string key)
    {
        return Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => string.Equals(attribute.Key, key, StringComparison.OrdinalIgnoreCase))
            ?.Value;
    }

    private static string ShortCommitSha(string commitSha)
    {
        var trimmed = commitSha.Trim();
        return trimmed.Length <= 7 ? trimmed : trimmed[..7];
    }
}
