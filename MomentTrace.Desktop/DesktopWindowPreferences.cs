using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using MomentTrace.Core;

namespace MomentTrace.Desktop;

public sealed class DesktopWindowPreferences
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    public static readonly string[] BuiltInAdultDomains =
    [
        "pornhub.com",
        "xvideos.com",
        "xnxx.com",
        "xhamster.com",
        "redtube.com",
        "youporn.com",
        "tube8.com",
        "spankbang.com",
        "beeg.com",
        "eporner.com"
    ];

    private string[] _adultBlocklistDomains = [];

    public double Opacity { get; set; } = 1.0;

    public string TimelineViewMode { get; set; } = "Cards";

    public string Theme { get; set; } = "Midnight";

    public bool ExcludeAdultWebsites { get; set; } = true;

    public string[] AdultBlocklistDomains
    {
        get => _adultBlocklistDomains.Length == 0 ? BuiltInAdultDomains : _adultBlocklistDomains;
        set => _adultBlocklistDomains = value ?? [];
    }

    public string[] CustomBlockedDomains { get; set; } = [];

    public string[] CustomAllowedDomains { get; set; } = [];

    public string[] CustomBlockedApps { get; set; } = [];

    public DateTimeOffset? AdultBlocklistUpdatedAt { get; set; }

    public static string PrivacyBlocklistDirectory =>
        MomentTracePaths.PrivacyBlocklistsDirectory;

    public static string AdultBlocklistCachePath => Path.Combine(PrivacyBlocklistDirectory, "adult-domains.txt");

    public static string PreferencesPath
    {
        get
        {
            var preferencesPath = Path.Combine(MomentTracePaths.AppDataDirectory, "settings.json");
            var legacyPreferencesPath = Path.Combine(MomentTracePaths.LegacyAppDataDirectory, "settings.json");

            if (!File.Exists(preferencesPath) && File.Exists(legacyPreferencesPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(preferencesPath)!);
                File.Copy(legacyPreferencesPath, preferencesPath);
            }

            return preferencesPath;
        }
    }

    public static DesktopWindowPreferences Load()
    {
        try
        {
            if (!File.Exists(PreferencesPath))
            {
                return new DesktopWindowPreferences();
            }

            var json = File.ReadAllText(PreferencesPath);
            return JsonSerializer.Deserialize<DesktopWindowPreferences>(json) ?? new DesktopWindowPreferences();
        }
        catch
        {
            return new DesktopWindowPreferences();
        }
    }

    public void Save()
    {
        SaveJson(PreferencesPath, JsonSerializer.Serialize(this, SerializerOptions));
    }

    private static void SaveJson(string path, string json)
    {
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var tempPath = Path.Combine(directory, $"settings.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp");

            try
            {
                File.WriteAllText(tempPath, json);

                if (File.Exists(path))
                {
                    File.Replace(tempPath, path, null);
                }
                else
                {
                    File.Move(tempPath, path);
                }

                return;
            }
            catch (IOException) when (attempt < 4)
            {
                TryDelete(tempPath);
                Thread.Sleep(40 * (attempt + 1));
            }
            catch (UnauthorizedAccessException) when (attempt < 4)
            {
                TryDelete(tempPath);
                Thread.Sleep(40 * (attempt + 1));
            }
            catch
            {
                TryDelete(tempPath);
                throw;
            }
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // A leftover temp file is safer than crashing while saving preferences.
        }
    }

    public PrivacyFilterOptions ToPrivacyFilterOptions()
    {
        return new PrivacyFilterOptions
        {
            ExcludeAdultWebsites = ExcludeAdultWebsites,
            AdultDomains = AdultBlocklistDomains,
            CustomBlockedDomains = CustomBlockedDomains,
            CustomAllowedDomains = CustomAllowedDomains,
            CustomBlockedApps = CustomBlockedApps
        };
    }
}
