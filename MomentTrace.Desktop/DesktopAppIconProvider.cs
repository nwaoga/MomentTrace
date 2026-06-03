using MomentTrace.Presentation;
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
    private readonly Dictionary<string, string?> _cache = new(StringComparer.OrdinalIgnoreCase);

    public string? GetIcon(string? executablePath, string? appName, string? processName)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return null;
        }

        if (_cache.TryGetValue(executablePath, out var cached))
        {
            return cached;
        }

        var iconPath = OperatingSystem.IsWindows()
            ? TryGetWindowsIconPath(executablePath)
            : OperatingSystem.IsMacOS()
                ? TryGetMacIconPath(executablePath)
                : null;

        _cache[executablePath] = iconPath;
        return iconPath;
    }

    private static string? TryGetWindowsIconPath(string executablePath)
    {
        if (!File.Exists(executablePath))
        {
            return null;
        }

        try
        {
            var file = new FileInfo(executablePath);
            var cachePath = GetCachePath($"{file.FullName}|{file.LastWriteTimeUtc.Ticks}|{file.Length}", "png");
            if (File.Exists(cachePath))
            {
                return cachePath;
            }

            return RunWindowsIconExtract(executablePath, cachePath) && File.Exists(cachePath)
                ? cachePath
                : null;
        }
        catch
        {
            return null;
        }
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

    private static bool RunWindowsIconExtract(string executablePath, string cachePath)
    {
        const string script = """
            Add-Type -AssemblyName System.Drawing
            $icon = [System.Drawing.Icon]::ExtractAssociatedIcon($args[0])
            if ($null -eq $icon) { exit 1 }
            try {
                $bitmap = $icon.ToBitmap()
                try {
                    $bitmap.Save($args[1], [System.Drawing.Imaging.ImageFormat]::Png)
                }
                finally {
                    $bitmap.Dispose()
                }
            }
            finally {
                $icon.Dispose()
            }
            exit 0
            """;

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-Command");
            startInfo.ArgumentList.Add(script);
            startInfo.ArgumentList.Add(executablePath);
            startInfo.ArgumentList.Add(cachePath);

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
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MomentTrace",
            "icons");
        Directory.CreateDirectory(directory);

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(cacheKey)));
        return Path.Combine(directory, $"{hash}.{extension}");
    }
}
