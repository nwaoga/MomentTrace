using System.Drawing;
using System.Drawing.Imaging;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace MomentTrace.Windows;

public sealed class WindowsAppIconProvider
{
    public string? GetIconPath(string? executablePath, string? appName, string? processName)
    {
        var cacheKey = BuildCacheKey(executablePath, appName, processName);
        var cachePath = GetCachePath(cacheKey, "png");
        if (File.Exists(cachePath))
        {
            return cachePath;
        }

        var packagedIcon = TryFindPackagedIcon(executablePath, appName, processName);
        if (!string.IsNullOrWhiteSpace(packagedIcon) && TrySaveImageAsPng(packagedIcon, cachePath))
        {
            return cachePath;
        }

        if (!string.IsNullOrWhiteSpace(executablePath)
            && File.Exists(executablePath)
            && TrySaveAssociatedIcon(executablePath, cachePath))
        {
            return cachePath;
        }

        return null;
    }

    private static string BuildCacheKey(string? executablePath, string? appName, string? processName)
    {
        if (!string.IsNullOrWhiteSpace(executablePath) && File.Exists(executablePath))
        {
            var file = new FileInfo(executablePath);
            return $"windows-v1|{file.FullName}|{file.LastWriteTimeUtc.Ticks}|{file.Length}|{appName}|{processName}";
        }

        return $"windows-v1|missing|{executablePath}|{appName}|{processName}";
    }

    private static string? TryFindPackagedIcon(string? executablePath, string? appName, string? processName)
    {
        foreach (var packageDirectory in CandidatePackageDirectories(executablePath, appName, processName))
        {
            var iconPath = TryFindPackageIcon(packageDirectory);
            if (!string.IsNullOrWhiteSpace(iconPath))
            {
                return iconPath;
            }
        }

        return null;
    }

    private static IEnumerable<string> CandidatePackageDirectories(string? executablePath, string? appName, string? processName)
    {
        var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var directory in CandidatePackageDirectoriesCore(executablePath, appName, processName))
        {
            if (Directory.Exists(directory) && yielded.Add(directory))
            {
                yield return directory;
            }
        }
    }

    private static IEnumerable<string> CandidatePackageDirectoriesCore(string? executablePath, string? appName, string? processName)
    {
        var windowsApps = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "WindowsApps");

        if (!string.IsNullOrWhiteSpace(executablePath)
            && executablePath.StartsWith(windowsApps, StringComparison.OrdinalIgnoreCase))
        {
            var existingPackage = TryFindPackageFromExecutablePath(executablePath, windowsApps);
            if (!string.IsNullOrWhiteSpace(existingPackage))
            {
                yield return existingPackage;
            }

            var stalePackagePrefix = TryGetPackagePrefixFromPath(executablePath, windowsApps);
            foreach (var directory in EnumeratePackageDirectoriesByPrefix(windowsApps, stalePackagePrefix))
            {
                yield return directory;
            }
        }

        foreach (var prefix in CandidatePackagePrefixes(appName, processName))
        {
            foreach (var directory in EnumeratePackageDirectoriesByPrefix(windowsApps, prefix))
            {
                yield return directory;
            }
        }
    }

    private static string? TryFindPackageFromExecutablePath(string executablePath, string windowsApps)
    {
        try
        {
            var current = new DirectoryInfo(Path.GetDirectoryName(executablePath) ?? string.Empty);
            while (current is not null && current.FullName.StartsWith(windowsApps, StringComparison.OrdinalIgnoreCase))
            {
                if (File.Exists(Path.Combine(current.FullName, "AppxManifest.xml")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }
        }
        catch
        {
        }

        return null;
    }

    private static string? TryGetPackagePrefixFromPath(string executablePath, string windowsApps)
    {
        try
        {
            var relative = Path.GetRelativePath(windowsApps, executablePath);
            var packageFolder = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
            var underscoreIndex = packageFolder.IndexOf('_', StringComparison.Ordinal);
            return underscoreIndex > 0 ? packageFolder[..underscoreIndex] : packageFolder;
        }
        catch
        {
            return null;
        }
    }

    private static IEnumerable<string> CandidatePackagePrefixes(string? appName, string? processName)
    {
        var candidates = new[]
        {
            appName,
            processName,
            KnownPackagePrefix(appName),
            KnownPackagePrefix(processName)
        };

        foreach (var candidate in candidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                yield return candidate.Trim();
            }
        }
    }

    private static string? KnownPackagePrefix(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = Normalize(value);
        return normalized switch
        {
            "codex" => "OpenAI.Codex",
            "microsoftstore" => "Microsoft.WindowsStore",
            "applicationframehost" => "Microsoft.WindowsStore",
            "snippingtool" or "snippingtoolexe" => "Microsoft.ScreenSketch",
            "whatsapp" or "whatsapproot" => "5319275A.WhatsAppDesktop",
            _ => null
        };
    }

    private static IEnumerable<string> EnumeratePackageDirectoriesByPrefix(string windowsApps, string? prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix) || !Directory.Exists(windowsApps))
        {
            yield break;
        }

        foreach (var pattern in BuildPackagePatterns(prefix))
        {
            IEnumerable<string> directories;
            try
            {
                directories = Directory.EnumerateDirectories(windowsApps, pattern);
            }
            catch
            {
                continue;
            }

            foreach (var directory in directories.OrderByDescending(Directory.GetLastWriteTimeUtc))
            {
                if (File.Exists(Path.Combine(directory, "AppxManifest.xml")))
                {
                    yield return directory;
                }
            }
        }
    }

    private static IEnumerable<string> BuildPackagePatterns(string prefix)
    {
        yield return $"{prefix}_*";

        var normalized = Normalize(prefix);
        if (!string.IsNullOrWhiteSpace(normalized))
        {
            yield return $"*{normalized}*";
        }
    }

    private static string? TryFindPackageIcon(string packageDirectory)
    {
        var manifestPath = Path.Combine(packageDirectory, "AppxManifest.xml");
        if (!File.Exists(manifestPath))
        {
            return null;
        }

        try
        {
            var document = XDocument.Load(manifestPath);
            var visual = document
                .Descendants()
                .FirstOrDefault(element => element.Name.LocalName == "VisualElements");
            if (visual is null)
            {
                return null;
            }

            var logo = AttributeValue(visual, "Square44x44Logo")
                ?? AttributeValue(visual, "Logo")
                ?? AttributeValue(visual, "Square150x150Logo");
            if (string.IsNullOrWhiteSpace(logo))
            {
                return null;
            }

            var directPath = Path.Combine(packageDirectory, logo.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(directPath))
            {
                return BestLogoVariant(directPath) ?? directPath;
            }

            return BestLogoVariant(directPath);
        }
        catch
        {
            return null;
        }
    }

    private static string? AttributeValue(XElement element, string name)
    {
        return element
            .Attributes()
            .FirstOrDefault(attribute => attribute.Name.LocalName == name)
            ?.Value;
    }

    private static string? BestLogoVariant(string directPath)
    {
        var directory = Path.GetDirectoryName(directPath);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return null;
        }

        var baseName = Path.GetFileNameWithoutExtension(directPath);
        try
        {
            return Directory.EnumerateFiles(directory)
                .Where(path => Path.GetFileNameWithoutExtension(path).StartsWith(baseName, StringComparison.OrdinalIgnoreCase))
                .Where(path => Path.GetExtension(path) is ".png" or ".jpg" or ".jpeg")
                .OrderBy(path => LogoRank(Path.GetFileName(path)))
                .ThenByDescending(path => new FileInfo(path).Length)
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private static int LogoRank(string fileName)
    {
        if (fileName.Contains("targetsize-256", StringComparison.OrdinalIgnoreCase)
            || fileName.Contains("scale-400", StringComparison.OrdinalIgnoreCase)
            || fileName.Contains("scale-200", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (fileName.Contains("targetsize-96", StringComparison.OrdinalIgnoreCase)
            || fileName.Contains("targetsize-64", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        return 2;
    }

    private static bool TrySaveImageAsPng(string sourcePath, string cachePath)
    {
        try
        {
            using var source = Image.FromFile(sourcePath);
            using var bitmap = new Bitmap(source);
            bitmap.Save(cachePath, ImageFormat.Png);
            return File.Exists(cachePath);
        }
        catch
        {
            return false;
        }
    }

    private static bool TrySaveAssociatedIcon(string executablePath, string cachePath)
    {
        try
        {
            using var icon = Icon.ExtractAssociatedIcon(executablePath);
            if (icon is null)
            {
                return false;
            }

            using var bitmap = icon.ToBitmap();
            bitmap.Save(cachePath, ImageFormat.Png);
            return File.Exists(cachePath);
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

    private static string Normalize(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString();
    }
}
