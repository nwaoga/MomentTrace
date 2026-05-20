using System.IO;
using System.Text.Json;

namespace MomentTrace.App;

public sealed class WindowPreferences
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    public double Opacity { get; set; } = 1.0;

    public string TimelineViewMode { get; set; } = "Cards";

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
}
