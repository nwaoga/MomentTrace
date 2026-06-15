using MomentTrace.Core;
using MomentTrace.Presentation;
#if WINDOWS
using MomentTrace.Windows;
#endif
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace MomentTrace.Desktop;

public sealed class DesktopAppIconProvider : IAppIconProvider<string>
{
#if WINDOWS
    private static readonly WindowsAppIconProvider WindowsIconProvider = new();
#endif
    private readonly Dictionary<string, string?> _cache = new(StringComparer.OrdinalIgnoreCase);

    public string? GetIcon(string? executablePath, string? appName, string? processName)
    {
        var cacheKey = $"{executablePath}|{appName}|{processName}";
        if (_cache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var iconPath = GetIconCore(executablePath, appName, processName);
        _cache[cacheKey] = iconPath;
        return iconPath;
    }

    private static string? GetIconCore(string? executablePath, string? appName, string? processName)
    {
#if WINDOWS
        return WindowsIconProvider.GetIconPath(executablePath, appName, processName);
#else
        return !string.IsNullOrWhiteSpace(executablePath) && OperatingSystem.IsMacOS()
            ? TryGetMacIconPath(executablePath)
            : null;
#endif
    }

    private static string? TryGetMacIconPath(string executablePath)
    {
        var bundlePath = ResolveMacBundlePath(executablePath);
        if (string.IsNullOrWhiteSpace(bundlePath))
        {
            return null;
        }

        var resourcesPath = Path.Combine(bundlePath, "Contents", "Resources");
        if (!Directory.Exists(resourcesPath))
        {
            return null;
        }

        var iconSourcePath = TryFindMacIconSource(bundlePath, resourcesPath);
        if (string.IsNullOrWhiteSpace(iconSourcePath) || !File.Exists(iconSourcePath))
        {
            return null;
        }

        if (Path.GetExtension(iconSourcePath).Equals(".png", StringComparison.OrdinalIgnoreCase))
        {
            return iconSourcePath;
        }

        var info = new FileInfo(iconSourcePath);
        var cachePath = GetCachePath($"{info.FullName}|{info.LastWriteTimeUtc.Ticks}|{info.Length}", "png");
        if (File.Exists(cachePath))
        {
            return cachePath;
        }

        return RunMacCommand("/usr/bin/sips", "-s", "format", "png", iconSourcePath, "--out", cachePath) && File.Exists(cachePath)
            ? cachePath
            : null;
    }

    private static string? ResolveMacBundlePath(string executablePath)
    {
        if (Directory.Exists(executablePath) && executablePath.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
        {
            return executablePath;
        }

        var current = File.Exists(executablePath)
            ? new FileInfo(executablePath).Directory
            : new DirectoryInfo(executablePath);

        while (current is not null)
        {
            if (current.Name.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return null;
    }

    private static string? TryFindMacIconSource(string bundlePath, string resourcesPath)
    {
        var plistPath = Path.Combine(bundlePath, "Contents", "Info.plist");
        var iconName = ReadMacPlistValue(plistPath, "CFBundleIconFile")
            ?? ReadMacPlistValue(plistPath, "CFBundleIconName");

        if (!string.IsNullOrWhiteSpace(iconName))
        {
            var baseName = Path.GetFileNameWithoutExtension(iconName);
            var matches = Directory.EnumerateFiles(resourcesPath)
                .Where(path => string.Equals(Path.GetFileNameWithoutExtension(path), baseName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => Path.GetExtension(path).Equals(".png", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ToList();

            if (matches.Count > 0)
            {
                return matches[0];
            }

            var directPath = Path.Combine(resourcesPath, iconName.EndsWith(".icns", StringComparison.OrdinalIgnoreCase) || iconName.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                ? iconName
                : $"{iconName}.icns");
            if (File.Exists(directPath))
            {
                return directPath;
            }
        }

        return Directory.EnumerateFiles(resourcesPath, "*.png").FirstOrDefault()
            ?? Directory.EnumerateFiles(resourcesPath, "*.icns").FirstOrDefault();
    }

    private static string? ReadMacPlistValue(string plistPath, string key)
    {
        if (!File.Exists(plistPath))
        {
            return null;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "/usr/bin/defaults",
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            startInfo.ArgumentList.Add("read");
            startInfo.ArgumentList.Add(plistPath);
            startInfo.ArgumentList.Add(key);

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return null;
            }

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode == 0 ? output.Trim() : null;
        }
        catch
        {
            return null;
        }
    }

    private static bool RunMacCommand(string fileName, params string[] arguments)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };

            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return false;
            }

            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static string GetCachePath(string cacheKey, string extension)
    {
        var directory = MomentTracePaths.IconsDirectory;
        Directory.CreateDirectory(directory);

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(cacheKey)));
        return Path.Combine(directory, $"{hash}.{extension}");
    }
}
