using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
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
    private readonly Action<string> _themeChanged;
    private readonly Action _recordingChanged;
    private readonly Func<Task> _activityDataChanged;
    private bool _isUpdatingStartupCheckBox;
    private bool _isUpdatingOpacitySlider;
    private bool _isUpdatingTimelineViewMode;
    private bool _isUpdatingTheme;

    public SettingsWindow(
        IActivityStore store,
        IActivityRecorder recorder,
        IStartupRegistration startupRegistration,
        double currentOpacity,
        string currentTimelineViewMode,
        string currentTheme,
        Action<double> opacityChanged,
        Action<string> timelineViewModeChanged,
        Action<string> themeChanged,
        Action recordingChanged,
        Func<Task> activityDataChanged)
    {
        _store = store;
        _recorder = recorder;
        _startupRegistration = startupRegistration;
        _preferences = WindowPreferences.Load();
        _opacityChanged = opacityChanged;
        _timelineViewModeChanged = timelineViewModeChanged;
        _themeChanged = themeChanged;
        _recordingChanged = recordingChanged;
        _activityDataChanged = activityDataChanged;

        InitializeComponent();
        AppThemePalette.Find(currentTheme).ApplyTo(this);
        Background = new SolidColorBrush(AppThemePalette.Find(currentTheme).Base);
        RecentAppsList.ItemsSource = _recentApps;
        RecentWebsitesList.ItemsSource = _recentWebsites;
        LoadCurrentSettings(currentOpacity, currentTimelineViewMode, currentTheme);
        LoadPrivacySettings();
        LoadWhereILive();
        _ = LoadRecentPrivacyChoicesAsync();
    }

    private void LoadCurrentSettings(double currentOpacity, string currentTimelineViewMode, string currentTheme)
    {
        _isUpdatingStartupCheckBox = true;
        StartWithWindowsCheckBox.IsChecked = _startupRegistration.IsEnabled();
        _isUpdatingStartupCheckBox = false;

        _isUpdatingTimelineViewMode = true;
        TimelineViewModeComboBox.SelectedIndex = string.Equals(currentTimelineViewMode, "List", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        _isUpdatingTimelineViewMode = false;

        _isUpdatingTheme = true;
        ThemeComboBox.ItemsSource = AppThemePalette.All;
        ThemeComboBox.DisplayMemberPath = nameof(AppThemePalette.DisplayName);
        ThemeComboBox.SelectedItem = AppThemePalette.Find(currentTheme);
        _isUpdatingTheme = false;

        _isUpdatingOpacitySlider = true;
        OpacitySlider.Value = Math.Clamp(currentOpacity, 0.0, 1.0) * 100;
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
        _opacityChanged(Math.Clamp(OpacitySlider.Value / 100, 0.0, 1.0));
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

    private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingTheme || ThemeComboBox.SelectedItem is not AppThemePalette theme)
        {
            return;
        }

        theme.ApplyTo(this);
        Background = new SolidColorBrush(theme.Base);
        _themeChanged(theme.Key);
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

    private async void ExportActivityButton_Click(object sender, RoutedEventArgs e)
    {
        ExportActivityButton.IsEnabled = false;
        LocalActivityStatusText.Text = "Preparing local activity export...";

        try
        {
            var activities = await _store.QueryAsync(new TimelineQuery { Limit = 100000 });
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Export MomentTrace activity",
                Filter = "JSON files (*.json)|*.json",
                FileName = $"momenttrace-activity-{DateTime.Now:yyyyMMdd-HHmm}.json",
                AddExtension = true,
                DefaultExt = ".json"
            };

            if (dialog.ShowDialog(this) != true)
            {
                LocalActivityStatusText.Text = "Export cancelled.";
                return;
            }

            var export = new ActivityExport(DateTimeOffset.Now, activities.Count, activities);
            var json = JsonSerializer.Serialize(export, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(dialog.FileName, json);
            LocalActivityStatusText.Text = $"Exported {activities.Count:N0} activity rows.";
        }
        catch
        {
            LocalActivityStatusText.Text = "Export failed. Please try again.";
            System.Media.SystemSounds.Exclamation.Play();
        }
        finally
        {
            ExportActivityButton.IsEnabled = true;
        }
    }

    private async void DeleteActivityButton_Click(object sender, RoutedEventArgs e)
    {
        var result = System.Windows.MessageBox.Show(
            this,
            "Delete all local timeline activity and cached MomentTrace previews from this device? Settings and privacy blocklists will be kept.",
            "Delete local activity",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        DeleteActivityButton.IsEnabled = false;
        LocalActivityStatusText.Text = "Deleting local activity...";
        var restartRecording = _recorder.IsRecording;

        try
        {
            if (restartRecording)
            {
                await _recorder.StopAsync();
            }

            await _store.DeleteAllAsync();
            DeleteCachedPreviewDirectory("screenshots");
            DeleteCachedPreviewDirectory("thumbnails");

            if (restartRecording)
            {
                await _recorder.StartAsync();
            }

            _recordingChanged();
            await _activityDataChanged();
            LocalActivityStatusText.Text = "Local activity history deleted.";
        }
        catch
        {
            LocalActivityStatusText.Text = "Delete failed. Please try again.";
            System.Media.SystemSounds.Exclamation.Play();

            if (restartRecording && !_recorder.IsRecording)
            {
                await _recorder.StartAsync();
                _recordingChanged();
            }
        }
        finally
        {
            DeleteActivityButton.IsEnabled = true;
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

    private static void DeleteCachedPreviewDirectory(string directoryName)
    {
        var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MomentTrace");
        var target = Path.GetFullPath(Path.Combine(appDataPath, directoryName));
        var root = Path.GetFullPath(appDataPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

        if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !Directory.Exists(target))
        {
            return;
        }

        Directory.Delete(target, recursive: true);
        Directory.CreateDirectory(target);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleWindowState();
            return;
        }

        if (e.ButtonState != MouseButtonState.Pressed)
        {
            return;
        }

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void TitleMinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void TitleMaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleWindowState();
    }

    private void ToggleWindowState()
    {
        WindowWorkArea.Apply(this);
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void Window_StateChanged(object sender, EventArgs e)
    {
        WindowWorkArea.Apply(this);
    }

    private void Window_LocationChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Normal)
        {
            WindowWorkArea.Apply(this);
        }
    }

    private sealed class PrivacyChoiceViewModel(string title, string subtitle, string value, bool isBlocked)
    {
        public string Title { get; } = title;

        public string Subtitle { get; } = subtitle;

        public string Value { get; } = value;

        public bool IsBlocked { get; set; } = isBlocked;
    }

    private sealed record ActivityExport(DateTimeOffset ExportedAt, int Count, IReadOnlyList<TimelineActivity> Activities);
}
