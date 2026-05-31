using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using MomentTrace.Core;
using MomentTrace.Presentation;
using MomentTrace.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MomentTrace.Desktop;

public partial class MainWindow : Window
{
    private readonly TimelinePresentationBuilder _timelineBuilder = new(
        RuleBasedActivityClassifier.Default,
        RuleBasedSessionSummarizer.Default);
    private readonly OverviewPresentationBuilder _overviewBuilder;
    private readonly DetailContextBuilder _detailContextBuilder = new(RuleBasedActivityClassifier.Default);
    private readonly List<TimelineActivity> _visibleActivities = [];
    private IReadOnlyList<ActivityMomentViewModel> _sessions = [];
    private IReadOnlyList<TimelineSessionGroup> _timelineGroups = [];
    private OverviewPresentationModel? _currentOverview;
    private string _topAppsSort = "Duration";
    private long? _selectedMomentId;
    private string _searchText = string.Empty;

    public MainWindow()
    {
        InitializeComponent();
        _overviewBuilder = new OverviewPresentationBuilder(_timelineBuilder);
        Opened += async (_, _) => await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        var store = new SqliteActivityStore(SqliteActivityStore.DefaultDatabasePath);
        await store.InitializeAsync();

        var activities = await store.QueryAsync(new TimelineQuery { Limit = 1500 });
        _visibleActivities.Clear();
        _visibleActivities.AddRange(activities.Where(activity => !ActivityExclusionFilter.IsExcluded(activity)));

        var dateFilters = TimelineDateFilterBuilder.Build(_visibleActivities, DateTime.Today);
        DateFilterComboBox.ItemsSource = dateFilters;
        DateFilterComboBox.SelectedItem ??= dateFilters.FirstOrDefault();

        StatusText.Text = $"{_visibleActivities.Count} captured rows found in local storage";
        ShellSummaryText.Text = _visibleActivities.Count == 0
            ? "No captured activity yet"
            : $"{_visibleActivities.Count} raw rows available in the local database";

        await RefreshTimelineAsync();
        await RefreshOverviewAsync();
    }

    private async Task RefreshTimelineAsync()
    {
        await Task.Yield();

        var selectedDate = GetSelectedDate();
        _sessions = _timelineBuilder.BuildSessions(
            _visibleActivities
                .Where(activity => activity.StartedAt.LocalDateTime.Date == selectedDate)
                .ToList())
            .Where(MatchesSearch)
            .ToList();
        _timelineGroups = _timelineBuilder.BuildGroups(_sessions);
        if (_sessions.Count == 0)
        {
            _selectedMomentId = null;
        }
        else if (_selectedMomentId is null || _sessions.All(session => session.Id != _selectedMomentId.Value))
        {
            _selectedMomentId = _sessions[0].Id;
        }

        TimelineGroupsItems.ItemsSource = BuildTimelineGroupRows();
        TimelineCountText.Text = $"{_sessions.Count} moments on {TimelineDateFilterBuilder.FormatDateLabel(selectedDate, DateTime.Today)}";
        TimelineSubtitleText.Text = _sessions.Count == 0
            ? "No activity captured for this day yet"
            : "Local moments grouped from the shared presentation layer";

        if (_sessions.Count > 0 && _selectedMomentId is long selectedId)
        {
            var selected = _sessions.FirstOrDefault(session => session.Id == selectedId) ?? _sessions[0];
            ShowDetail(selected);
        }
        else
        {
            ClearDetailPanel();
        }
    }

    private async Task RefreshOverviewAsync()
    {
        await Task.Yield();

        var selectedDate = GetSelectedDate();
        _currentOverview = _overviewBuilder.Build(_visibleActivities, selectedDate);
        var overview = _currentOverview;

        OverviewSubtitleText.Text = $"{overview.Sessions.Count} moments on {TimelineDateFilterBuilder.FormatDateLabel(selectedDate, DateTime.Today)}";
        ApplyMetric(MetricOneValue, MetricOneDetail, overview.SummaryMetrics.ElementAtOrDefault(0));
        ApplyMetric(MetricTwoValue, MetricTwoDetail, overview.SummaryMetrics.ElementAtOrDefault(1));
        ApplyMetric(MetricThreeValue, MetricThreeDetail, overview.SummaryMetrics.ElementAtOrDefault(2));
        ApplyMetric(MetricFourValue, MetricFourDetail, overview.SummaryMetrics.ElementAtOrDefault(3));

        HeatmapPanel.Children.Clear();
        foreach (var segment in overview.HeatmapSegments)
        {
            var cell = new Border
            {
                Width = 10,
                Height = 28,
                CornerRadius = new CornerRadius(3),
                Background = Brush.Parse(segment.Fill),
                Margin = new Thickness(0, 0, 4, 4)
            };
            ToolTip.SetTip(cell, segment.Tooltip);
            HeatmapPanel.Children.Add(cell);
        }

        TopAppsItems.ItemsSource = SortTopApplications(overview.TopApplications)
            .Select(app => new TopAppRow(app.AppName, $"{app.Sessions} sessions", app.Duration, app.PercentOfDay))
            .ToList();
        FocusBlocksItems.ItemsSource = overview.FocusBlocks
            .Take(8)
            .Select(block => new FocusRow(block.Label, OverviewPresentationBuilder.FormatDuration(block.Duration)))
            .ToList();
        LegendItems.ItemsSource = overview.Legend;
        UpdateTopAppSortButtons();
    }

    private void TimelineNavButton_Click(object? sender, RoutedEventArgs e)
    {
        TimelinePage.IsVisible = true;
        OverviewPage.IsVisible = false;
        SetActiveNav(isTimeline: true);
    }

    private void OverviewNavButton_Click(object? sender, RoutedEventArgs e)
    {
        TimelinePage.IsVisible = false;
        OverviewPage.IsVisible = true;
        SetActiveNav(isTimeline: false);
    }

    private async void RefreshButton_Click(object? sender, RoutedEventArgs e)
    {
        await ReloadAsync();
    }

    private async void DateFilterComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        await RefreshTimelineAsync();
        await RefreshOverviewAsync();
    }

    private async void SearchTextBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        _searchText = SearchTextBox.Text?.Trim() ?? string.Empty;

        if (!IsLoaded)
        {
            return;
        }

        await RefreshTimelineAsync();
    }

    private void TimelineMomentButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { CommandParameter: MomentRow row })
        {
            return;
        }

        var selected = _sessions.FirstOrDefault(session => session.Id == row.Id);
        if (selected is null)
        {
            return;
        }

        _selectedMomentId = selected.Id;
        TimelineGroupsItems.ItemsSource = BuildTimelineGroupRows();
        ShowDetail(selected);
    }

    private void ShowDetail(ActivityMomentViewModel selected)
    {
        var detail = _detailContextBuilder.Build(selected, _visibleActivities);
        DetailTitleText.Text = selected.Title;
        DetailSubtitleText.Text = $"{selected.CardTime} · {selected.CategoryLabel} · {selected.Location}";
        DetailNarrativeText.Text = detail.Narrative;
        RawSummaryText.Text = detail.RawEntriesSummary;
        RelatedSummaryText.Text = detail.RelatedSummary;
        RawEntriesItems.ItemsSource = detail.RawEntries;
        RelatedMomentsItems.ItemsSource = detail.RelatedMoments;
    }

    private void TopAppsSortButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { CommandParameter: string sortKey } || _currentOverview is null)
        {
            return;
        }

        _topAppsSort = sortKey;
        TopAppsItems.ItemsSource = SortTopApplications(_currentOverview.TopApplications)
            .Select(app => new TopAppRow(app.AppName, $"{app.Sessions} sessions", app.Duration, app.PercentOfDay))
            .ToList();
        UpdateTopAppSortButtons();
    }

    private DateTime GetSelectedDate()
    {
        return DateFilterComboBox.SelectedItem is DateFilterOption option ? option.Date : DateTime.Today;
    }

    private void ClearDetailPanel()
    {
        DetailTitleText.Text = "Select a moment";
        DetailSubtitleText.Text = "Timeline context appears here";
        DetailNarrativeText.Text = "Select a moment to inspect its grouped raw activity and nearby related work.";
        RawSummaryText.Text = "No moment selected";
        RelatedSummaryText.Text = "No related moments yet";
        RawEntriesItems.ItemsSource = Array.Empty<DetailActivityRowViewModel>();
        RelatedMomentsItems.ItemsSource = Array.Empty<RelatedMomentViewModel>();
    }

    private void ApplyMetric(TextBlock valueBlock, TextBlock detailBlock, OverviewMetricViewModel? metric)
    {
        valueBlock.Text = metric?.Value ?? "0";
        detailBlock.Text = metric?.Detail ?? string.Empty;
    }

    private void SetActiveNav(bool isTimeline)
    {
        TimelineNavButton.Background = Brush.Parse(isTimeline ? "#7C5CFF" : "#172743");
        TimelineNavButton.BorderBrush = Brush.Parse(isTimeline ? "#7C5CFF" : "#2B3D5B");
        TimelineNavButton.Foreground = Brush.Parse(isTimeline ? "#FFFFFF" : "#CBD5E1");

        OverviewNavButton.Background = Brush.Parse(!isTimeline ? "#7C5CFF" : "#172743");
        OverviewNavButton.BorderBrush = Brush.Parse(!isTimeline ? "#7C5CFF" : "#2B3D5B");
        OverviewNavButton.Foreground = Brush.Parse(!isTimeline ? "#FFFFFF" : "#CBD5E1");
    }

    private IReadOnlyList<TimelineGroupRow> BuildTimelineGroupRows()
    {
        return _timelineGroups
            .Select(group => new TimelineGroupRow(
                group.Name,
                group.Summary,
                group.TimeRange,
                group.AccentBrush,
                $"{group.VisibleActivities.Count} moment{(group.VisibleActivities.Count == 1 ? string.Empty : "s")}",
                group.VisibleActivities
                    .Select(session => ToMomentRow(session, session.Id == _selectedMomentId))
                    .ToList()))
            .ToList();
    }

    private void UpdateTopAppSortButtons()
    {
        ApplySortButtonState(TopAppsSeenSortButton, _topAppsSort == "Duration");
        ApplySortButtonState(TopAppsSessionsSortButton, _topAppsSort == "Sessions");
        ApplySortButtonState(TopAppsPercentSortButton, _topAppsSort == "Percent");
    }

    private static void ApplySortButtonState(Button button, bool isActive)
    {
        button.Background = Brush.Parse(isActive ? "#7C5CFF" : "#172743");
        button.BorderBrush = Brush.Parse(isActive ? "#8F79FF" : "#2B3D5B");
        button.Foreground = Brush.Parse(isActive ? "#FFFFFF" : "#CBD5E1");
    }

    private IEnumerable<TopApplicationViewModel> SortTopApplications(IEnumerable<TopApplicationViewModel> apps)
    {
        return _topAppsSort switch
        {
            "Sessions" => apps.OrderByDescending(app => app.SessionCount),
            "Percent" => apps.OrderByDescending(app => app.Percent),
            _ => apps.OrderByDescending(app => app.ActiveDuration)
        };
    }

    private bool MatchesSearch(ActivityMomentViewModel session)
    {
        if (string.IsNullOrWhiteSpace(_searchText))
        {
            return true;
        }

        return Contains(session.Title)
            || Contains(session.MemoryDescription)
            || Contains(session.Location)
            || Contains(session.AppName)
            || Contains(session.CategoryLabel);

        bool Contains(string? value) =>
            !string.IsNullOrWhiteSpace(value)
            && value.Contains(_searchText, StringComparison.OrdinalIgnoreCase);
    }

    private static MomentRow ToMomentRow(ActivityMomentViewModel session)
    {
        return new MomentRow(
            session.Id,
            session.Title,
            session.MemoryDescription,
            $"{session.Location} · {session.AppName}",
            session.CardTime,
            session.IconGlyph,
            session.IconBackground,
            session.IconForeground,
            false,
            "#16233A",
            "#2C3E5E");
    }

    private MomentRow ToMomentRow(ActivityMomentViewModel session, bool isSelected)
    {
        var row = ToMomentRow(session);
        return row with
        {
            IsSelected = isSelected,
            CardBackground = isSelected ? "#1B2742" : "#16233A",
            BorderBrush = isSelected ? "#7C5CFF" : "#2C3E5E"
        };
    }

    private sealed record MomentRow(
        long Id,
        string Title,
        string Subtitle,
        string Meta,
        string TimeLabel,
        string Glyph,
        string GlyphBackground,
        string GlyphForeground,
        bool IsSelected,
        string CardBackground,
        string BorderBrush);

    private sealed record TimelineGroupRow(
        string Name,
        string Summary,
        string TimeRange,
        string AccentBrush,
        string CountLabel,
        IReadOnlyList<MomentRow> Moments);

    private sealed record TopAppRow(string AppName, string Meta, string Duration, string Percent);

    private sealed record FocusRow(string Label, string Duration);
}
