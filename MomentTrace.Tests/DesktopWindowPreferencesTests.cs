using MomentTrace.Desktop;

namespace MomentTrace.Tests;

public sealed class DesktopWindowPreferencesTests : IDisposable
{
    private readonly string _testRoot = Path.Combine(Path.GetTempPath(), "MomentTrace.Tests", Guid.NewGuid().ToString("N"));
    private readonly string _preferencesPath;
    private readonly string _legacyPreferencesPath;

    public DesktopWindowPreferencesTests()
    {
        _preferencesPath = Path.Combine(_testRoot, "MomentTrace", "settings.json");
        _legacyPreferencesPath = Path.Combine(_testRoot, "NwaogaTimeline", "settings.json");
        DesktopWindowPreferences.PreferencesPathOverride = _preferencesPath;
        DesktopWindowPreferences.LegacyPreferencesPathOverride = _legacyPreferencesPath;
    }

    [Fact]
    public void Load_ReturnsDefaults_WhenSettingsFileDoesNotExist()
    {
        var preferences = DesktopWindowPreferences.Load();

        Assert.Equal(1.0, preferences.Opacity);
        Assert.Equal("Cards", preferences.TimelineViewMode);
        Assert.Equal("Midnight", preferences.Theme);
        Assert.True(preferences.ExcludeAdultWebsites);
        Assert.False(preferences.HasCompletedFirstRunOnboarding);
        Assert.Equal(DesktopWindowPreferences.BuiltInAdultDomains, preferences.AdultBlocklistDomains);
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsPreferences()
    {
        var saved = new DesktopWindowPreferences
        {
            Opacity = 0.72,
            TimelineViewMode = "List",
            Theme = "NeroRoast",
            ExcludeAdultWebsites = false,
            HasCompletedFirstRunOnboarding = true,
            AdultBlocklistDomains = ["customadult.example"],
            CustomBlockedDomains = ["blocked.example"],
            CustomAllowedDomains = ["allowed.example"],
            CustomBlockedApps = ["codex"],
            AdultBlocklistUpdatedAt = new DateTimeOffset(2026, 7, 8, 17, 0, 0, TimeSpan.Zero)
        };

        saved.Save();
        var reloaded = DesktopWindowPreferences.Load();

        Assert.Equal(saved.Opacity, reloaded.Opacity);
        Assert.Equal(saved.TimelineViewMode, reloaded.TimelineViewMode);
        Assert.Equal(saved.Theme, reloaded.Theme);
        Assert.Equal(saved.ExcludeAdultWebsites, reloaded.ExcludeAdultWebsites);
        Assert.Equal(saved.HasCompletedFirstRunOnboarding, reloaded.HasCompletedFirstRunOnboarding);
        Assert.Equal(saved.AdultBlocklistDomains, reloaded.AdultBlocklistDomains);
        Assert.Equal(saved.CustomBlockedDomains, reloaded.CustomBlockedDomains);
        Assert.Equal(saved.CustomAllowedDomains, reloaded.CustomAllowedDomains);
        Assert.Equal(saved.CustomBlockedApps, reloaded.CustomBlockedApps);
        Assert.Equal(saved.AdultBlocklistUpdatedAt, reloaded.AdultBlocklistUpdatedAt);
    }

    [Fact]
    public void PreferencesPath_MigratesLegacySettings_WhenCurrentFileIsMissing()
    {
        var legacyDirectory = Path.GetDirectoryName(_legacyPreferencesPath)!;
        Directory.CreateDirectory(legacyDirectory);

        var legacyJson = """
        {
          "Opacity": 0.64,
          "TimelineViewMode": "List",
          "Theme": "CostaAfterglow"
        }
        """;

        File.WriteAllText(_legacyPreferencesPath, legacyJson);

        var loaded = DesktopWindowPreferences.Load();

        Assert.Equal(0.64, loaded.Opacity);
        Assert.Equal("List", loaded.TimelineViewMode);
        Assert.Equal("CostaAfterglow", loaded.Theme);
        Assert.True(File.Exists(DesktopWindowPreferences.PreferencesPath));
    }

    [Fact]
    public void ToPrivacyFilterOptions_UsesBuiltInAdultDomainsWhenCustomListIsUnset()
    {
        var preferences = new DesktopWindowPreferences
        {
            CustomBlockedDomains = ["blocked.example"],
            CustomAllowedDomains = ["allowed.example"],
            CustomBlockedApps = ["codex"]
        };

        var options = preferences.ToPrivacyFilterOptions();

        Assert.Equal(DesktopWindowPreferences.BuiltInAdultDomains, options.AdultDomains);
        Assert.Equal(preferences.CustomBlockedDomains, options.CustomBlockedDomains);
        Assert.Equal(preferences.CustomAllowedDomains, options.CustomAllowedDomains);
        Assert.Equal(preferences.CustomBlockedApps, options.CustomBlockedApps);
        Assert.True(options.ExcludeAdultWebsites);
    }

    public void Dispose()
    {
        DesktopWindowPreferences.PreferencesPathOverride = null;
        DesktopWindowPreferences.LegacyPreferencesPathOverride = null;

        try
        {
            if (Directory.Exists(_testRoot))
            {
                Directory.Delete(_testRoot, recursive: true);
            }
        }
        catch
        {
            // Test cleanup should not hide the actual assertion result.
        }
    }
}
