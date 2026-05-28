using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using MomentTrace.Core;
using MomentTrace.Storage;

namespace MomentTrace.App;

public partial class SettingsWindow : Window
{
    private const string AdultBlocklistUrl = "https://raw.githubusercontent.com/blocklistproject/Lists/master/porn.txt";

    private readonly IActivityStore _store;
    private readonly IActivityRecorder _recorder;
    private readonly IStartupRegistration _startupRegistration;
    private readonly WindowPreferences _preferences;
    private readonly ObservableCollection<PrivacyChoiceViewModel> _recentApps = [];
    private readonly ObservableCollection<PrivacyChoiceViewModel> _recentWebsites = [];
    private readonly Action<double> _opacityChanged;
    private readonly Action<string> _timelineViewModeChanged;
    private readonly Action _recordingChanged;
    private bool _isUpdatingStartupCheckBox;
    private bool _isUpdatingOpacitySlider;
    private bool _isUpdatingTimelineViewMode;

    public SettingsWindow(
        IActivityStore store,
        IActivityRecorder recorder,
        IStartupRegistration startupRegistration,
        double currentOpacity,
        string currentTimelineViewMode,
        Action<double> opacityChanged,
        Action<string> timelineViewModeChanged,
        Action recordingChanged)
    {
        _store = store;
        _recorder = recorder;
        _startupRegistration = startupRegistration;
        _preferences = WindowPreferences.Load();
        _opacityChanged = opacityChanged;
        _timelineViewModeChanged = timelineViewModeChanged;
        _recordingChanged = recordingChanged;

        InitializeComponent();
        RecentAppsList.ItemsSource = _recentApps;
        RecentWebsitesList.ItemsSource = _recentWebsites;
        LoadCurrentSettings(currentOpacity, currentTimelineViewMode);
        LoadPrivacySettings();
        LoadWhereILive();
        _ = LoadRecentPrivacyChoicesAsync();
    }

    private void LoadCurrentSettings(double currentOpacity, string currentTimelineViewMode)
    {
        _isUpdatingStartupCheckBox = true;
        StartWithWindowsCheckBox.IsChecked = _startupRegistration.IsEnabled();
        _isUpdatingStartupCheckBox = false;

        _isUpdatingTimelineViewMode = true;
        TimelineViewModeComboBox.SelectedIndex = string.Equals(currentTimelineViewMode, "List", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        _isUpdatingTimelineViewMode = false;

        _isUpdatingOpacitySlider = true;
        OpacitySlider.Value = Math.Clamp(currentOpacity, 0.55, 1.0) * 100;
        OpacityValueText.Text = $"{Math.Round(OpacitySlider.Value)}%";
        _isUpdatingOpacitySlider = false;

        UpdateRecordingButton();
    }

    private void LoadPrivacySettings()
    {
        ExcludeAdultWebsitesCheckBox.IsChecked = _preferences.ExcludeAdultWebsites;
        CustomBlockedDomainsTextBox.Text = string.Join(Environment.NewLine, _preferences.CustomBlockedDomains);
        CustomAllowedDomainsTextBox.Text = string.Join(Environment.NewLine, _preferences.CustomAllowedDomains);
        AdultBlocklistStatusText.Text = _preferences.AdultBlocklistUpdatedAt is null
            ? $"Built-in starter list ({_preferences.AdultBlocklistDomains.Length} domains)"
            : $"Cached list updated {_preferences.AdultBlocklistUpdatedAt.Value.LocalDateTime:g} ({_preferences.AdultBlocklistDomains.Length:N0} domains)";
    }

    private void LoadWhereILive()
    {
        var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MomentTrace");
        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion
            ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
            ?? "Development";

        VersionText.Text = version;
        InstallPathText.Text = AppContext.BaseDirectory;
        AppDataPathText.Text = appDataPath;
        DatabasePathText.Text = SqliteActivityStore.DefaultDatabasePath;
        SettingsPathText.Text = WindowPreferences.PreferencesPath;
    }

    private async void RecordingButton_Click(object sender, RoutedEventArgs e)
    {
        if (_recorder.IsRecording)
        {
            await _recorder.StopAsync();
        }
        else
        {
            await _recorder.StartAsync();
        }

        UpdateRecordingButton();
        _recordingChanged();
    }

    private void UpdateRecordingButton()
    {
        RecordingButton.Content = _recorder.IsRecording ? "Pause recording" : "Resume recording";
    }

    private void StartWithWindowsCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingStartupCheckBox)
        {
            return;
        }

        _startupRegistration.SetEnabled(StartWithWindowsCheckBox.IsChecked == true);
    }

    private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdatingOpacitySlider)
        {
            return;
        }

        OpacityValueText.Text = $"{Math.Round(OpacitySlider.Value)}%";
        _opacityChanged(Math.Clamp(OpacitySlider.Value / 100, 0.55, 1.0));
    }

    private void TimelineViewModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingTimelineViewMode
            || TimelineViewModeComboBox.SelectedItem is not ComboBoxItem item
            || item.Content is not string viewMode)
        {
            return;
        }

        _timelineViewModeChanged(viewMode);
    }

    private void ExcludeAdultWebsitesCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        _preferences.ExcludeAdultWebsites = ExcludeAdultWebsitesCheckBox.IsChecked == true;
        _preferences.Save();
    }

    private void SavePrivacyFiltersButton_Click(object sender, RoutedEventArgs e)
    {
        var checkedDomains = _recentWebsites
            .Where(item => item.IsBlocked)
            .Select(item => item.Value);
        var checkedApps = _recentApps
            .Where(item => item.IsBlocked)
            .Select(item => item.Value);
        var visibleDomains = _recentWebsites
            .Select(item => item.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var visibleApps = _recentApps
            .Select(item => item.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        _preferences.CustomBlockedDomains = ParseDomainText(CustomBlockedDomainsTextBox.Text)
            .Where(domain => !visibleDomains.Contains(domain))
            .Concat(checkedDomains)
            .Select(DomainPrivacyFilter.ParseDomainLine)
            .Where(domain => !string.IsNullOrWhiteSpace(domain))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        _preferences.CustomAllowedDomains = ParseDomainText(CustomAllowedDomainsTextBox.Text);
        _preferences.CustomBlockedApps = ParseAppText(string.Join(Environment.NewLine, _preferences.CustomBlockedApps))
            .Where(app => !visibleApps.Contains(app))
            .Concat(checkedApps)
            .Select(UserPrivacyFilter.NormalizeApp)
            .Where(app => !string.IsNullOrWhiteSpace(app))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        _preferences.ExcludeAdultWebsites = ExcludeAdultWebsitesCheckBox.IsChecked == true;
        _preferences.Save();
        LoadPrivacySettings();
        _ = LoadRecentPrivacyChoicesAsync();
    }

    private async void UpdateAdultBlocklistButton_Click(object sender, RoutedEventArgs e)
    {
        UpdateAdultBlocklistButton.IsEnabled = false;
        AdultBlocklistStatusText.Text = "Updating local cache...";

        try
        {
            using var httpClient = new HttpClient();
            var content = await httpClient.GetStringAsync(AdultBlocklistUrl);
            var domains = content
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(DomainPrivacyFilter.ParseDomainLine)
                .Where(domain => !string.IsNullOrWhiteSpace(domain))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            Directory.CreateDirectory(WindowPreferences.PrivacyBlocklistDirectory);
            await File.WriteAllLinesAsync(WindowPreferences.AdultBlocklistCachePath, domains);

            _preferences.AdultBlocklistDomains = domains;
            _preferences.AdultBlocklistUpdatedAt = DateTimeOffset.Now;
            _preferences.Save();
            LoadPrivacySettings();
        }
        catch
        {
            AdultBlocklistStatusText.Text = "Update failed. Keeping current local list.";
            System.Media.SystemSounds.Exclamation.Play();
        }
        finally
        {
            UpdateAdultBlocklistButton.IsEnabled = true;
        }
    }

    private static string[] ParseDomainText(string text)
    {
        return text
            .Split(new[] { '\r', '\n', ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(DomainPrivacyFilter.ParseDomainLine)
            .Where(domain => !string.IsNullOrWhiteSpace(domain))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private async Task LoadRecentPrivacyChoicesAsync()
    {
        IReadOnlyList<TimelineActivity> activities;
        try
        {
            activities = await _store.QueryAsync(new TimelineQuery { Limit = 1000 });
        }
        catch
        {
            RecentAppsStatusText.Text = "Unavailable";
            RecentWebsitesStatusText.Text = "Unavailable";
            return;
        }

        var blockedApps = ParseAppText(string.Join(Environment.NewLine, _preferences.CustomBlockedApps))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var blockedDomains = ParseDomainText(CustomBlockedDomainsTextBox.Text)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var recentApps = activities
            .Where(activity => !ActivityExclusionFilter.IsExcluded(activity))
            .Where(activity => !string.IsNullOrWhiteSpace(activity.ProcessName) || !string.IsNullOrWhiteSpace(activity.AppName))
            .GroupBy(activity => UserPrivacyFilter.NormalizeApp(string.IsNullOrWhiteSpace(activity.ProcessName) ? activity.AppName : activity.ProcessName))
            .Where(group => !string.IsNullOrWhiteSpace(group.Key))
            .Select(group =>
            {
                var representative = group.OrderByDescending(activity => activity.StartedAt).First();
                var title = string.IsNullOrWhiteSpace(representative.AppName) ? representative.ProcessName : representative.AppName;
                var subtitle = $"{representative.ProcessName} · {group.Count()} entries";
                return new PrivacyChoiceViewModel(title, subtitle, group.Key, blockedApps.Contains(group.Key));
            })
            .OrderBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToList();

        var recentWebsites = activities
            .Select(activity => TryGetHost(activity.Resource, out var host) ? host : string.Empty)
            .Where(host => !string.IsNullOrWhiteSpace(host))
            .GroupBy(host => host, StringComparer.OrdinalIgnoreCase)
            .Select(group => new PrivacyChoiceViewModel(group.Key, $"{group.Count()} entries", group.Key, blockedDomains.Contains(group.Key)))
            .OrderBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToList();

        _recentApps.Clear();
        foreach (var app in recentApps)
        {
            _recentApps.Add(app);
        }

        _recentWebsites.Clear();
        foreach (var website in recentWebsites)
        {
            _recentWebsites.Add(website);
        }

        RecentAppsStatusText.Text = recentApps.Count == 0 ? "No apps yet" : $"{recentApps.Count} shown";
        RecentWebsitesStatusText.Text = recentWebsites.Count == 0 ? "No sites yet" : $"{recentWebsites.Count} shown";
    }

    private static string[] ParseAppText(string text)
    {
        return text
            .Split(new[] { '\r', '\n', ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(UserPrivacyFilter.NormalizeApp)
            .Where(app => !string.IsNullOrWhiteSpace(app))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool TryGetHost(string? resource, out string host)
    {
        host = string.Empty;
        if (string.IsNullOrWhiteSpace(resource))
        {
            return false;
        }

        var candidate = resource.Trim();
        if (!candidate.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !candidate.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            candidate = $"https://{candidate}";
        }

        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri)
            || string.IsNullOrWhiteSpace(uri.Host))
        {
            return false;
        }

        host = DomainPrivacyFilter.ParseDomainLine(uri.Host);
        return !string.IsNullOrWhiteSpace(host);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private sealed class PrivacyChoiceViewModel(string title, string subtitle, string value, bool isBlocked)
    {
        public string Title { get; } = title;

        public string Subtitle { get; } = subtitle;

        public string Value { get; } = value;

        public bool IsBlocked { get; set; } = isBlocked;
    }
}
