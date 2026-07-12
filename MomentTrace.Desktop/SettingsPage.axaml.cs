using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using MomentTrace.Core;
using MomentTrace.Storage;

namespace MomentTrace.Desktop;

public partial class SettingsPage : UserControl
{
    private const string AdultBlocklistUrl = "https://raw.githubusercontent.com/blocklistproject/Lists/master/porn.txt";
    private static readonly TimeSpan CaptureStaleThreshold = TimeSpan.FromMinutes(2);

    private readonly IActivityStore _store;
    private readonly IActivityRecorder? _recorder;
    private readonly DesktopWindowPreferences _preferences;
    private readonly DesktopStartupRegistration _startupRegistration;
    private readonly LocalDataCleanupService _localDataCleanupService;
    private readonly ObservableCollection<PrivacyChoiceViewModel> _recentApps = [];
    private readonly ObservableCollection<PrivacyChoiceViewModel> _recentWebsites = [];
    private readonly Action<double> _opacityChanged;
    private readonly Action<string> _timelineViewModeChanged;
    private readonly Action<string> _themeChanged;
    private readonly Func<Task> _activityDataChanged;
    private bool _isUpdatingStartupCheckBox;
    private bool _isUpdatingOpacitySlider;
    private bool _isUpdatingTimelineViewMode;
    private bool _isUpdatingTheme;
    private ScrollViewer? _pendingDragScrollViewer;
    private Point _scrollDragStartPoint;
    private Vector _scrollDragStartOffset;
    private bool _isScrollDragging;
    private static readonly Cursor ScrollReadyCursor = new(StandardCursorType.Hand);
    private static readonly Cursor ScrollDraggingCursor = new(StandardCursorType.SizeAll);

    public SettingsPage(
        IActivityStore store,
        IActivityRecorder? recorder,
        DesktopWindowPreferences preferences,
        string currentTimelineViewMode,
        string currentTheme,
        Action<double> opacityChanged,
        Action<string> timelineViewModeChanged,
        Action<string> themeChanged,
        Func<Task> activityDataChanged)
    {
        _store = store;
        _recorder = recorder;
        _preferences = preferences;
        _startupRegistration = new DesktopStartupRegistration("MomentTrace.Desktop", Environment.ProcessPath ?? AppContext.BaseDirectory);
        _localDataCleanupService = LocalDataCleanupService.CreateDefault();
        _opacityChanged = opacityChanged;
        _timelineViewModeChanged = timelineViewModeChanged;
        _themeChanged = themeChanged;
        _activityDataChanged = activityDataChanged;

        InitializeComponent();
        AddHandler(InputElement.PointerWheelChangedEvent, GlobalPointerWheelChanged, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(InputElement.PointerPressedEvent, GlobalPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(InputElement.PointerMovedEvent, GlobalPointerMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(InputElement.PointerReleasedEvent, GlobalPointerReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
        SettingsScrollViewer.Cursor = ScrollReadyCursor;

        RecentAppsList.ItemsSource = _recentApps;
        RecentWebsitesList.ItemsSource = _recentWebsites;
        RecentAppsList.ItemTemplate = BuildPrivacyTemplate();
        RecentWebsitesList.ItemTemplate = BuildPrivacyTemplate();

        ThemeComboBox.ItemsSource = DesktopAppThemePalette.All;
        ThemeComboBox.SelectionChanged += ThemeComboBox_SelectionChanged;
        TimelineViewModeComboBox.SelectionChanged += TimelineViewModeComboBox_SelectionChanged;
        StartWithWindowsCheckBox.PropertyChanged += StartWithWindowsCheckBox_PropertyChanged;
        ExcludeAdultWebsitesCheckBox.PropertyChanged += ExcludeAdultWebsitesCheckBox_PropertyChanged;
        OpacitySlider.PropertyChanged += OpacitySlider_PropertyChanged;
        SavePrivacyFiltersButton.Click += SavePrivacyFiltersButton_Click;
        UpdateAdultBlocklistButton.Click += UpdateAdultBlocklistButton_Click;
        ExportActivityButton.Click += ExportActivityButton_Click;
        DeleteActivityButton.Click += DeleteActivityButton_Click;
        DeleteAllLocalDataButton.Click += DeleteAllLocalDataButton_Click;

        LoadCurrentSettings(_preferences.Opacity, currentTimelineViewMode, currentTheme);
        LoadPrivacySettings();
        LoadWhereILive();
        ShowSettingsSection("Recording");
        _ = LoadRecentPrivacyChoicesAsync();
        _ = LoadHealthStatusAsync();
    }

    public async Task OnShownAsync()
    {
        LoadPrivacySettings();
        LoadWhereILive();
        await LoadRecentPrivacyChoicesAsync();
        await LoadHealthStatusAsync();
    }

    private void SettingsSectionButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string section })
        {
            ShowSettingsSection(section);
        }
    }

    private void ShowSettingsSection(string section)
    {
        RecordingSection.IsVisible = section == "Recording";
        HealthSection.IsVisible = section == "Health";
        AppearanceSection.IsVisible = section == "Appearance";
        PrivacySection.IsVisible = section == "Privacy";
        DataSection.IsVisible = section == "Data";
        AboutSection.IsVisible = section == "About";

        SetSectionButtonState(RecordingSectionButton, section == "Recording");
        SetSectionButtonState(HealthSectionButton, section == "Health");
        SetSectionButtonState(AppearanceSectionButton, section == "Appearance");
        SetSectionButtonState(PrivacySectionButton, section == "Privacy");
        SetSectionButtonState(DataSectionButton, section == "Data");
        SetSectionButtonState(AboutSectionButton, section == "About");

        SettingsScrollViewer.Offset = new Vector(0, 0);
    }

    private static void SetSectionButtonState(Button button, bool isActive)
    {
        button.Classes.Set("active", isActive);
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
        ThemeComboBox.SelectedItem = DesktopAppThemePalette.Find(currentTheme);
        _isUpdatingTheme = false;

        _isUpdatingOpacitySlider = true;
        OpacitySlider.Value = Math.Clamp(currentOpacity, 0.0, 1.0) * 100;
        OpacityValueText.Text = $"{Math.Round(OpacitySlider.Value)}%";
        _isUpdatingOpacitySlider = false;
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
        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion
            ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
            ?? "Development";

        VersionText.Text = version;
        InstallPathText.Text = AppContext.BaseDirectory;
        AppDataPathText.Text = MomentTracePaths.AppDataDirectory;
        DatabasePathText.Text = SqliteActivityStore.DefaultDatabasePath;
        SettingsPathText.Text = DesktopWindowPreferences.PreferencesPath;
    }

    private async Task LoadHealthStatusAsync()
    {
        var screenshotsPath = MomentTracePaths.ScreenshotsDirectory;
        var iconsPath = MomentTracePaths.IconsDirectory;
        var privacyCount = _preferences.CustomBlockedApps.Length
            + _preferences.CustomBlockedDomains.Length
            + (_preferences.ExcludeAdultWebsites ? _preferences.AdultBlocklistDomains.Length : 0);

        HealthUpdatedText.Text = $"Checked {DateTime.Now:t}";
        ScreenshotsHealthText.Text = Directory.Exists(screenshotsPath)
            ? $"{CountFiles(screenshotsPath):N0} cached files"
            : "No screenshot cache yet";
        IconsHealthText.Text = Directory.Exists(iconsPath)
            ? $"{CountFiles(iconsPath):N0} cached icons"
            : "No icon cache yet";
        SettingsHealthText.Text = File.Exists(DesktopWindowPreferences.PreferencesPath)
            ? "Readable and saved locally"
            : "Using defaults until first save";
        PrivacyHealthText.Text = privacyCount == 0
            ? "No custom exclusions active"
            : $"{privacyCount:N0} exclusion rules active";

        try
        {
            var recent = await _store.QueryAsync(new TimelineQuery { Limit = 250 });
            var latestCapture = recent
                .OrderByDescending(activity => activity.EndedAt)
                .FirstOrDefault();

            RecorderHealthText.Text = BuildRecorderHealthText(latestCapture);
            BrowserUrlHealthText.Text = BuildBrowserUrlHealthText(recent);
            DatabaseHealthText.Text = File.Exists(SqliteActivityStore.DefaultDatabasePath)
                ? $"Readable · {recent.Count:N0} recent rows checked"
                : "Ready · database will be created on first write";
        }
        catch
        {
            RecorderHealthText.Text = BuildRecorderHealthText(null);
            BrowserUrlHealthText.Text = BuildBrowserUrlHealthText([]);
            DatabaseHealthText.Text = "Needs attention · database query failed";
        }
    }

    private string BuildRecorderHealthText(TimelineActivity? latestCapture)
    {
        var recordingState = _recorder?.IsRecording == true ? "Recording" : "Recorder idle";

        if (latestCapture is null)
        {
            return _recorder is null
                ? "Recorder unavailable on this platform"
                : $"{recordingState} · no captured activity yet";
        }

        var capturedAt = latestCapture.EndedAt.LocalDateTime;
        var age = DateTimeOffset.Now - latestCapture.EndedAt;
        var freshness = age <= CaptureStaleThreshold ? "fresh" : $"stale by {FormatAge(age)}";

        return $"{recordingState} · last capture {capturedAt:g} ({freshness})";
    }

    private static string FormatAge(TimeSpan age)
    {
        if (age.TotalHours >= 1)
        {
            return $"{Math.Max(1, (int)Math.Round(age.TotalHours))}h";
        }

        if (age.TotalMinutes >= 1)
        {
            return $"{Math.Max(1, (int)Math.Round(age.TotalMinutes))}m";
        }

        return $"{Math.Max(1, (int)Math.Round(age.TotalSeconds))}s";
    }

    private string BuildBrowserUrlHealthText(IReadOnlyList<TimelineActivity> recent)
    {
        if (OperatingSystem.IsWindows() && _recorder is not null)
        {
            var browserMoments = recent.Count(activity => activity.Kind == ActivityKind.Browser);
            var browserUrls = recent.Count(activity => IsWebUrl(activity.Resource));

            if (browserUrls > 0)
            {
                return $"Available via UI Automation · {browserUrls:N0} recent URL captures";
            }

            if (browserMoments > 0)
            {
                return $"Available via UI Automation · {browserMoments:N0} recent browser moments but no URL surfaced";
            }

            return "Available in this Windows build for supported browser windows";
        }

        if (OperatingSystem.IsMacOS())
        {
            return "Reduced mode on this platform · browser URL extraction is not wired here";
        }

        return "Unavailable on this platform";
    }

    private static bool IsWebUrl(string? value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && uri.Scheme is "http" or "https";
    }

    private static int CountFiles(string path)
    {
        try
        {
            return Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).Count();
        }
        catch
        {
            return 0;
        }
    }

    private void StartWithWindowsCheckBox_Changed(object? sender, RoutedEventArgs e)
    {
        if (_isUpdatingStartupCheckBox)
        {
            return;
        }

        _startupRegistration.SetEnabled(StartWithWindowsCheckBox.IsChecked == true);
    }

    private void StartWithWindowsCheckBox_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == ToggleButton.IsCheckedProperty)
        {
            StartWithWindowsCheckBox_Changed(sender, new RoutedEventArgs());
        }
    }

    private void OpacitySlider_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (_isUpdatingOpacitySlider || e.Property.Name != nameof(RangeBase.Value))
        {
            return;
        }

        OpacityValueText.Text = $"{Math.Round(OpacitySlider.Value)}%";
        _opacityChanged(Math.Clamp(OpacitySlider.Value / 100, 0.0, 1.0));
    }

    private void TimelineViewModeComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingTimelineViewMode || TimelineViewModeComboBox.SelectedItem is not ComboBoxItem item || item.Content is not string viewMode)
        {
            return;
        }

        _timelineViewModeChanged(viewMode);
    }

    private void ThemeComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingTheme || ThemeComboBox.SelectedItem is not DesktopAppThemePalette theme)
        {
            return;
        }

        _themeChanged(theme.Key);
    }

    private Window GetOwnerWindow()
    {
        return TopLevel.GetTopLevel(this) as Window
            ?? throw new InvalidOperationException("SettingsPage must be hosted in a Window.");
    }

    private void ExcludeAdultWebsitesCheckBox_Changed(object? sender, RoutedEventArgs e)
    {
        _preferences.ExcludeAdultWebsites = ExcludeAdultWebsitesCheckBox.IsChecked == true;
        _preferences.Save();
    }

    private void ExcludeAdultWebsitesCheckBox_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == ToggleButton.IsCheckedProperty)
        {
            ExcludeAdultWebsitesCheckBox_Changed(sender, new RoutedEventArgs());
        }
    }

    private async void SavePrivacyFiltersButton_Click(object? sender, RoutedEventArgs e)
    {
        await SavePrivacyFiltersAsync(reloadChoices: true);
    }

    private async Task SavePrivacyFiltersAsync(bool reloadChoices)
    {
        var checkedDomains = _recentWebsites.Where(item => item.IsBlocked).Select(item => item.Value);
        var checkedApps = _recentApps.Where(item => item.IsBlocked).Select(item => item.Value);
        var visibleDomains = _recentWebsites.Select(item => item.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var visibleApps = _recentApps.Select(item => item.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);

        _preferences.CustomBlockedDomains = ParseDomainText(CustomBlockedDomainsTextBox.Text ?? string.Empty)
            .Where(domain => !visibleDomains.Contains(domain))
            .Concat(checkedDomains)
            .Select(DomainPrivacyFilter.ParseDomainLine)
            .Where(domain => !string.IsNullOrWhiteSpace(domain))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        _preferences.CustomAllowedDomains = ParseDomainText(CustomAllowedDomainsTextBox.Text ?? string.Empty);
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
        await _activityDataChanged();
        await LoadHealthStatusAsync();
        if (reloadChoices)
        {
            await LoadRecentPrivacyChoicesAsync();
        }
    }

    private async void UpdateAdultBlocklistButton_Click(object? sender, RoutedEventArgs e)
    {
        UpdateAdultBlocklistButton.IsEnabled = false;
        AdultBlocklistStatusText.Text = "Updating local cache...";

        try
        {
            using var httpClient = new HttpClient();
            var content = await httpClient.GetStringAsync(AdultBlocklistUrl);
            var domains = content
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(DomainPrivacyFilter.ParseDomainLine)
                .Where(domain => !string.IsNullOrWhiteSpace(domain))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            Directory.CreateDirectory(DesktopWindowPreferences.PrivacyBlocklistDirectory);
            await File.WriteAllLinesAsync(DesktopWindowPreferences.AdultBlocklistCachePath, domains);

            _preferences.AdultBlocklistDomains = domains;
            _preferences.AdultBlocklistUpdatedAt = DateTimeOffset.Now;
            _preferences.Save();
            LoadPrivacySettings();
        }
        catch
        {
            AdultBlocklistStatusText.Text = "Update failed. Keeping current local list.";
        }
        finally
        {
            UpdateAdultBlocklistButton.IsEnabled = true;
        }
    }

    private async void ExportActivityButton_Click(object? sender, RoutedEventArgs e)
    {
        ExportActivityButton.IsEnabled = false;
        LocalActivityStatusText.Text = "Preparing local activity export...";

        try
        {
            var activities = await _store.QueryAsync(new TimelineQuery { Limit = 100000 });
            var exportPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                $"momenttrace-activity-{DateTime.Now:yyyyMMdd-HHmm}.json");

            var export = new ActivityExport(DateTimeOffset.Now, activities.Count, activities);
            var json = JsonSerializer.Serialize(export, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(exportPath, json);
            LocalActivityStatusText.Text = $"Exported {activities.Count:N0} activity rows to {exportPath}.";
        }
        catch
        {
            LocalActivityStatusText.Text = "Export failed. Please try again.";
        }
        finally
        {
            ExportActivityButton.IsEnabled = true;
        }
    }

    private async void DeleteActivityButton_Click(object? sender, RoutedEventArgs e)
    {
        if (!await ConfirmDeleteActivityAsync())
        {
            return;
        }

        DeleteActivityButton.IsEnabled = false;
        LocalActivityStatusText.Text = "Deleting local activity...";

        try
        {
            await _store.DeleteAllAsync();
            DeleteCachedPreviewDirectory("screenshots");
            DeleteCachedPreviewDirectory("thumbnails");
            await _activityDataChanged();
            await LoadHealthStatusAsync();
            await LoadRecentPrivacyChoicesAsync();
            LocalActivityStatusText.Text = "Local activity history deleted.";
        }
        catch
        {
            LocalActivityStatusText.Text = "Delete failed. Please try again.";
        }
        finally
        {
            DeleteActivityButton.IsEnabled = true;
        }
    }

    private async void DeleteAllLocalDataButton_Click(object? sender, RoutedEventArgs e)
    {
        if (!await ConfirmDeleteAllLocalDataAsync())
        {
            return;
        }

        DeleteAllLocalDataButton.IsEnabled = false;
        DeleteActivityButton.IsEnabled = false;
        ExportActivityButton.IsEnabled = false;
        LocalActivityStatusText.Text = "Deleting all local data and closing MomentTrace...";

        try
        {
            if (_recorder is not null && _recorder.IsRecording)
            {
                await _recorder.StopAsync();
            }

            _startupRegistration.SetEnabled(false);
            await _localDataCleanupService.DeleteAllLocalDataAsync(_store);
            LocalActivityStatusText.Text = "All local data deleted. Closing MomentTrace...";

            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown();
                return;
            }

            GetOwnerWindow().Close();
        }
        catch
        {
            LocalActivityStatusText.Text = "Full local-data delete failed. Please try again.";
            DeleteAllLocalDataButton.IsEnabled = true;
            DeleteActivityButton.IsEnabled = true;
            ExportActivityButton.IsEnabled = true;
        }
    }

    private static string[] ParseDomainText(string text)
    {
        return text
            .Split(['\r', '\n', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(DomainPrivacyFilter.ParseDomainLine)
            .Where(domain => !string.IsNullOrWhiteSpace(domain))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string[] ParseAppText(string text)
    {
        return text
            .Split(['\r', '\n', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(UserPrivacyFilter.NormalizeApp)
            .Where(app => !string.IsNullOrWhiteSpace(app))
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
        var blockedDomains = ParseDomainText(CustomBlockedDomainsTextBox.Text ?? string.Empty)
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

    private FuncDataTemplate<PrivacyChoiceViewModel> BuildPrivacyTemplate()
    {
        return new FuncDataTemplate<PrivacyChoiceViewModel>((item, nameScope) =>
        {
            var title = new TextBlock
            {
                Text = item.Title,
                Foreground = new SolidColorBrush(_currentTextColor()),
                FontSize = 13,
                FontWeight = FontWeight.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            var subtitle = new TextBlock
            {
                Text = item.Subtitle,
                Foreground = new SolidColorBrush(_currentMutedTextColor()),
                FontSize = 11,
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            var content = new StackPanel
            {
                Spacing = 2,
                Children = { title, subtitle }
            };

            var checkBox = new CheckBox
            {
                IsChecked = item.IsBlocked,
                Content = content,
                Margin = new Thickness(0, 0, 0, 8),
                Foreground = new SolidColorBrush(_currentTextColor())
            };

            var container = new Border
            {
                Background = new SolidColorBrush(_currentPanelColor()),
                BorderBrush = new SolidColorBrush(item.IsBlocked ? _currentAccentColor() : _currentStrokeColor()),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(10, 8),
                Margin = new Thickness(0, 0, 0, 8),
                Child = checkBox
            };

            checkBox.IsCheckedChanged += (sender, eventArgs) =>
            {
                item.IsBlocked = checkBox.IsChecked == true;
                container.BorderBrush = new SolidColorBrush(item.IsBlocked ? _currentAccentColor() : _currentStrokeColor());
                _ = SavePrivacyFiltersAsync(reloadChoices: false);
            };
            container.PointerReleased += (_, e) =>
            {
                if (e.InitialPressMouseButton is not MouseButton.Left)
                {
                    return;
                }

                if (e.Source is Visual sourceVisual
                    && sourceVisual.GetSelfAndVisualAncestors().Any(visual => ReferenceEquals(visual, checkBox)))
                {
                    return;
                }

                checkBox.IsChecked = checkBox.IsChecked != true;
                e.Handled = true;
            };

            return container;
        });

        Color _currentTextColor() => DesktopAppThemePalette.Find(_preferences.Theme).Text;
        Color _currentMutedTextColor() => DesktopAppThemePalette.Find(_preferences.Theme).MutedText;
        Color _currentPanelColor() => DesktopAppThemePalette.Find(_preferences.Theme).Surface;
        Color _currentStrokeColor() => DesktopAppThemePalette.Find(_preferences.Theme).Stroke;
        Color _currentAccentColor() => DesktopAppThemePalette.Find(_preferences.Theme).Accent;
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
        var appDataPath = MomentTracePaths.AppDataDirectory;
        var target = Path.GetFullPath(Path.Combine(appDataPath, directoryName));
        var root = Path.GetFullPath(appDataPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

        if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !Directory.Exists(target))
        {
            return;
        }

        Directory.Delete(target, recursive: true);
        Directory.CreateDirectory(target);
    }

    private void GlobalPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (!SettingsScrollViewer.IsVisible
            || !CanScrollVertically(SettingsScrollViewer)
            || !IsPointerInside(e, SettingsScrollViewer))
        {
            return;
        }

        ApplyWheelDelta(SettingsScrollViewer, e);
    }

    private void GlobalPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed is false)
        {
            return;
        }

        if (e.Source is Visual sourceVisual && IsScrollDragBlockedElement(sourceVisual))
        {
            return;
        }

        if (!SettingsScrollViewer.IsVisible
            || !CanScrollVertically(SettingsScrollViewer)
            || !IsPointerInside(e, SettingsScrollViewer))
        {
            return;
        }

        _pendingDragScrollViewer = SettingsScrollViewer;
        _scrollDragStartPoint = e.GetPosition(this);
        _scrollDragStartOffset = SettingsScrollViewer.Offset;
        _isScrollDragging = false;
    }

    private void GlobalPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_pendingDragScrollViewer is not { } scrollViewer)
        {
            return;
        }

        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed is false)
        {
            ClearScrollDrag(e);
            return;
        }

        var currentPoint = e.GetPosition(this);
        var delta = currentPoint - _scrollDragStartPoint;
        if (!_isScrollDragging && Math.Abs(delta.Y) < 6)
        {
            return;
        }

        if (!_isScrollDragging)
        {
            _isScrollDragging = true;
            e.Pointer.Capture(scrollViewer);
            scrollViewer.Cursor = ScrollDraggingCursor;
        }

        ApplyDragDelta(scrollViewer, delta);
        e.Handled = true;
    }

    private void GlobalPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_pendingDragScrollViewer is null)
        {
            return;
        }

        ClearScrollDrag(e);
    }

    private void ClearScrollDrag(PointerEventArgs e)
    {
        if (_pendingDragScrollViewer is { } scrollViewer)
        {
            scrollViewer.Cursor = ScrollReadyCursor;
        }

        e.Pointer.Capture(null);
        _pendingDragScrollViewer = null;
        _isScrollDragging = false;
    }

    private void ApplyDragDelta(ScrollViewer scrollViewer, Vector delta)
    {
        var maxOffset = Math.Max(0, scrollViewer.Extent.Height - scrollViewer.Viewport.Height);
        if (maxOffset <= 0)
        {
            return;
        }

        var nextY = Math.Clamp(_scrollDragStartOffset.Y - delta.Y, 0, maxOffset);
        scrollViewer.Offset = new Vector(scrollViewer.Offset.X, nextY);
    }

    private static bool IsScrollDragBlockedElement(Visual sourceVisual)
    {
        return sourceVisual.GetSelfAndVisualAncestors().Any(visual =>
            visual is TextBox
                or Button
                or CheckBox
                or ComboBox
                or Slider
                or ScrollBar
                or Thumb
                or Track);
    }

    private static bool CanScrollVertically(ScrollViewer scrollViewer)
    {
        return scrollViewer.Extent.Height > scrollViewer.Viewport.Height + 0.5;
    }

    private static bool IsPointerInside(PointerEventArgs e, Control control)
    {
        var point = e.GetPosition(control);
        return point.X >= 0
            && point.Y >= 0
            && point.X <= control.Bounds.Width
            && point.Y <= control.Bounds.Height;
    }

    private static void ApplyWheelDelta(ScrollViewer scrollViewer, PointerWheelEventArgs e)
    {
        var maxOffset = Math.Max(0, scrollViewer.Extent.Height - scrollViewer.Viewport.Height);
        if (maxOffset <= 0)
        {
            return;
        }

        var nextY = Math.Clamp(scrollViewer.Offset.Y - (e.Delta.Y * 44), 0, maxOffset);
        if (Math.Abs(nextY - scrollViewer.Offset.Y) < 0.01)
        {
            return;
        }

        scrollViewer.Offset = new Vector(scrollViewer.Offset.X, nextY);
        e.Handled = true;
    }

    private async Task<bool> ConfirmDeleteActivityAsync()
    {
        var palette = DesktopAppThemePalette.Find(_preferences.Theme);
        var dialog = new Window
        {
            Title = "Delete local activity",
            Width = 440,
            Height = 220,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            WindowDecorations = Avalonia.Controls.WindowDecorations.None,
            Background = Brushes.Transparent
        };

        dialog.Content = new Border
        {
            Background = new SolidColorBrush(palette.Base),
            BorderBrush = new SolidColorBrush(palette.Stroke),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(22),
            Child = BuildContent()
        };

        return await dialog.ShowDialog<bool>(GetOwnerWindow());

        StackPanel BuildContent()
        {
            var deleteButton = new Button
            {
                Content = "Delete",
                Background = new SolidColorBrush(palette.DangerFill),
                BorderBrush = new SolidColorBrush(palette.Danger),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(9),
                Foreground = new SolidColorBrush(palette.Text),
                Padding = new Thickness(16, 9),
                MinWidth = 86,
                HorizontalContentAlignment = HorizontalAlignment.Center
            };
            var cancelButton = new Button
            {
                Content = "Cancel",
                Background = new SolidColorBrush(palette.Toolbar),
                BorderBrush = new SolidColorBrush(palette.PanelEdgeStrong),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(9),
                Foreground = new SolidColorBrush(palette.Text),
                Padding = new Thickness(16, 9),
                MinWidth = 86,
                HorizontalContentAlignment = HorizontalAlignment.Center
            };

            deleteButton.Click += (_, _) => dialog.Close(true);
            cancelButton.Click += (_, _) => dialog.Close(false);

            return new StackPanel
            {
                Spacing = 18,
                Children =
                {
                    new TextBlock
                    {
                        Text = "Delete all local activity?",
                        Foreground = new SolidColorBrush(palette.Text),
                        FontSize = 20,
                        FontWeight = FontWeight.SemiBold
                    },
                    new TextBlock
                    {
                        Text = "This removes the local timeline and cached MomentTrace previews from this device. Your settings and privacy blocklists stay in place.",
                        Foreground = new SolidColorBrush(palette.MutedText),
                        TextWrapping = TextWrapping.Wrap,
                        LineHeight = 20
                    },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Spacing = 10,
                        Children = { cancelButton, deleteButton }
                    }
                }
            };
        }
    }

    private async Task<bool> ConfirmDeleteAllLocalDataAsync()
    {
        var palette = DesktopAppThemePalette.Find(_preferences.Theme);
        var dialog = new Window
        {
            Title = "Delete all local data",
            Width = 460,
            Height = 256,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            WindowDecorations = Avalonia.Controls.WindowDecorations.None,
            Background = Brushes.Transparent
        };

        dialog.Content = new Border
        {
            Background = new SolidColorBrush(palette.Base),
            BorderBrush = new SolidColorBrush(palette.Stroke),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(22),
            Child = BuildContent()
        };

        return await dialog.ShowDialog<bool>(GetOwnerWindow());

        StackPanel BuildContent()
        {
            var deleteButton = new Button
            {
                Content = "Delete everything",
                Background = new SolidColorBrush(palette.DangerFill),
                BorderBrush = new SolidColorBrush(palette.Danger),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(9),
                Foreground = new SolidColorBrush(palette.Text),
                Padding = new Thickness(16, 9),
                MinWidth = 132,
                HorizontalContentAlignment = HorizontalAlignment.Center
            };
            var cancelButton = new Button
            {
                Content = "Cancel",
                Background = new SolidColorBrush(palette.Toolbar),
                BorderBrush = new SolidColorBrush(palette.PanelEdgeStrong),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(9),
                Foreground = new SolidColorBrush(palette.Text),
                Padding = new Thickness(16, 9),
                MinWidth = 86,
                HorizontalContentAlignment = HorizontalAlignment.Center
            };

            deleteButton.Click += (_, _) => dialog.Close(true);
            cancelButton.Click += (_, _) => dialog.Close(false);

            return new StackPanel
            {
                Spacing = 18,
                Children =
                {
                    new TextBlock
                    {
                        Text = "Delete all local data?",
                        Foreground = new SolidColorBrush(palette.Text),
                        FontSize = 20,
                        FontWeight = FontWeight.SemiBold
                    },
                    new TextBlock
                    {
                        Text = "This removes the local timeline, screenshots, thumbnails, icons, settings, cached privacy blocklists, and turns off startup for this device. MomentTrace will close when the cleanup finishes.",
                        Foreground = new SolidColorBrush(palette.MutedText),
                        TextWrapping = TextWrapping.Wrap,
                        LineHeight = 20
                    },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Spacing = 10,
                        Children = { cancelButton, deleteButton }
                    }
                }
            };
        }
    }

    private sealed record ActivityExport(DateTimeOffset ExportedAt, int Count, IReadOnlyList<TimelineActivity> Activities);
}
