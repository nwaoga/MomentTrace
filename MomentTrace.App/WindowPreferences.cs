using System.IO;
using System.Text.Json;
using MomentTrace.Core;

namespace MomentTrace.App;

public sealed class WindowPreferences
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
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MomentTrace", "privacy-blocklists");

    public static string AdultBlocklistCachePath => Path.Combine(PrivacyBlocklistDirectory, "adult-domains.txt");

    public static string PreferencesPath
    {
        get
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var preferencesPath = Path.Combine(appData, "MomentTrace", "settings.json");
            var legacyPreferencesPath = Path.Combine(appData, "NwaogaTimeline", "settings.json");

            if (!File.Exists(preferencesPath) && File.Exists(legacyPreferencesPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(preferencesPath)!);
                File.Copy(legacyPreferencesPath, preferencesPath);
            }

            return preferencesPath;
        }
    }

    public static WindowPreferences Load()
    {
        try
        {
            if (!File.Exists(PreferencesPath))
            {
                return new WindowPreferences();
            }

            var json = File.ReadAllText(PreferencesPath);
            return JsonSerializer.Deserialize<WindowPreferences>(json) ?? new WindowPreferences();
        }
        catch
        {
            return new WindowPreferences();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PreferencesPath)!);
        File.WriteAllText(PreferencesPath, JsonSerializer.Serialize(this, SerializerOptions));
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
