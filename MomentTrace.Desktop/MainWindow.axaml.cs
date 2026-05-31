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
                .ToList());

        var rows = _sessions
            .Select(session => new MomentRow(
                session.Title,
                session.MemoryDescription,
                $"{session.Location} · {session.AppName}",
                session.CardTime,
                session.IconGlyph,
                session.IconBackground,
                session.IconForeground))
            .ToList();

        TimelineListBox.ItemsSource = rows;
        TimelineCountText.Text = $"{rows.Count} moments on {TimelineDateFilterBuilder.FormatDateLabel(selectedDate, DateTime.Today)}";
        TimelineSubtitleText.Text = rows.Count == 0
            ? "No activity captured for this day yet"
            : "Local moments grouped from the shared presentation layer";

        if (rows.Count > 0)
        {
            TimelineListBox.SelectedIndex = 0;
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
        var overview = _overviewBuilder.Build(_visibleActivities, selectedDate);

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

        TopAppsItems.ItemsSource = overview.TopApplications
            .Select(app => new TopAppRow(app.AppName, $"{app.Sessions} sessions", app.Duration, app.PercentOfDay))
            .ToList();
        FocusBlocksItems.ItemsSource = overview.FocusBlocks
            .Take(8)
            .Select(block => new FocusRow(block.Label, OverviewPresentationBuilder.FormatDuration(block.Duration)))
            .ToList();
        LegendItems.ItemsSource = overview.Legend;
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

    private void TimelineListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (TimelineListBox.SelectedIndex < 0 || TimelineListBox.SelectedIndex >= _sessions.Count)
        {
            ClearDetailPanel();
            return;
        }

        var selected = _sessions[TimelineListBox.SelectedIndex];
        var detail = _detailContextBuilder.Build(selected, _visibleActivities);

        DetailTitleText.Text = selected.Title;
        DetailSubtitleText.Text = $"{selected.CardTime} · {selected.CategoryLabel} · {selected.Location}";
        DetailNarrativeText.Text = detail.Narrative;
        RawSummaryText.Text = detail.RawEntriesSummary;
        RelatedSummaryText.Text = detail.RelatedSummary;
        RawEntriesItems.ItemsSource = detail.RawEntries;
        RelatedMomentsItems.ItemsSource = detail.RelatedMoments;
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

    private sealed record MomentRow(
        string Title,
        string Subtitle,
        string Meta,
        string TimeLabel,
        string Glyph,
        string GlyphBackground,
        string GlyphForeground);

    private sealed record TopAppRow(string AppName, string Meta, string Duration, string Percent);

    private sealed record FocusRow(string Label, string Duration);
}
