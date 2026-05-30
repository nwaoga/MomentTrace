using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using MomentTrace.Core;
using MomentTrace.Presentation;
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
    private readonly IActivityClassifier _activityClassifier;
    private readonly ISessionSummarizer _sessionSummarizer;
    private readonly ISearchProvider _searchProvider;
    private readonly IInsightProvider _insightProvider;
    private readonly TimelinePresentationBuilder _timelinePresentationBuilder;
    private AppThemePalette _currentTheme;
    private readonly ObservableCollection<ActivityItemViewModel> _activities = [];
    private readonly ObservableCollection<TimelineSessionGroupViewModel> _activityGroups = [];
    private readonly ObservableCollection<SummaryMetricViewModel> _summaryMetrics = [];
    private readonly ObservableCollection<HeatmapSegmentViewModel> _heatmapSegments = [];
    private readonly ObservableCollection<TopApplicationViewModel> _topApplications = [];
    private readonly ObservableCollection<FocusBlockViewModel> _focusBlocks = [];
    private readonly ObservableCollection<LegendItemViewModel> _overviewLegend = [];
    private readonly ObservableCollection<DateFilterOption> _dateFilters = [];
    private readonly ObservableCollection<DetailActivityRowViewModel> _detailActivityRows = [];
    private readonly ObservableCollection<RelatedMomentViewModel> _relatedMomentRows = [];
    private readonly DispatcherTimer _dateRolloverTimer;
    private readonly ICollectionView _activityView;
    private string _overviewSort = "Duration";
    private TimelineSemanticFilter? _selectedSemanticFilter;
    private TimelineSourceFilter? _selectedSourceFilter;
    private TimelineLibraryFilter? _selectedLibraryFilter;
    private bool _isUpdatingDateFilter;
    private bool _allowClose;
    private bool _isDetailVisible = true;
    private bool _isRefreshingForClockChange;
    private DateTime _lastObservedDate = DateTime.Today;
    private DetailTab _selectedDetailTab = DetailTab.Overview;
    private ActivityItemViewModel? _selectedDetailActivity;

    public MainWindow(
        IActivityStore store,
        IActivityRecorder recorder,
        IStartupRegistration startupRegistration,
        IAiProvider? aiProvider = null,
        IActivityClassifier? activityClassifier = null,
        ISessionSummarizer? sessionSummarizer = null,
        ISearchProvider? searchProvider = null,
        IInsightProvider? insightProvider = null)
    {
        _store = store;
        _recorder = recorder;
        _startupRegistration = startupRegistration;
        _aiProvider = aiProvider;
        _activityClassifier = activityClassifier ?? RuleBasedActivityClassifier.Default;
        _sessionSummarizer = sessionSummarizer ?? RuleBasedSessionSummarizer.Default;
        _searchProvider = searchProvider ?? LocalMetadataSearchProvider.Default;
        _insightProvider = insightProvider ?? LocalInsightProvider.Default;
        _timelinePresentationBuilder = new TimelinePresentationBuilder(_activityClassifier, _sessionSummarizer);
        _currentTheme = AppThemePalette.Find(_preferences.Theme);

        InitializeComponent();
        ApplyTheme(_preferences.Theme);

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
        DetailActivityRows.ItemsSource = _detailActivityRows;
        RelatedMomentRows.ItemsSource = _relatedMomentRows;
        AiSummaryPanel.Visibility = _aiProvider is null ? Visibility.Collapsed : Visibility.Visible;
        InsightsButton.Visibility = _aiProvider is null ? Visibility.Collapsed : Visibility.Visible;
        ApplyTimelineViewMode(_preferences.TimelineViewMode);

        _dateRolloverTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(1)
        };
        _dateRolloverTimer.Tick += DateRolloverTimer_Tick;
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
        _lastObservedDate = DateTime.Today;
        _dateRolloverTimer.Start();
        SystemEvents.PowerModeChanged += SystemEvents_PowerModeChanged;
    }

    private async void Recorder_ActivityChanged(object? sender, TimelineActivity e)
    {
        await Dispatcher.InvokeAsync(async () =>
        {
            await RefreshForClockChangeAsync();
            await LoadActivitiesAsync();
            await LoadOverviewAsync();
        });
    }

    private async void DateRolloverTimer_Tick(object? sender, EventArgs e)
    {
        await RefreshForClockChangeAsync();
    }

    private async void SystemEvents_PowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode != PowerModes.Resume)
        {
            return;
        }

        await Dispatcher.InvokeAsync(async () => await RefreshForClockChangeAsync(force: true));
    }

    private async Task RefreshForClockChangeAsync(bool force = false)
    {
        var currentDate = DateTime.Today;
        if (_isRefreshingForClockChange || (!force && currentDate == _lastObservedDate))
        {
            return;
        }

        _isRefreshingForClockChange = true;
        try
        {
            _lastObservedDate = currentDate;
            await LoadDateFiltersAsync(currentDate);
            await LoadActivitiesAsync();
            await LoadOverviewAsync();
        }
        finally
        {
            _isRefreshingForClockChange = false;
        }
    }

    private async Task LoadActivitiesAsync()
    {
        var selectedDate = GetSelectedDate();
        var query = new TimelineQuery
        {
            Kind = GetSelectedKind(),
            Limit = 1000
        };
        var privacyFilter = CurrentPrivacyFilter();

        var activities = (await _store.QueryAsync(query))
            .Where(activity => !ActivityExclusionFilter.IsExcluded(activity))
            .Where(activity => !privacyFilter.ShouldExclude(activity))
            .Where(activity => _searchProvider.Matches(activity, SearchBox.Text, _activityClassifier))
            .Where(MatchesActiveTimelineFilter)
            .Where(activity => selectedDate is null || activity.StartedAt.LocalDateTime.Date == selectedDate.Value)
            .ToList();
        var sessions = _timelinePresentationBuilder.BuildSessions(activities)
            .Select(ToActivityItemViewModel)
            .ToList();
        TodayActivityCountText.Text = $"{sessions.Count} moments";
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

        UpdateTimelineEmptyState(sessions.Count);

        if (TimelineList.SelectedItem is null && _activities.Count > 0)
        {
            TimelineList.SelectedIndex = 0;
        }
    }

    private void UpdateTimelineEmptyState(int visibleSessionCount)
    {
        TimelineEmptyState.Visibility = visibleSessionCount == 0 ? Visibility.Visible : Visibility.Collapsed;

        if (visibleSessionCount > 0)
        {
            return;
        }

        var filterLabel = GetActiveTimelineFilterLabel();
        TimelineEmptyTitleText.Text = filterLabel is null
            ? "No moments found"
            : $"No {filterLabel.ToLowerInvariant()} yet";
        TimelineEmptyBodyText.Text = _selectedLibraryFilter switch
        {
            TimelineLibraryFilter.SavedMoments => "Pinned, bookmarked, or noted moments will appear here.",
            TimelineLibraryFilter.Notes => "Add a personal note to a moment and it will appear here.",
            TimelineLibraryFilter.Bookmarks => "Bookmark any moment to save it as a reusable Library reference.",
            _ when !string.IsNullOrWhiteSpace(SearchBox.Text) => "Try a different search, date, or activity filter.",
            _ => "MomentTrace will show captured moments here once there is activity for the selected date."
        };
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
        var sessions = _timelinePresentationBuilder.BuildSessions(todayActivities)
            .Select(ToActivityItemViewModel)
            .OrderBy(activity => activity.SessionStartedAt)
            .ToList();
        var topApps = BuildTopApplications(todayActivities, totalActive).ToList();
        var longestFocus = BuildFocusBlocks(sessions).Where(block => !block.IsGap).OrderByDescending(block => block.Duration).FirstOrDefault();

        _summaryMetrics.Clear();
        _summaryMetrics.Add(new SummaryMetricViewModel("Remembered Span", FormatDuration(totalActive), "Foreground time captured locally"));
        _summaryMetrics.Add(new SummaryMetricViewModel("Moments", sessions.Count.ToString(), "Grouped from related activity"));
        _summaryMetrics.Add(new SummaryMetricViewModel("Most Seen App", topApps.FirstOrDefault()?.AppName ?? "No activity yet", topApps.FirstOrDefault()?.Duration ?? "Waiting for activity"));
        _summaryMetrics.Add(new SummaryMetricViewModel("Longest Visible Run", longestFocus is null ? "No run yet" : FormatDuration(longestFocus.Duration), longestFocus?.Label ?? "Visible blocks appear here"));

        _heatmapSegments.Clear();
        foreach (var segment in BuildHeatmap(todayActivities, selectedDate))
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
        _overviewLegend.Add(new LegendItemViewModel("Apps and tools", ActivityPalette.Apps));
        _overviewLegend.Add(new LegendItemViewModel("Web browsing", ActivityPalette.Web));
        _overviewLegend.Add(new LegendItemViewModel("Development work", ActivityPalette.Development));
        _overviewLegend.Add(new LegendItemViewModel("Documents", ActivityPalette.Documents));
        _overviewLegend.Add(new LegendItemViewModel("Gaps or idle space", ActivityPalette.Gap));

        OverviewDateText.Text = FormatDateLabel(selectedDate);
        OverviewSidebarDateText.Text = selectedDate.ToString("dddd, d MMMM yyyy");
        OverviewTotalActiveText.Text = FormatDuration(totalActive);
        OverviewSessionsText.Text = sessions.Count.ToString();
        OverviewAppsText.Text = topApps.Count.ToString();
        OverviewFocusText.Text = longestFocus is null ? "None yet" : FormatDuration(longestFocus.Duration);
    }

    private async Task LoadDateFiltersAsync(DateTime? selectedDate = null)
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

        var dateToSelect = selectedDate ?? DateTime.Today;
        DateFilter.SelectedItem = _dateFilters.FirstOrDefault(option => option.Date == dateToSelect) ?? _dateFilters.FirstOrDefault();
        _isUpdatingDateFilter = false;
    }

    private static IEnumerable<HeatmapSegmentViewModel> BuildHeatmap(IReadOnlyList<TimelineActivity> activities, DateTime day)
    {
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
            var tooltip = $"{start:HH:mm} - {end:HH:mm}\n{topApp}\nSeen {FormatDuration(TimeSpan.FromSeconds(activeSeconds))}\n{overlapping.Count} captures";

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
            return ActivityPalette.Empty;
        }

        return kind switch
        {
            MomentTrace.Core.ActivityKind.Browser => ActivityPalette.Scale(ActivityPalette.Web, intensity),
            MomentTrace.Core.ActivityKind.Development => ActivityPalette.Scale(ActivityPalette.Development, intensity),
            MomentTrace.Core.ActivityKind.Document => ActivityPalette.Scale(ActivityPalette.Documents, intensity),
            MomentTrace.Core.ActivityKind.System => ActivityPalette.Scale(ActivityPalette.SystemActivity, intensity),
            _ => ActivityPalette.Scale(ActivityPalette.Apps, intensity)
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

    private bool MatchesActiveTimelineFilter(TimelineActivity activity)
    {
        if (_selectedSemanticFilter is not null && !MatchesSemanticFilter(activity, _selectedSemanticFilter.Value))
        {
            return false;
        }

        if (_selectedSourceFilter is not null && !MatchesSourceFilter(activity, _selectedSourceFilter.Value))
        {
            return false;
        }

        return _selectedLibraryFilter is null || MatchesLibraryFilter(activity, _selectedLibraryFilter.Value);
    }

    private bool MatchesSemanticFilter(TimelineActivity activity, TimelineSemanticFilter filter)
    {
        var category = _activityClassifier.Classify(activity).Category;
        return filter switch
        {
            TimelineSemanticFilter.Browsing => category is ActivityCategory.Browsing or ActivityCategory.Research,
            TimelineSemanticFilter.Development => category == ActivityCategory.Development,
            TimelineSemanticFilter.Communication => category == ActivityCategory.Communication,
            TimelineSemanticFilter.Documents => category == ActivityCategory.Documents,
            _ => true
        };
    }

    private static bool MatchesSourceFilter(TimelineActivity activity, TimelineSourceFilter filter)
    {
        return filter switch
        {
            TimelineSourceFilter.Apps => activity.Kind != MomentTrace.Core.ActivityKind.Browser
                && activity.Kind != MomentTrace.Core.ActivityKind.Document,
            TimelineSourceFilter.Websites => activity.Kind == MomentTrace.Core.ActivityKind.Browser
                || IsWebUrl(activity.Resource),
            TimelineSourceFilter.Files => activity.Kind == MomentTrace.Core.ActivityKind.Document
                || LooksLikeFileActivity(activity),
            _ => true
        };
    }

    private static bool LooksLikeFileActivity(TimelineActivity activity)
    {
        var value = $"{activity.WindowTitle} {activity.Resource} {activity.ExecutablePath}";
        return value.Contains(".pdf", StringComparison.OrdinalIgnoreCase)
            || value.Contains(".doc", StringComparison.OrdinalIgnoreCase)
            || value.Contains(".xls", StringComparison.OrdinalIgnoreCase)
            || value.Contains(".ppt", StringComparison.OrdinalIgnoreCase)
            || value.Contains(".txt", StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesLibraryFilter(TimelineActivity activity, TimelineLibraryFilter filter)
    {
        return filter switch
        {
            TimelineLibraryFilter.SavedMoments => IsSavedMoment(activity),
            TimelineLibraryFilter.Notes => !string.IsNullOrWhiteSpace(activity.Note),
            TimelineLibraryFilter.Bookmarks => activity.IsBookmarked,
            _ => true
        };
    }

    private static bool IsSavedMoment(TimelineActivity activity)
    {
        return activity.IsPinned
            || activity.IsBookmarked
            || !string.IsNullOrWhiteSpace(activity.Note);
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

    private static UserPrivacyFilter CurrentPrivacyFilter()
    {
        return new UserPrivacyFilter(WindowPreferences.Load().ToPrivacyFilterOptions());
    }

    private IEnumerable<TimelineSessionGroupViewModel> BuildTimelineGroups(IReadOnlyList<ActivityItemViewModel> sessions)
    {
        foreach (var group in _timelinePresentationBuilder.BuildGroups(sessions))
        {
            yield return new TimelineSessionGroupViewModel(group);
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
        ClearTimelineRailFilters();
        SearchBox.Text = string.Empty;
        KindFilter.SelectedIndex = 0;
        DateFilter.SelectedItem = _dateFilters.FirstOrDefault(option => option.Date == DateTime.Today);
        await LoadActivitiesAsync();
        await LoadOverviewAsync();
    }

    private async void TimelineButton_Click(object sender, RoutedEventArgs e)
    {
        ShowTimelinePage();
        ClearTimelineRailFilters();
        await LoadActivitiesAsync();
    }

    private async void TimelineSemanticFilter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string tag }
            || !Enum.TryParse<TimelineSemanticFilter>(tag, out var filter))
        {
            return;
        }

        ShowTimelinePage();
        _selectedSemanticFilter = filter;
        _selectedSourceFilter = null;
        _selectedLibraryFilter = null;
        KindFilter.SelectedIndex = 0;
        UpdateNavigationState();
        await LoadActivitiesAsync();
    }

    private async void TimelineSourceFilter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string tag }
            || !Enum.TryParse<TimelineSourceFilter>(tag, out var filter))
        {
            return;
        }

        ShowTimelinePage();
        _selectedSemanticFilter = null;
        _selectedSourceFilter = filter;
        _selectedLibraryFilter = null;
        KindFilter.SelectedIndex = 0;
        UpdateNavigationState();
        await LoadActivitiesAsync();
    }

    private async void LibraryFilter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string tag }
            || !Enum.TryParse<TimelineLibraryFilter>(tag, out var filter))
        {
            return;
        }

        ShowTimelinePage();
        _selectedSemanticFilter = null;
        _selectedSourceFilter = null;
        _selectedLibraryFilter = filter;
        KindFilter.SelectedIndex = 0;
        UpdateNavigationState();
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
        UpdateNavigationState();
    }

    private void ShowOverviewPage()
    {
        TimelinePage.Visibility = Visibility.Collapsed;
        OverviewPage.Visibility = Visibility.Visible;
        TimelineDetailContent.Visibility = Visibility.Collapsed;
        OverviewSidebarContent.Visibility = Visibility.Visible;
        SearchBox.Visibility = Visibility.Collapsed;
        KindFilter.Visibility = Visibility.Collapsed;
        UpdateNavigationState(isOverviewActive: true);
    }

    private async void PinnedButton_Click(object sender, RoutedEventArgs e)
    {
        ShowTimelinePage();
        ClearTimelineRailFilters();
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

    private void ClearTimelineRailFilters()
    {
        _selectedSemanticFilter = null;
        _selectedSourceFilter = null;
        _selectedLibraryFilter = null;
        UpdateNavigationState();
    }

    private void UpdateNavigationState(bool isOverviewActive = false)
    {
        SetFilterHighlight(TimelineNavHighlight, !isOverviewActive && _selectedSemanticFilter is null && _selectedSourceFilter is null);
        SetFilterHighlight(OverviewNavHighlight, isOverviewActive);

        SetFilterHighlight(BrowsingNavHighlight, _selectedSemanticFilter == TimelineSemanticFilter.Browsing);
        SetFilterHighlight(DevelopmentNavHighlight, _selectedSemanticFilter == TimelineSemanticFilter.Development);
        SetFilterHighlight(CommunicationNavHighlight, _selectedSemanticFilter == TimelineSemanticFilter.Communication);
        SetFilterHighlight(DocumentsNavHighlight, _selectedSemanticFilter == TimelineSemanticFilter.Documents);
        SetFilterHighlight(AppsNavHighlight, _selectedSourceFilter == TimelineSourceFilter.Apps);
        SetFilterHighlight(WebsitesNavHighlight, _selectedSourceFilter == TimelineSourceFilter.Websites);
        SetFilterHighlight(FilesNavHighlight, _selectedSourceFilter == TimelineSourceFilter.Files);
        SetFilterHighlight(SavedMomentsNavHighlight, _selectedLibraryFilter == TimelineLibraryFilter.SavedMoments);
        SetFilterHighlight(NotesNavHighlight, _selectedLibraryFilter == TimelineLibraryFilter.Notes);
        SetFilterHighlight(BookmarksNavHighlight, _selectedLibraryFilter == TimelineLibraryFilter.Bookmarks);

        var label = GetActiveTimelineFilterLabel();
        ActiveFilterBadge.Visibility = label is null ? Visibility.Collapsed : Visibility.Visible;
        ActiveFilterText.Text = label ?? string.Empty;
    }

    private void SetFilterHighlight(Border border, bool isActive)
    {
        var accentBrush = GetThemeBrush("AccentBrush");
        var textBrush = GetThemeBrush("TextBrush");
        var accentTextBrush = GetThemeBrush("AccentTextBrush");

        border.Background = isActive
            ? accentBrush
            : System.Windows.Media.Brushes.Transparent;

        if (border.Child is System.Windows.Controls.Button button)
        {
            button.Foreground = isActive ? accentTextBrush : textBrush;
        }
    }

    private System.Windows.Media.Brush GetThemeBrush(string resourceKey)
    {
        return TryFindResource(resourceKey) as System.Windows.Media.Brush
            ?? System.Windows.Media.Brushes.Transparent;
    }

    private string? GetActiveTimelineFilterLabel()
    {
        if (_selectedSemanticFilter is not null)
        {
            return _selectedSemanticFilter.Value switch
            {
                TimelineSemanticFilter.Browsing => "Browsing moments",
                TimelineSemanticFilter.Development => "Development moments",
                TimelineSemanticFilter.Communication => "Communication moments",
                TimelineSemanticFilter.Documents => "Document moments",
                _ => null
            };
        }

        if (_selectedSourceFilter is not null)
        {
            return _selectedSourceFilter.Value switch
            {
                TimelineSourceFilter.Apps => "Source: apps",
                TimelineSourceFilter.Websites => "Source: websites",
                TimelineSourceFilter.Files => "Source: files",
                _ => null
            };
        }

        if (_selectedLibraryFilter is not null)
        {
            return _selectedLibraryFilter.Value switch
            {
                TimelineLibraryFilter.SavedMoments => "Saved moments",
                TimelineLibraryFilter.Notes => "Moments with notes",
                TimelineLibraryFilter.Bookmarks => "Bookmarked moments",
                _ => null
            };
        }

        return null;
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
        DetailNarrativeText.Text = "Select a moment to see the context around it.";
        DetailStartedText.Text = string.Empty;
        DetailEndedText.Text = string.Empty;
        DetailDurationText.Text = string.Empty;
        DetailProcessText.Text = string.Empty;
        DetailIconImage.Source = null;
        DetailIconFallbackText.Visibility = Visibility.Visible;
        ScreenshotPreviewImage.Source = null;
        ScreenshotPreviewBorder.Visibility = Visibility.Collapsed;
        MomentNoteTextBox.Text = string.Empty;
        PinButton.Content = "Pin";
        PinButton.IsEnabled = false;
        SetActionButtonActive(PinButton, false);
        BookmarkButton.Content = "Bookmark";
        BookmarkButton.IsEnabled = false;
        SetActionButtonActive(BookmarkButton, false);
        DeleteMomentButton.IsEnabled = false;
        ActivityTabSummaryText.Text = "Select a moment to inspect its raw captured entries.";
        RelatedTabSummaryText.Text = "Select a moment to see nearby activity from the same app or category.";
        _detailActivityRows.Clear();
        _relatedMomentRows.Clear();
        SelectDetailTab(DetailTab.Overview);
    }

    private void ShowDetails(ActivityItemViewModel selected)
    {
        _selectedDetailActivity = selected;
        var activity = selected.Activity;
        DetailKindText.Text = selected.KindLabel;
        DetailTitleText.Text = selected.Title;
        DetailAppText.Text = selected.Location;
        DetailNarrativeText.Text = GetDetailNarrative(selected);
        DetailStartedText.Text = $"{selected.SessionStartedAt.LocalDateTime:dddd, d MMM yyyy HH:mm:ss}";
        DetailEndedText.Text = $"{selected.SessionEndedAt.LocalDateTime:dddd, d MMM yyyy HH:mm:ss}";
        DetailDurationText.Text = selected.Duration;
        DetailProcessText.Text = selected.TechnicalSource;
        DetailIconImage.Source = selected.ProgramIcon;
        DetailIconFallbackText.Text = selected.IconGlyph;
        DetailIconFallbackText.Visibility = selected.ProgramIcon is null ? Visibility.Visible : Visibility.Collapsed;
        ScreenshotPreviewImage.Source = selected.ScreenshotPreview;
        ScreenshotPreviewBorder.Visibility = selected.ScreenshotPreview is null ? Visibility.Collapsed : Visibility.Visible;
        PinButton.Content = activity.IsPinned ? "Pinned" : "Pin";
        PinButton.IsEnabled = true;
        PinButton.ToolTip = activity.IsPinned
            ? "Pinned moments stay prominent in the Timeline. Click to unpin."
            : "Keep this moment prominent in the Timeline.";
        SetActionButtonActive(PinButton, activity.IsPinned);
        BookmarkButton.Content = activity.IsBookmarked ? "Bookmarked" : "Bookmark";
        BookmarkButton.IsEnabled = true;
        BookmarkButton.Visibility = Visibility.Visible;
        BookmarkButton.ToolTip = activity.IsBookmarked
            ? "Saved as a Library reference. Click to remove bookmark."
            : "Save this moment as a Library reference.";
        SetActionButtonActive(BookmarkButton, activity.IsBookmarked);
        MomentNoteTextBox.Text = activity.Note ?? string.Empty;
        DeleteMomentButton.IsEnabled = true;
        _ = LoadDetailContextAsync(selected);
    }

    private void SetActionButtonActive(System.Windows.Controls.Button button, bool isActive)
    {
        button.Background = isActive ? GetThemeBrush("AccentBrush") : GetThemeBrush("SubtlePanelBrush");
        button.BorderBrush = isActive ? GetThemeBrush("AccentBrush") : GetThemeBrush("BorderBrush");
        button.Foreground = isActive ? GetThemeBrush("AccentTextBrush") : GetThemeBrush("TextBrush");
    }

    private async Task LoadDetailContextAsync(ActivityItemViewModel selected)
    {
        var selectedId = selected.Id;
        var privacyFilter = CurrentPrivacyFilter();
        var activities = (await _store.QueryAsync(new TimelineQuery { Limit = 1000 }))
            .Where(activity => !ActivityExclusionFilter.IsExcluded(activity))
            .Where(activity => !privacyFilter.ShouldExclude(activity))
            .ToList();

        if (_selectedDetailActivity?.Id != selectedId)
        {
            return;
        }

        var rawEntries = activities
            .Where(activity => IsRawEntryForMoment(selected, activity))
            .OrderBy(activity => activity.StartedAt)
            .ToList();

        if (rawEntries.Count == 0)
        {
            rawEntries.Add(selected.Activity);
        }

        _detailActivityRows.Clear();
        foreach (var activity in rawEntries)
        {
            _detailActivityRows.Add(new DetailActivityRowViewModel(activity));
        }

        ActivityTabSummaryText.Text = rawEntries.Count == 1
            ? "This moment currently has one captured entry."
            : $"{rawEntries.Count} raw entries were grouped into this remembered moment.";

        var selectedCategory = _activityClassifier.Classify(selected.Activity).Category;
        var related = activities
            .Where(activity => activity.Id != selected.Id)
            .Select(activity => new
            {
                Activity = activity,
                Category = _activityClassifier.Classify(activity).Category,
                Distance = Math.Abs((activity.StartedAt - selected.SessionStartedAt).TotalSeconds)
            })
            .Where(item => string.Equals(item.Activity.AppName, selected.AppName, StringComparison.OrdinalIgnoreCase)
                || item.Category == selectedCategory
                || SameMomentTarget(item.Activity, selected.Activity))
            .OrderBy(item => item.Distance)
            .Take(6)
            .Select(item => new RelatedMomentViewModel(new ActivityItemViewModel(item.Activity), item.Category == selectedCategory ? "Same activity type" : "Same source"))
            .ToList();

        _relatedMomentRows.Clear();
        foreach (var item in related)
        {
            _relatedMomentRows.Add(item);
        }

        RelatedTabSummaryText.Text = related.Count == 0
            ? "No related moments found yet."
            : $"{related.Count} nearby moments from the same source or category.";
    }

    private static bool IsRawEntryForMoment(ActivityItemViewModel selected, TimelineActivity activity)
    {
        if (activity.Id == selected.Id)
        {
            return true;
        }

        return activity.StartedAt.LocalDateTime.Date == selected.SessionStartedAt.LocalDateTime.Date
            && activity.StartedAt >= selected.SessionStartedAt.AddMinutes(-1)
            && activity.EndedAt <= selected.SessionEndedAt.AddMinutes(1)
            && SameMomentTarget(activity, selected.Activity);
    }

    private static bool SameMomentTarget(TimelineActivity left, TimelineActivity right)
    {
        if (!string.Equals(left.ProcessName, right.ProcessName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var leftTarget = GetMomentTarget(left);
        var rightTarget = GetMomentTarget(right);
        return string.Equals(leftTarget, rightTarget, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetMomentTarget(TimelineActivity activity)
    {
        if (!string.IsNullOrWhiteSpace(activity.Resource))
        {
            return NormalizeResource(activity.Resource);
        }

        return string.IsNullOrWhiteSpace(activity.WindowTitle)
            ? activity.AppName
            : activity.WindowTitle.Trim();
    }

    private static string NormalizeResource(string resource)
    {
        if (!Uri.TryCreate(resource.Trim(), UriKind.Absolute, out var uri))
        {
            return resource.Trim().TrimEnd('/');
        }

        return $"{uri.Host}{uri.AbsolutePath.TrimEnd('/')}";
    }

    private static string GetDetailNarrative(ActivityItemViewModel selected)
    {
        return selected.Category switch
        {
            ActivityCategory.Browsing => $"Browsed {selected.Location} during this remembered moment.",
            ActivityCategory.Research => $"Reviewed and compared web context around {selected.Location}.",
            ActivityCategory.Development => "Worked in a development context with code, tooling, or project windows.",
            ActivityCategory.Communication => "Moved through communication context, messages, or collaboration tools.",
            ActivityCategory.Documents => "Reviewed or edited document-based work.",
            ActivityCategory.ScreenshotCapture => "Captured a screenshot or visual reference for later recall.",
            _ => selected.MemoryDescription
        };
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

    private async void BookmarkButton_Click(object sender, RoutedEventArgs e)
    {
        var selected = _selectedDetailActivity ?? TimelineList.SelectedItem as ActivityItemViewModel;
        if (selected is null)
        {
            return;
        }

        await _store.SetBookmarkedAsync(selected.Id, !selected.Activity.IsBookmarked);
        await LoadActivitiesAsync();
        await ReselectDetailAsync(selected.Id);
    }

    private async void SaveNoteButton_Click(object sender, RoutedEventArgs e)
    {
        var selected = _selectedDetailActivity ?? TimelineList.SelectedItem as ActivityItemViewModel;
        if (selected is null)
        {
            return;
        }

        await _store.SetNoteAsync(selected.Id, MomentNoteTextBox.Text);
        await LoadActivitiesAsync();
        await ReselectDetailAsync(selected.Id);
    }

    private async void DeleteMomentButton_Click(object sender, RoutedEventArgs e)
    {
        var selected = _selectedDetailActivity ?? TimelineList.SelectedItem as ActivityItemViewModel;
        if (selected is null)
        {
            return;
        }

        var result = System.Windows.MessageBox.Show(
            this,
            "Delete this moment from your local timeline? Cached MomentTrace previews for this moment will also be removed.",
            "Delete moment",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        await _store.DeleteAsync(selected.Id);
        DeleteCachedPreview(selected.Activity.ScreenshotPath);
        DeleteCachedPreview(selected.Activity.ThumbnailPath);
        ClearDetails();
        await LoadDateFiltersAsync();
        await LoadActivitiesAsync();
        await LoadOverviewAsync();
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

    private void ToggleTimelineGroupButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: TimelineSessionGroupViewModel group })
        {
            group.ToggleExpanded();
        }
    }

    private async Task TogglePinnedAsync(ActivityItemViewModel activity)
    {
        await _store.SetPinnedAsync(activity.Id, !activity.Activity.IsPinned);
        await LoadActivitiesAsync();
        await ReselectDetailAsync(activity.Id);
    }

    private async Task ReselectDetailAsync(long activityId)
    {
        var refreshed = (await _store.QueryAsync(new TimelineQuery { Limit = 1000 }))
            .FirstOrDefault(activity => activity.Id == activityId);
        if (refreshed is null)
        {
            ClearDetails();
            return;
        }

        ShowDetails(new ActivityItemViewModel(refreshed));
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

    private static void DeleteCachedPreview(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            var fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath) || !IsMomentTracePreviewPath(fullPath))
            {
                return;
            }

            File.Delete(fullPath);
        }
        catch
        {
            // Deleting the timeline row matters more than failing on a locked cache file.
        }
    }

    private static bool IsMomentTracePreviewPath(string fullPath)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var momentTracePath = Path.Combine(appData, "MomentTrace");

        return IsInsideDirectory(fullPath, Path.Combine(momentTracePath, "screenshots"))
            || IsInsideDirectory(fullPath, Path.Combine(momentTracePath, "thumbnails"));
    }

    private static bool IsInsideDirectory(string fullPath, string directory)
    {
        var root = Path.GetFullPath(directory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        return fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase);
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

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowWorkArea.Apply(this);
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
        MaximizeButton.Content = WindowState == WindowState.Maximized ? "❐" : "□";
    }

    private void Window_StateChanged(object sender, EventArgs e)
    {
        WindowWorkArea.Apply(this);
        MaximizeButton.Content = WindowState == WindowState.Maximized ? "❐" : "□";
    }

    private void Window_LocationChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Normal)
        {
            WindowWorkArea.Apply(this);
        }
    }

    private void CloseWindowButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ToggleDetailsButton_Click(object sender, RoutedEventArgs e)
    {
        _isDetailVisible = !_isDetailVisible;
        DetailSurface.Visibility = _isDetailVisible ? Visibility.Visible : Visibility.Collapsed;
        ToggleDetailsButton.Content = "☰";
    }

    private void DetailTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button
            || !Enum.TryParse<DetailTab>(button.Content?.ToString(), out var tab))
        {
            return;
        }

        SelectDetailTab(tab);
    }

    private void SelectDetailTab(DetailTab tab)
    {
        _selectedDetailTab = tab;

        OverviewDetailTabContent.Visibility = tab == DetailTab.Overview ? Visibility.Visible : Visibility.Collapsed;
        ActivityDetailTabContent.Visibility = tab == DetailTab.Activity ? Visibility.Visible : Visibility.Collapsed;
        RelatedDetailTabContent.Visibility = tab == DetailTab.Related ? Visibility.Visible : Visibility.Collapsed;

        SetDetailTabButton(OverviewDetailTabButton, tab == DetailTab.Overview);
        SetDetailTabButton(ActivityDetailTabButton, tab == DetailTab.Activity);
        SetDetailTabButton(RelatedDetailTabButton, tab == DetailTab.Related);
    }

    private static void SetDetailTabButton(System.Windows.Controls.Button button, bool isActive)
    {
        button.Tag = isActive ? "Active" : button.Content?.ToString();
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
        var panelStrength = Math.Clamp(strength + 0.04, 0.55, 1.0);

        WindowSurface.Background = new SolidColorBrush(_currentTheme.WithAlpha(_currentTheme.Base, strength));
        RailSurface.Background = new SolidColorBrush(_currentTheme.WithAlpha(_currentTheme.Rail, panelStrength));
        TopCommandSurface.Background = new SolidColorBrush(_currentTheme.WithAlpha(_currentTheme.TopCommand, panelStrength));
        DetailSurface.Background = new SolidColorBrush(_currentTheme.WithAlpha(_currentTheme.Detail, panelStrength));
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var settingsWindow = new SettingsWindow(
            _store,
            _recorder,
            _startupRegistration,
            _preferences.Opacity,
            _preferences.TimelineViewMode,
            _preferences.Theme,
            SaveOpacity,
            SaveTimelineViewMode,
            SaveTheme,
            UpdateRecordingStatus,
            RefreshAfterLocalDataChangedAsync)
        {
            Owner = this
        };

        settingsWindow.ShowDialog();
    }

    private async Task RefreshAfterLocalDataChangedAsync()
    {
        await LoadDateFiltersAsync();
        await LoadActivitiesAsync();
        await LoadOverviewAsync();
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

    private void SaveTheme(string theme)
    {
        _preferences.Theme = AppThemePalette.Find(theme).Key;
        ApplyTheme(_preferences.Theme);
        _preferences.Save();
    }

    private void ApplyTheme(string theme)
    {
        _currentTheme = AppThemePalette.Find(theme);
        _currentTheme.ApplyTo(this);
        Background = System.Windows.Media.Brushes.Transparent;
        ApplySurfaceTransparency(_preferences.Opacity);
        UpdateNavigationState(OverviewPage.Visibility == Visibility.Visible);
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

    private static ActivityItemViewModel ToActivityItemViewModel(ActivityMomentViewModel activity)
    {
        return new ActivityItemViewModel(activity.Activity, activity.SessionStartedAt, activity.SessionEndedAt, activity.OccurrenceCount);
    }

    private sealed record SummaryMetricViewModel(string Label, string Value, string Detail);

    private sealed record HeatmapSegmentViewModel(string Fill, string Tooltip);

    private sealed record DateFilterOption(DateTime Date, string Label)
    {
        public override string ToString()
        {
            return Label;
        }
    }

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

        public string Tooltip => $"{Label}\nSeen {FormatDuration(Duration)}";

        public static FocusBlockViewModel Active(string appName, TimeSpan duration, MomentTrace.Core.ActivityKind kind)
        {
            var fill = kind switch
            {
                MomentTrace.Core.ActivityKind.Browser => ActivityPalette.Web,
                MomentTrace.Core.ActivityKind.Development => ActivityPalette.Development,
                MomentTrace.Core.ActivityKind.Document => ActivityPalette.Documents,
                MomentTrace.Core.ActivityKind.System => ActivityPalette.SystemActivity,
                _ => ActivityPalette.Apps
            };

            return new FocusBlockViewModel(appName, duration, fill, isGap: false);
        }

        public static FocusBlockViewModel Gap(TimeSpan duration)
        {
            return new FocusBlockViewModel("Gap or idle space", duration, ActivityPalette.Gap, isGap: true);
        }
    }

    private sealed record LegendItemViewModel(string Label, string Fill);

    private static class ActivityPalette
    {
        public const string Apps = "#8B5CF6";
        public const string Web = "#2F80ED";
        public const string Development = "#22C55E";
        public const string Documents = "#F59E0B";
        public const string SystemActivity = "#38BDF8";
        public const string Gap = "#10233F";
        public const string Empty = "#152033";

        public static string Scale(string hex, double intensity)
        {
            var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
            var amount = intensity > 0.66 ? 1.0 : intensity > 0.33 ? 0.72 : 0.44;
            var baseColor = System.Windows.Media.Color.FromRgb(21, 32, 51);
            return ToHex(Mix(color, baseColor, amount));
        }

        private static System.Windows.Media.Color Mix(System.Windows.Media.Color foreground, System.Windows.Media.Color background, double foregroundAmount)
        {
            var inverse = 1 - foregroundAmount;
            return System.Windows.Media.Color.FromRgb(
                (byte)Math.Round((foreground.R * foregroundAmount) + (background.R * inverse)),
                (byte)Math.Round((foreground.G * foregroundAmount) + (background.G * inverse)),
                (byte)Math.Round((foreground.B * foregroundAmount) + (background.B * inverse)));
        }

        private static string ToHex(System.Windows.Media.Color color)
        {
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }
    }

    private sealed class DetailActivityRowViewModel
    {
        public DetailActivityRowViewModel(TimelineActivity activity)
        {
            var item = new ActivityItemViewModel(activity);
            TimeRange = activity.StartedAt.LocalDateTime.ToString("t");
            Title = item.Title;
            Source = string.IsNullOrWhiteSpace(activity.Resource)
                ? item.TechnicalSource
                : activity.Resource;
            Duration = $"Seen {item.Duration}";
        }

        public string TimeRange { get; }

        public string Title { get; }

        public string Source { get; }

        public string Duration { get; }
    }

    private sealed class RelatedMomentViewModel
    {
        private readonly ActivityItemViewModel _activity;

        public RelatedMomentViewModel(ActivityItemViewModel activity, string reason)
        {
            _activity = activity;
            Title = activity.Title;
            Detail = $"{reason} · {activity.CardTime} · seen {activity.Duration}";
        }

        public string Title { get; }

        public string Detail { get; }

        public ImageSource? ProgramIcon => _activity.ProgramIcon;
    }

    private enum TimelineSemanticFilter
    {
        Browsing,
        Development,
        Communication,
        Documents
    }

    private enum TimelineSourceFilter
    {
        Apps,
        Websites,
        Files
    }

    private enum TimelineLibraryFilter
    {
        SavedMoments,
        Notes,
        Bookmarks
    }

    private enum DetailTab
    {
        Overview,
        Activity,
        Related
    }

    private sealed class TimelineSessionGroupViewModel(TimelineSessionGroup group) : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public string Name { get; } = group.Name;

        public string Summary { get; } = group.Summary;

        public string TimeRange { get; } = group.TimeRange;

        public string AccentBrush { get; } = group.AccentBrush;

        public IReadOnlyList<ActivityItemViewModel> Activities { get; } = group.Activities.Select(ToActivityItemViewModel).ToList();

        public int ActivityCount => Activities.Count;

        public IReadOnlyList<ActivityItemViewModel> VisibleActivities => group.VisibleActivities.Select(ToActivityItemViewModel).ToList();

        public string ToggleText => group.ToggleText;

        public string ToggleGlyph => group.ToggleGlyph;

        public Visibility ToggleVisibility => group.HasMoreThanPreviewCount ? Visibility.Visible : Visibility.Collapsed;

        public void ToggleExpanded()
        {
            group.ToggleExpanded();
            OnPropertyChanged(nameof(VisibleActivities));
            OnPropertyChanged(nameof(ToggleText));
            OnPropertyChanged(nameof(ToggleGlyph));
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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
            _dateRolloverTimer.Stop();
            SystemEvents.PowerModeChanged -= SystemEvents_PowerModeChanged;
            return;
        }

        e.Cancel = true;
        Hide();
    }
}
