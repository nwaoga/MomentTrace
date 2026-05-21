using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using MomentTrace.Core;
using Process = System.Diagnostics.Process;
using ProcessStartInfo = System.Diagnostics.ProcessStartInfo;

namespace MomentTrace.App;

public partial class MainWindow : Window
{
    private readonly IActivityStore _store;
    private readonly IActivityRecorder _recorder;
    private readonly IStartupRegistration _startupRegistration;
    private readonly WindowPreferences _preferences = WindowPreferences.Load();
    private readonly IAiProvider? _aiProvider;
    private readonly ObservableCollection<ActivityItemViewModel> _activities = [];
    private readonly ObservableCollection<TimelineSessionGroupViewModel> _activityGroups = [];
    private readonly ObservableCollection<SummaryMetricViewModel> _summaryMetrics = [];
    private readonly ObservableCollection<HeatmapSegmentViewModel> _heatmapSegments = [];
    private readonly ObservableCollection<TopApplicationViewModel> _topApplications = [];
    private readonly ObservableCollection<FocusBlockViewModel> _focusBlocks = [];
    private readonly ObservableCollection<LegendItemViewModel> _overviewLegend = [];
    private readonly ObservableCollection<DateFilterOption> _dateFilters = [];
    private readonly ICollectionView _activityView;
    private string _overviewSort = "Duration";
    private bool _isUpdatingDateFilter;
    private bool _allowClose;
    private bool _isDetailVisible = true;
    private ActivityItemViewModel? _selectedDetailActivity;

    public MainWindow(IActivityStore store, IActivityRecorder recorder, IStartupRegistration startupRegistration, IAiProvider? aiProvider = null)
    {
        _store = store;
        _recorder = recorder;
        _startupRegistration = startupRegistration;
        _aiProvider = aiProvider;

        InitializeComponent();

        SearchBox.Text = string.Empty;
        _activityView = CollectionViewSource.GetDefaultView(_activities);
        _activityView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ActivityItemViewModel.DateGroup)));
        TimelineList.ItemsSource = _activityView;
        TimelineCardGroups.ItemsSource = _activityGroups;
        SummaryCards.ItemsSource = _summaryMetrics;
        ActivityHeatmap.ItemsSource = _heatmapSegments;
        TopApplicationsTable.ItemsSource = _topApplications;
        FocusTimeline.ItemsSource = _focusBlocks;
        OverviewLegend.ItemsSource = _overviewLegend;
        DateFilter.ItemsSource = _dateFilters;
        AiSummaryPanel.Visibility = _aiProvider is null ? Visibility.Collapsed : Visibility.Visible;
        InsightsButton.Visibility = _aiProvider is null ? Visibility.Collapsed : Visibility.Visible;
        ApplyTimelineViewMode(_preferences.TimelineViewMode);

        _recorder.ActivityChanged += Recorder_ActivityChanged;
        Loaded += MainWindow_Loaded;
    }

    public void AllowClose()
    {
        _allowClose = true;
        Close();
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        ApplySavedOpacity();
        ApplyCurrentUser();
        await _recorder.StartAsync();
        UpdateRecordingStatus();
        await LoadDateFiltersAsync();
        await LoadActivitiesAsync();
        await LoadOverviewAsync();
    }

    private async void Recorder_ActivityChanged(object? sender, TimelineActivity e)
    {
        await Dispatcher.InvokeAsync(async () =>
        {
            await LoadActivitiesAsync();
            await LoadOverviewAsync();
        });
    }

    private async Task LoadActivitiesAsync()
    {
        var selectedDate = GetSelectedDate();
        var query = new TimelineQuery
        {
            SearchText = SearchBox.Text,
            Kind = GetSelectedKind(),
            Limit = 1000
        };
        var privacyFilter = CurrentPrivacyFilter();

        var activities = (await _store.QueryAsync(query))
            .Where(activity => !ActivityExclusionFilter.IsExcluded(activity))
            .Where(activity => !privacyFilter.ShouldExclude(activity))
            .Where(activity => selectedDate is null || activity.StartedAt.LocalDateTime.Date == selectedDate.Value)
            .ToList();
        var sessions = BuildTimelineSessions(activities).ToList();
        TodayActivityCountText.Text = $"{sessions.Count} activities";
        _activities.Clear();
        _activityGroups.Clear();

        foreach (var activity in sessions)
        {
            _activities.Add(activity);
        }

        foreach (var group in BuildTimelineGroups(sessions))
        {
            _activityGroups.Add(group);
        }

        if (TimelineList.SelectedItem is null && _activities.Count > 0)
        {
            TimelineList.SelectedIndex = 0;
        }
    }

    private async Task LoadOverviewAsync()
    {
        var selectedDate = GetSelectedDate() ?? DateTime.Today;
        var privacyFilter = CurrentPrivacyFilter();
        var activities = (await _store.QueryAsync(new TimelineQuery { Limit = 1000 }))
            .Where(activity => !ActivityExclusionFilter.IsExcluded(activity))
            .Where(activity => !privacyFilter.ShouldExclude(activity))
            .ToList();
        var todayActivities = activities
            .Where(activity => activity.StartedAt.LocalDateTime.Date == selectedDate)
            .OrderBy(activity => activity.StartedAt)
            .ToList();

        var totalActive = todayActivities.Aggregate(TimeSpan.Zero, (total, activity) => total + activity.Duration);
        var sessions = BuildTimelineSessions(todayActivities).OrderBy(activity => activity.SessionStartedAt).ToList();
        var topApps = BuildTopApplications(todayActivities, totalActive).ToList();
        var longestFocus = BuildFocusBlocks(sessions).Where(block => !block.IsGap).OrderByDescending(block => block.Duration).FirstOrDefault();

        _summaryMetrics.Clear();
        _summaryMetrics.Add(new SummaryMetricViewModel("Total Active Time", FormatDuration(totalActive), "Recorded foreground activity"));
        _summaryMetrics.Add(new SummaryMetricViewModel("Number of Sessions", sessions.Count.ToString(), "Collapsed continuous moments"));
        _summaryMetrics.Add(new SummaryMetricViewModel("Most Used App", topApps.FirstOrDefault()?.AppName ?? "No activity yet", topApps.FirstOrDefault()?.Duration ?? "Waiting for activity"));
        _summaryMetrics.Add(new SummaryMetricViewModel("Longest Focus Session", longestFocus is null ? "No focus yet" : FormatDuration(longestFocus.Duration), longestFocus?.Label ?? "Active blocks appear here"));

        _heatmapSegments.Clear();
        foreach (var segment in BuildHeatmap(todayActivities))
        {
            _heatmapSegments.Add(segment);
        }

        _topApplications.Clear();
        foreach (var app in SortTopApplications(topApps))
        {
            _topApplications.Add(app);
        }

        _focusBlocks.Clear();
        foreach (var block in BuildFocusBlocks(sessions))
        {
            _focusBlocks.Add(block);
        }

        _overviewLegend.Clear();
        _overviewLegend.Add(new LegendItemViewModel("Apps and tools", "#6E56CF"));
        _overviewLegend.Add(new LegendItemViewModel("Web browsing", "#2F80ED"));
        _overviewLegend.Add(new LegendItemViewModel("Development work", "#55B867"));
        _overviewLegend.Add(new LegendItemViewModel("Gaps or idle space", "#22304A"));

        OverviewDateText.Text = FormatDateLabel(selectedDate);
        OverviewSidebarDateText.Text = selectedDate.ToString("dddd, d MMMM yyyy");
        OverviewTotalActiveText.Text = FormatDuration(totalActive);
        OverviewSessionsText.Text = sessions.Count.ToString();
        OverviewAppsText.Text = topApps.Count.ToString();
        OverviewFocusText.Text = longestFocus is null ? "None yet" : FormatDuration(longestFocus.Duration);
    }

    private async Task LoadDateFiltersAsync()
    {
        var privacyFilter = CurrentPrivacyFilter();
        var activities = (await _store.QueryAsync(new TimelineQuery { Limit = 2000 }))
            .Where(activity => !ActivityExclusionFilter.IsExcluded(activity))
            .Where(activity => !privacyFilter.ShouldExclude(activity))
            .ToList();
        var dates = activities
            .Select(activity => activity.StartedAt.LocalDateTime.Date)
            .Append(DateTime.Today)
            .Append(DateTime.Today.AddDays(-1))
            .Distinct()
            .OrderByDescending(date => date)
            .ToList();

        _isUpdatingDateFilter = true;
        _dateFilters.Clear();
        foreach (var date in dates)
        {
            _dateFilters.Add(new DateFilterOption(date, FormatDateLabel(date)));
        }

        DateFilter.SelectedItem = _dateFilters.FirstOrDefault(option => option.Date == DateTime.Today) ?? _dateFilters.FirstOrDefault();
        _isUpdatingDateFilter = false;
    }

    private static IEnumerable<HeatmapSegmentViewModel> BuildHeatmap(IReadOnlyList<TimelineActivity> activities)
    {
        var day = DateTime.Today;
        var blockLength = TimeSpan.FromMinutes(15);
        var maxSeconds = blockLength.TotalSeconds;

        for (var index = 0; index < 96; index++)
        {
            var start = day.AddTicks(blockLength.Ticks * index);
            var end = start.Add(blockLength);
            var overlapping = activities
                .Select(activity => new
                {
                    Activity = activity,
                    Seconds = GetOverlapSeconds(activity.StartedAt.LocalDateTime, activity.EndedAt.LocalDateTime, start, end)
                })
                .Where(item => item.Seconds > 0)
                .ToList();

            var activeSeconds = overlapping.Sum(item => item.Seconds);
            var dominant = overlapping
                .GroupBy(item => item.Activity.Kind)
                .OrderByDescending(group => group.Sum(item => item.Seconds))
                .FirstOrDefault()?.Key;
            var topApp = overlapping
                .GroupBy(item => item.Activity.AppName)
                .OrderByDescending(group => group.Sum(item => item.Seconds))
                .FirstOrDefault()?.Key ?? "No activity";
            var intensity = Math.Clamp(activeSeconds / maxSeconds, 0, 1);
            var tooltip = $"{start:HH:mm} - {end:HH:mm}\n{topApp}\n{FormatDuration(TimeSpan.FromSeconds(activeSeconds))}\n{overlapping.Count} activities";

            yield return new HeatmapSegmentViewModel(GetHeatmapFill(dominant, intensity), tooltip);
        }
    }

    private static IEnumerable<TopApplicationViewModel> BuildTopApplications(IReadOnlyList<TimelineActivity> activities, TimeSpan totalActive)
    {
        return activities
            .GroupBy(activity => activity.AppName)
            .Select(group =>
            {
                var duration = group.Aggregate(TimeSpan.Zero, (total, activity) => total + activity.Duration);
                var representative = group.OrderByDescending(activity => activity.EndedAt).First();
                var percent = totalActive.TotalSeconds <= 0 ? 0 : duration.TotalSeconds / totalActive.TotalSeconds;
                return new TopApplicationViewModel(representative, duration, group.Count(), percent);
            })
            .OrderByDescending(app => app.ActiveDuration)
            .Take(8);
    }

    private IEnumerable<TopApplicationViewModel> SortTopApplications(IEnumerable<TopApplicationViewModel> apps)
    {
        return _overviewSort switch
        {
            "Sessions" => apps.OrderByDescending(app => app.SessionCount),
            "Percent" => apps.OrderByDescending(app => app.Percent),
            _ => apps.OrderByDescending(app => app.ActiveDuration)
        };
    }

    private static IReadOnlyList<FocusBlockViewModel> BuildFocusBlocks(IReadOnlyList<ActivityItemViewModel> sessions)
    {
        var blocks = new List<FocusBlockViewModel>();
        ActivityItemViewModel? previous = null;

        foreach (var session in sessions)
        {
            if (previous is not null)
            {
                var gap = session.SessionStartedAt - previous.SessionEndedAt;
                if (gap.TotalMinutes >= 5)
                {
                    blocks.Add(FocusBlockViewModel.Gap(gap));
                }
            }

            blocks.Add(FocusBlockViewModel.Active(session.AppName, session.SessionEndedAt - session.SessionStartedAt, session.Activity.Kind));
            previous = session;
        }

        return blocks.Take(26).ToList();
    }

    private static double GetOverlapSeconds(DateTime activityStart, DateTime activityEnd, DateTime blockStart, DateTime blockEnd)
    {
        var start = activityStart > blockStart ? activityStart : blockStart;
        var end = activityEnd < blockEnd ? activityEnd : blockEnd;
        return end > start ? (end - start).TotalSeconds : 0;
    }

    private static string GetHeatmapFill(MomentTrace.Core.ActivityKind? kind, double intensity)
    {
        if (intensity <= 0)
        {
            return "#142033";
        }

        return kind switch
        {
            MomentTrace.Core.ActivityKind.Browser => intensity > 0.66 ? "#2F80ED" : intensity > 0.33 ? "#2363B8" : "#1B3D72",
            MomentTrace.Core.ActivityKind.Development => intensity > 0.66 ? "#55B867" : intensity > 0.33 ? "#3E854C" : "#244E32",
            MomentTrace.Core.ActivityKind.Document => intensity > 0.66 ? "#D8A84F" : intensity > 0.33 ? "#9B793D" : "#594727",
            _ => intensity > 0.66 ? "#8B5CF6" : intensity > 0.33 ? "#6E56CF" : "#3A286F"
        };
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
        {
            return $"{(int)duration.TotalHours}h {duration.Minutes}m";
        }

        if (duration.TotalMinutes >= 1)
        {
            return $"{Math.Max(1, (int)Math.Round(duration.TotalMinutes))}m";
        }

        return duration.TotalSeconds > 0 ? $"{Math.Max(1, (int)Math.Round(duration.TotalSeconds))}s" : "0m";
    }

    private MomentTrace.Core.ActivityKind? GetSelectedKind()
    {
        if (KindFilter.SelectedItem is not ComboBoxItem item || item.Content is not string content || content == "All")
        {
            return null;
        }

        return Enum.TryParse<MomentTrace.Core.ActivityKind>(content, out var kind) ? kind : null;
    }

    private DateTime? GetSelectedDate()
    {
        return DateFilter.SelectedItem is DateFilterOption option ? option.Date : DateTime.Today;
    }

    private static string FormatDateLabel(DateTime date)
    {
        var today = DateTime.Today;
        if (date == today)
        {
            return "Today";
        }

        if (date == today.AddDays(-1))
        {
            return "Yesterday";
        }

        return date.ToString("ddd, d MMM");
    }

    private static DomainPrivacyFilter CurrentPrivacyFilter()
    {
        return new DomainPrivacyFilter(WindowPreferences.Load().ToPrivacyFilterOptions());
    }

    private static IEnumerable<ActivityItemViewModel> BuildTimelineSessions(IReadOnlyList<TimelineActivity> activities)
    {
        TimelineSessionBuilder? session = null;

        foreach (var activity in activities.OrderByDescending(activity => activity.StartedAt))
        {
            if (session is not null && session.CanInclude(activity))
            {
                session.Include(activity);
                continue;
            }

            if (session is not null)
            {
                yield return session.ToViewModel();
            }

            session = new TimelineSessionBuilder(activity);
        }

        if (session is not null)
        {
            yield return session.ToViewModel();
        }
    }

    private static IEnumerable<TimelineSessionGroupViewModel> BuildTimelineGroups(IReadOnlyList<ActivityItemViewModel> sessions)
    {
        TimelineGroupBuilder? group = null;

        foreach (var session in sessions)
        {
            if (group is not null && group.CanInclude(session))
            {
                group.Include(session);
                continue;
            }

            if (group is not null)
            {
                yield return group.ToViewModel();
            }

            group = new TimelineGroupBuilder(session);
        }

        if (group is not null)
        {
            yield return group.ToViewModel();
        }
    }

    private sealed class TimelineSessionBuilder
    {
        private readonly string _key;
        private readonly DateTime _date;
        private TimelineActivity _representative;
        private DateTimeOffset _startedAt;
        private DateTimeOffset _endedAt;
        private int _count;

        public TimelineSessionBuilder(TimelineActivity activity)
        {
            _representative = activity;
            _startedAt = activity.StartedAt;
            _endedAt = activity.EndedAt;
            _count = 1;
            _date = activity.StartedAt.LocalDateTime.Date;
            _key = GetSessionKey(activity);
        }

        public bool CanInclude(TimelineActivity activity)
        {
            return activity.StartedAt.LocalDateTime.Date == _date
                && string.Equals(GetSessionKey(activity), _key, StringComparison.OrdinalIgnoreCase);
        }

        public void Include(TimelineActivity activity)
        {
            _count++;

            if (activity.StartedAt < _startedAt)
            {
                _startedAt = activity.StartedAt;
            }

            if (activity.EndedAt > _endedAt)
            {
                _endedAt = activity.EndedAt;
            }

            if (activity.StartedAt > _representative.StartedAt)
            {
                _representative = activity;
            }
        }

        public ActivityItemViewModel ToViewModel()
        {
            return new ActivityItemViewModel(_representative, _startedAt, _endedAt, _count);
        }

        private static string GetSessionKey(TimelineActivity activity)
        {
            return string.Join(
                "|",
                activity.Kind,
                activity.ProcessName.Trim(),
                NormalizeSessionTarget(activity));
        }

        private static string NormalizeSessionTarget(TimelineActivity activity)
        {
            if (activity.Kind == MomentTrace.Core.ActivityKind.Browser && !string.IsNullOrWhiteSpace(activity.Resource))
            {
                return NormalizeBrowserResource(activity.Resource);
            }

            if (!string.IsNullOrWhiteSpace(activity.ExecutablePath))
            {
                return activity.ExecutablePath.Trim();
            }

            return activity.ProcessName.Trim();
        }

        private static string NormalizeBrowserResource(string resource)
        {
            if (!Uri.TryCreate(resource.Trim(), UriKind.Absolute, out var uri))
            {
                return resource.Trim().TrimEnd('/');
            }

            var path = uri.AbsolutePath.TrimEnd('/');
            return string.IsNullOrWhiteSpace(path)
                ? uri.Host
                : $"{uri.Host}{path}";
        }
    }

    private sealed class TimelineGroupBuilder
    {
        private static readonly TimeSpan GroupGap = TimeSpan.FromMinutes(25);
        private readonly List<ActivityItemViewModel> _activities = [];
        private readonly DateTime _date;
        private readonly MomentTrace.Core.ActivityKind _kind;
        private DateTimeOffset _startedAt;
        private DateTimeOffset _endedAt;

        public TimelineGroupBuilder(ActivityItemViewModel activity)
        {
            _date = activity.SessionStartedAt.LocalDateTime.Date;
            _kind = activity.Activity.Kind;
            _startedAt = activity.SessionStartedAt;
            _endedAt = activity.SessionEndedAt;
            _activities.Add(activity);
        }

        public bool CanInclude(ActivityItemViewModel activity)
        {
            return activity.SessionStartedAt.LocalDateTime.Date == _date
                && activity.Activity.Kind == _kind
                && _startedAt - activity.SessionEndedAt <= GroupGap
                && _activities.Count < 8;
        }

        public void Include(ActivityItemViewModel activity)
        {
            _activities.Add(activity);

            if (activity.SessionStartedAt < _startedAt)
            {
                _startedAt = activity.SessionStartedAt;
            }

            if (activity.SessionEndedAt > _endedAt)
            {
                _endedAt = activity.SessionEndedAt;
            }
        }

        public TimelineSessionGroupViewModel ToViewModel()
        {
            return new TimelineSessionGroupViewModel(_startedAt, _endedAt, _kind, _activities.ToList());
        }
    }

    private async void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        await LoadActivitiesAsync();
    }

    private async void KindFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        await LoadActivitiesAsync();
    }

    private async void DateFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || _isUpdatingDateFilter)
        {
            return;
        }

        await LoadActivitiesAsync();
        await LoadOverviewAsync();
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await LoadDateFiltersAsync();
        await LoadActivitiesAsync();
        await LoadOverviewAsync();
    }

    private async void TodayButton_Click(object sender, RoutedEventArgs e)
    {
        ShowTimelinePage();
        SearchBox.Text = string.Empty;
        KindFilter.SelectedIndex = 0;
        DateFilter.SelectedItem = _dateFilters.FirstOrDefault(option => option.Date == DateTime.Today);
        await LoadActivitiesAsync();
        await LoadOverviewAsync();
    }

    private async void TimelineButton_Click(object sender, RoutedEventArgs e)
    {
        ShowTimelinePage();
        await LoadActivitiesAsync();
    }

    private async void OverviewButton_Click(object sender, RoutedEventArgs e)
    {
        ShowOverviewPage();
        await LoadOverviewAsync();
    }

    private void OverviewSort_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: string sortKey })
        {
            return;
        }

        _overviewSort = sortKey;
        var sorted = SortTopApplications(_topApplications.ToList()).ToList();
        _topApplications.Clear();
        foreach (var app in sorted)
        {
            _topApplications.Add(app);
        }
    }

    private void ShowTimelinePage()
    {
        TimelinePage.Visibility = Visibility.Visible;
        OverviewPage.Visibility = Visibility.Collapsed;
        TimelineDetailContent.Visibility = Visibility.Visible;
        OverviewSidebarContent.Visibility = Visibility.Collapsed;
        SearchBox.Visibility = Visibility.Visible;
        KindFilter.Visibility = Visibility.Visible;
        TimelineNavHighlight.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(58, 40, 111));
        OverviewNavHighlight.Background = System.Windows.Media.Brushes.Transparent;
    }

    private void ShowOverviewPage()
    {
        TimelinePage.Visibility = Visibility.Collapsed;
        OverviewPage.Visibility = Visibility.Visible;
        TimelineDetailContent.Visibility = Visibility.Collapsed;
        OverviewSidebarContent.Visibility = Visibility.Visible;
        SearchBox.Visibility = Visibility.Collapsed;
        KindFilter.Visibility = Visibility.Collapsed;
        TimelineNavHighlight.Background = System.Windows.Media.Brushes.Transparent;
        OverviewNavHighlight.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(58, 40, 111));
    }

    private async void PinnedButton_Click(object sender, RoutedEventArgs e)
    {
        ShowTimelinePage();
        SearchBox.Text = string.Empty;
        KindFilter.SelectedIndex = 0;
        DateFilter.SelectedItem = _dateFilters.FirstOrDefault(option => option.Date == DateTime.Today);
        await LoadActivitiesAsync();

        var pinned = _activities.FirstOrDefault(activity => activity.Activity.IsPinned);
        if (pinned is not null)
        {
            TimelineList.SelectedItem = pinned;
            TimelineList.ScrollIntoView(pinned);
        }
    }

    private void UpdateRecordingStatus()
    {
        StatusText.Text = _recorder.IsRecording
            ? "Recording foreground window activity with timestamps."
            : "Recording is paused.";
    }

    private void TimelineList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TimelineList.SelectedItem is not ActivityItemViewModel selected)
        {
            ClearDetails();
            return;
        }

        ShowDetails(selected);
    }

    private void TimelineCard_Click(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && IsInsideButton(source))
        {
            return;
        }

        if (sender is FrameworkElement { DataContext: ActivityItemViewModel selected })
        {
            TimelineList.SelectedItem = null;
            ShowDetails(selected);
        }
    }

    private static bool IsInsideButton(DependencyObject source)
    {
        for (var current = source; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is System.Windows.Controls.Button)
            {
                return true;
            }
        }

        return false;
    }

    private void ClearDetails()
    {
        _selectedDetailActivity = null;
        DetailKindText.Text = "Select an activity";
        DetailTitleText.Text = "No activity selected";
        DetailAppText.Text = string.Empty;
        DetailStartedText.Text = string.Empty;
        DetailEndedText.Text = string.Empty;
        DetailDurationText.Text = string.Empty;
        DetailProcessText.Text = string.Empty;
        DetailIconImage.Source = null;
        DetailIconFallbackText.Visibility = Visibility.Visible;
        ScreenshotPreviewImage.Source = null;
        ScreenshotPreviewBorder.Visibility = Visibility.Collapsed;
    }

    private void ShowDetails(ActivityItemViewModel selected)
    {
        _selectedDetailActivity = selected;
        var activity = selected.Activity;
        DetailKindText.Text = selected.KindLabel;
        DetailTitleText.Text = activity.DisplayTitle;
        DetailAppText.Text = selected.Location;
        DetailStartedText.Text = $"{selected.SessionStartedAt.LocalDateTime:dddd, d MMM yyyy HH:mm:ss}";
        DetailEndedText.Text = $"{selected.SessionEndedAt.LocalDateTime:dddd, d MMM yyyy HH:mm:ss}";
        DetailDurationText.Text = selected.Duration;
        DetailProcessText.Text = string.IsNullOrWhiteSpace(activity.ExecutablePath)
            ? activity.ProcessName
            : $"{activity.ProcessName} · {activity.ExecutablePath}";
        DetailIconImage.Source = selected.ProgramIcon;
        DetailIconFallbackText.Text = selected.IconGlyph;
        DetailIconFallbackText.Visibility = selected.ProgramIcon is null ? Visibility.Visible : Visibility.Collapsed;
        ScreenshotPreviewImage.Source = selected.ScreenshotPreview;
        ScreenshotPreviewBorder.Visibility = selected.ScreenshotPreview is null ? Visibility.Collapsed : Visibility.Visible;
        PinButton.Content = activity.IsPinned ? "Unpin" : "Pin";
    }

    private async void PinButton_Click(object sender, RoutedEventArgs e)
    {
        var selected = _selectedDetailActivity ?? TimelineList.SelectedItem as ActivityItemViewModel;
        if (selected is null)
        {
            return;
        }

        await TogglePinnedAsync(selected);
    }

    private void OpenAppButton_Click(object sender, RoutedEventArgs e)
    {
        var selected = _selectedDetailActivity ?? TimelineList.SelectedItem as ActivityItemViewModel;
        if (selected is null)
        {
            return;
        }

        OpenActivity(selected);
    }

    private void OpenActivityButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button { Tag: ActivityItemViewModel activity })
        {
            OpenActivity(activity);
        }
    }

    private async void PinActivityButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button { Tag: ActivityItemViewModel activity })
        {
            TimelineList.SelectedItem = activity;
            await TogglePinnedAsync(activity);
        }
    }

    private async Task TogglePinnedAsync(ActivityItemViewModel activity)
    {
        await _store.SetPinnedAsync(activity.Id, !activity.Activity.IsPinned);
        await LoadActivitiesAsync();
    }

    private static void OpenActivity(ActivityItemViewModel activity)
    {
        try
        {
            var target = GetOpenTarget(activity);
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch
        {
            System.Media.SystemSounds.Exclamation.Play();
        }
    }

    private static string GetOpenTarget(ActivityItemViewModel activity)
    {
        if (IsWebUrl(activity.Resource))
        {
            return activity.Resource!;
        }

        return !string.IsNullOrWhiteSpace(activity.ExecutablePath)
            ? activity.ExecutablePath
            : activity.ProcessName;
    }

    private static bool IsWebUrl(string? value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && uri.Scheme is "http" or "https";
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void CloseWindowButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ToggleDetailsButton_Click(object sender, RoutedEventArgs e)
    {
        _isDetailVisible = !_isDetailVisible;
        DetailColumn.Width = _isDetailVisible ? new GridLength(330) : new GridLength(0);
        DetailSurface.Visibility = _isDetailVisible ? Visibility.Visible : Visibility.Collapsed;
        ToggleDetailsButton.Content = _isDetailVisible ? "▤" : "▥";
    }

    private void ApplySavedOpacity()
    {
        var opacity = Math.Clamp(_preferences.Opacity, 0.55, 1.0);
        Opacity = 1.0;
        ApplySurfaceTransparency(opacity);
    }

    private void ApplyCurrentUser()
    {
        var userName = Environment.UserName;
        var domain = Environment.UserDomainName;
        var displayName = string.IsNullOrWhiteSpace(userName) ? "Windows user" : userName;

        UserDisplayNameText.Text = displayName;
        UserAccountText.Text = string.IsNullOrWhiteSpace(domain) ? Environment.MachineName : $"{domain}\\{userName}";
        UserInitialsText.Text = GetInitials(displayName);
    }

    private static string GetInitials(string displayName)
    {
        var parts = displayName
            .Split([' ', '.', '_', '-'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(2)
            .ToArray();

        if (parts.Length == 0)
        {
            return "U";
        }

        return string.Concat(parts.Select(part => char.ToUpperInvariant(part[0])));
    }

    private void ApplySurfaceTransparency(double opacity)
    {
        var strength = Math.Clamp(opacity, 0.55, 1.0);
        var shellAlpha = (byte)Math.Round(strength * 255);
        var panelAlpha = (byte)Math.Round(Math.Clamp(strength + 0.04, 0.55, 1.0) * 255);

        WindowSurface.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(shellAlpha, 8, 17, 31));
        RailSurface.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(panelAlpha, 18, 27, 43));
        TopCommandSurface.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(panelAlpha, 13, 23, 40));
        DetailSurface.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(panelAlpha, 18, 27, 43));
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var settingsWindow = new SettingsWindow(
            _recorder,
            _startupRegistration,
            _preferences.Opacity,
            _preferences.TimelineViewMode,
            SaveOpacity,
            SaveTimelineViewMode,
            UpdateRecordingStatus)
        {
            Owner = this
        };

        settingsWindow.ShowDialog();
    }

    private void SaveOpacity(double opacity)
    {
        _preferences.Opacity = Math.Clamp(opacity, 0.55, 1.0);
        ApplySurfaceTransparency(_preferences.Opacity);
        _preferences.Save();
    }

    private void SaveTimelineViewMode(string viewMode)
    {
        _preferences.TimelineViewMode = IsCardViewMode(viewMode) ? "Cards" : "List";
        ApplyTimelineViewMode(_preferences.TimelineViewMode);
        _preferences.Save();
    }

    private void ApplyTimelineViewMode(string? viewMode)
    {
        var useCards = IsCardViewMode(viewMode);
        var itemTemplateKey = useCards ? "TimelineCardItemTemplate" : "TimelineListItemTemplate";
        var panelTemplateKey = useCards ? "TimelineCardItemsPanel" : "TimelineListItemsPanel";

        TimelineList.ItemTemplate = (DataTemplate)FindResource(itemTemplateKey);
        TimelineList.ItemsPanel = (ItemsPanelTemplate)FindResource("TimelineListItemsPanel");
        TimelineList.Visibility = useCards ? Visibility.Collapsed : Visibility.Visible;
        TimelineCardScroller.Visibility = useCards ? Visibility.Visible : Visibility.Collapsed;

        if (TimelineList.GroupStyle.Count > 0)
        {
            TimelineList.GroupStyle[0].Panel = (ItemsPanelTemplate)FindResource(panelTemplateKey);
        }
    }

    private static bool IsCardViewMode(string? viewMode)
    {
        return string.Equals(viewMode, "Cards", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record SummaryMetricViewModel(string Label, string Value, string Detail);

    private sealed record HeatmapSegmentViewModel(string Fill, string Tooltip);

    private sealed record DateFilterOption(DateTime Date, string Label);

    private sealed class TopApplicationViewModel
    {
        private readonly TimelineActivity _representative;

        public TopApplicationViewModel(TimelineActivity representative, TimeSpan activeDuration, int sessionCount, double percent)
        {
            _representative = representative;
            ActiveDuration = activeDuration;
            SessionCount = sessionCount;
            Percent = percent;
        }

        public string AppName => _representative.AppName;

        public ImageSource? ProgramIcon => new ActivityItemViewModel(_representative).ProgramIcon;

        public TimeSpan ActiveDuration { get; }

        public int SessionCount { get; }

        public double Percent { get; }

        public string Duration => FormatDuration(ActiveDuration);

        public string Sessions => SessionCount.ToString();

        public string PercentOfDay => $"{Percent:P0}";

        public string Trend => "stable";
    }

    private sealed class FocusBlockViewModel
    {
        private FocusBlockViewModel(string label, TimeSpan duration, string fill, bool isGap)
        {
            Label = label;
            Duration = duration;
            Fill = fill;
            IsGap = isGap;
        }

        public string Label { get; }

        public TimeSpan Duration { get; }

        public string Fill { get; }

        public bool IsGap { get; }

        public double Width => Math.Clamp(Duration.TotalMinutes * 1.8, 18, 96);

        public string Tooltip => $"{Label}\n{FormatDuration(Duration)}";

        public static FocusBlockViewModel Active(string appName, TimeSpan duration, MomentTrace.Core.ActivityKind kind)
        {
            var fill = kind switch
            {
                MomentTrace.Core.ActivityKind.Browser => "#2F80ED",
                MomentTrace.Core.ActivityKind.Development => "#55B867",
                MomentTrace.Core.ActivityKind.Document => "#D8A84F",
                _ => "#8B5CF6"
            };

            return new FocusBlockViewModel(appName, duration, fill, isGap: false);
        }

        public static FocusBlockViewModel Gap(TimeSpan duration)
        {
            return new FocusBlockViewModel("Gap or idle space", duration, "#22304A", isGap: true);
        }
    }

    private sealed record LegendItemViewModel(string Label, string Fill);

    private sealed class TimelineSessionGroupViewModel(
        DateTimeOffset startedAt,
        DateTimeOffset endedAt,
        MomentTrace.Core.ActivityKind kind,
        IReadOnlyList<ActivityItemViewModel> activities)
    {
        public string Name { get; } = GetName(kind, activities);

        public string Summary { get; } = GetSummary(kind, activities);

        public string TimeRange { get; } = $"{startedAt.LocalDateTime:HH:mm} - {endedAt.LocalDateTime:HH:mm}";

        public string AccentBrush { get; } = kind switch
        {
            MomentTrace.Core.ActivityKind.Browser => "#7C5CFF",
            MomentTrace.Core.ActivityKind.Development => "#71C562",
            MomentTrace.Core.ActivityKind.Document => "#E7B84E",
            _ => "#8D9BB0"
        };

        public IReadOnlyList<ActivityItemViewModel> Activities { get; } = activities;

        public int ActivityCount => Activities.Count;

        private static string GetName(MomentTrace.Core.ActivityKind kind, IReadOnlyList<ActivityItemViewModel> activities)
        {
            if (kind == MomentTrace.Core.ActivityKind.Browser)
            {
                return "Website browsing";
            }

            if (kind == MomentTrace.Core.ActivityKind.Development)
            {
                return "Development work";
            }

            if (kind == MomentTrace.Core.ActivityKind.Document)
            {
                return "Document activity";
            }

            var appName = activities.FirstOrDefault()?.AppName;
            return string.IsNullOrWhiteSpace(appName) ? "App activity" : $"{appName} activity";
        }

        private static string GetSummary(MomentTrace.Core.ActivityKind kind, IReadOnlyList<ActivityItemViewModel> activities)
        {
            var first = activities.FirstOrDefault();
            return kind switch
            {
                MomentTrace.Core.ActivityKind.Browser => "Browsed pages and websites with captured timestamps.",
                MomentTrace.Core.ActivityKind.Development => "Worked across code windows, tools and debugging sessions.",
                MomentTrace.Core.ActivityKind.Document => "Reviewed or edited documents and files.",
                _ when first is not null => $"Used {first.AppName} and related windows.",
                _ => "Recorded foreground activity with timestamps."
            };
        }
    }

    private void DragSurface_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left
            || e.OriginalSource is System.Windows.Controls.TextBox
                or System.Windows.Controls.ComboBox
                or System.Windows.Controls.Button
                or System.Windows.Controls.Slider)
        {
            return;
        }

        DragMove();
    }

    private void SectionSurface_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left
            || sender is not FrameworkElement surface
            || e.GetPosition(surface).Y > 92
            || IsInsideInteractiveControl((DependencyObject)e.OriginalSource))
        {
            return;
        }

        DragMove();
        e.Handled = true;
    }

    private static bool IsInsideInteractiveControl(DependencyObject source)
    {
        for (var current = source; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is System.Windows.Controls.Button
                or System.Windows.Controls.TextBox
                or System.Windows.Controls.ComboBox
                or System.Windows.Controls.Slider
                or System.Windows.Controls.CheckBox)
            {
                return true;
            }
        }

        return false;
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        if (_allowClose)
        {
            return;
        }

        e.Cancel = true;
        Hide();
    }
}
