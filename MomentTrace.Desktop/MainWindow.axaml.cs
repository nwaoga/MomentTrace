using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using MomentTrace.Core;
using MomentTrace.Presentation;
using MomentTrace.Storage;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
    private readonly SqliteActivityStore _store = new(SqliteActivityStore.DefaultDatabasePath);
    private readonly List<TimelineActivity> _visibleActivities = [];
    private IReadOnlyList<ActivityMomentViewModel> _sessions = [];
    private IReadOnlyList<TimelineSessionGroup> _timelineGroups = [];
    private OverviewPresentationModel? _currentOverview;
    private string _topAppsSort = "Duration";
    private long? _selectedMomentId;
    private string _searchText = string.Empty;
    private DetailTab _selectedDetailTab = DetailTab.Overview;
    private bool _isDetailPaneVisible = true;

    public MainWindow()
    {
        InitializeComponent();
        _overviewBuilder = new OverviewPresentationBuilder(_timelineBuilder);
        TypeFilterComboBox.ItemsSource = new[] { "All" };
        TypeFilterComboBox.SelectedIndex = 0;
        ApplyDetailPaneVisibility();
        Opened += async (_, _) => await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        await _store.InitializeAsync();

        var activities = await _store.QueryAsync(new TimelineQuery { Limit = 1500 });
        _visibleActivities.Clear();
        _visibleActivities.AddRange(activities.Where(activity => !ActivityExclusionFilter.IsExcluded(activity)));

        var dateFilters = TimelineDateFilterBuilder.Build(_visibleActivities, DateTime.Today);
        DateFilterComboBox.ItemsSource = dateFilters;
        DateFilterComboBox.SelectedItem ??= dateFilters.FirstOrDefault();

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
            .Select(app => ToTopAppRow(app))
            .ToList();
        FocusBlocksItems.ItemsSource = overview.FocusBlocks
            .Take(8)
            .Select((block, index) => new FocusRow(
                block.Label,
                OverviewPresentationBuilder.FormatDuration(block.Duration),
                (index % 3) switch
                {
                    0 => "#8B5CF6",
                    1 => "#22C55E",
                    _ => "#0EA5E9"
                }))
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

    private void ToggleDetailsButton_Click(object? sender, RoutedEventArgs e)
    {
        _isDetailPaneVisible = !_isDetailPaneVisible;
        ApplyDetailPaneVisibility();
    }

    private void MinimizeShellButton_Click(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeShellButton_Click(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void CloseShellButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void SearchTextBox_GotFocus(object? sender, RoutedEventArgs e)
    {
        SearchSurface.Background = Brush.Parse("#1B2B46");
        SearchSurface.BorderBrush = Brush.Parse("#6E90C2");
        SearchTextBox.Background = Brush.Parse("#1B2B46");
        SearchIconText.Foreground = Brush.Parse("#8FA9CB");
    }

    private void SearchTextBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        SearchSurface.Background = Brush.Parse("#111C31");
        SearchSurface.BorderBrush = Brush.Parse("#233653");
        SearchTextBox.Background = Brushes.Transparent;
        SearchIconText.Foreground = Brush.Parse("#64748B");
    }

    private void ApplyDetailPaneVisibility()
    {
        if (TimelineContentGrid.ColumnDefinitions.Count < 2)
        {
            return;
        }

        DetailSurface.IsVisible = _isDetailPaneVisible;
        TimelineContentGrid.ColumnDefinitions[1].Width = _isDetailPaneVisible
            ? new GridLength(380)
            : new GridLength(0);
    }

    private void ToolbarDragRegion_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed is false)
        {
            return;
        }

        if (e.Source is Visual sourceVisual && IsInteractiveChromeElement(sourceVisual))
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            return;
        }

        BeginMoveDrag(e);
    }

    private static bool IsInteractiveChromeElement(Visual sourceVisual)
    {
        return sourceVisual.GetSelfAndVisualAncestors().Any(visual =>
            visual is Button or TextBox or ComboBox);
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
        DetailGlyphHost.Background = Brush.Parse(selected.IconBackground);
        DetailGlyphText.Text = selected.IconGlyph;
        DetailGlyphText.Foreground = Brush.Parse(selected.IconForeground);
        DetailTitleText.Text = selected.Title;
        DetailSubtitleText.Text = $"{selected.Location} · {selected.AppName}";
        DetailMetaSpanText.Text = selected.CapturedSpan;
        DetailMetaSourceText.Text = selected.TechnicalSource;
        DetailMetaStartedText.Text = $"{selected.SessionStartedAt.LocalDateTime:dddd, d MMM yyyy HH:mm:ss}";
        DetailMetaEndedText.Text = $"{selected.SessionEndedAt.LocalDateTime:dddd, d MMM yyyy HH:mm:ss}";
        DetailMetaCategoryText.Text = selected.CategoryLabel;
        DetailNarrativeText.Text = detail.Narrative;
        ActivityTabSummaryText.Text = detail.RawEntriesSummary;
        RelatedSummaryText.Text = detail.RelatedSummary;
        RawEntriesItems.ItemsSource = detail.RawEntries;
        RelatedMomentsItems.ItemsSource = detail.RelatedMoments;
        PinMomentButtonText.Text = selected.IsPinned ? "Pinned" : "Pin";
        BookmarkMomentButtonText.Text = selected.IsBookmarked ? "Bookmarked" : "Bookmark";
        MomentNoteTextBox.Text = selected.Note ?? string.Empty;
        SetActionButtonActive(PinMomentButton, selected.IsPinned);
        SetActionButtonActive(BookmarkMomentButton, selected.IsBookmarked);
        SetDetailPreview(selected);
        SelectDetailTab(_selectedDetailTab);
    }

    private void TopAppsSortButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { CommandParameter: string sortKey } || _currentOverview is null)
        {
            return;
        }

        _topAppsSort = sortKey;
        TopAppsItems.ItemsSource = SortTopApplications(_currentOverview.TopApplications)
            .Select(app => ToTopAppRow(app))
            .ToList();
        UpdateTopAppSortButtons();
    }

    private DateTime GetSelectedDate()
    {
        return DateFilterComboBox.SelectedItem is DateFilterOption option ? option.Date : DateTime.Today;
    }

    private void ClearDetailPanel()
    {
        DetailGlyphHost.Background = Brush.Parse("#172743");
        DetailGlyphText.Text = "MT";
        DetailGlyphText.Foreground = Brush.Parse("#A78BFA");
        DetailTitleText.Text = "Select a moment";
        DetailSubtitleText.Text = "Timeline context appears here";
        DetailMetaSpanText.Text = "Local only";
        DetailMetaSourceText.Text = "MomentTrace";
        DetailMetaStartedText.Text = "Local only";
        DetailMetaEndedText.Text = "Local only";
        DetailMetaCategoryText.Text = "Moment";
        DetailNarrativeText.Text = "Select a moment to inspect its grouped raw activity and nearby related work.";
        ActivityTabSummaryText.Text = "Select a moment to inspect its raw captured entries.";
        RelatedSummaryText.Text = "No related moments yet";
        RawEntriesItems.ItemsSource = Array.Empty<DetailActivityRowViewModel>();
        RelatedMomentsItems.ItemsSource = Array.Empty<RelatedMomentViewModel>();
        PinMomentButtonText.Text = "Pin";
        BookmarkMomentButtonText.Text = "Bookmark";
        MomentNoteTextBox.Text = string.Empty;
        SetActionButtonActive(PinMomentButton, false);
        SetActionButtonActive(BookmarkMomentButton, false);
        DetailPreviewImage.Source = null;
        DetailPreviewHost.IsVisible = false;
        SelectDetailTab(DetailTab.Overview);
    }

    private void DetailTabButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { CommandParameter: string tabName }
            || !Enum.TryParse<DetailTab>(tabName, out var tab))
        {
            return;
        }

        SelectDetailTab(tab);
    }

    private void SelectDetailTab(DetailTab tab)
    {
        _selectedDetailTab = tab;

        OverviewDetailTabContent.IsVisible = tab == DetailTab.Overview;
        ActivityDetailTabContent.IsVisible = tab == DetailTab.Activity;
        RelatedDetailTabContent.IsVisible = tab == DetailTab.Related;

        SetDetailTabState(OverviewDetailTabText, OverviewDetailTabUnderline, tab == DetailTab.Overview);
        SetDetailTabState(ActivityDetailTabText, ActivityDetailTabUnderline, tab == DetailTab.Activity);
        SetDetailTabState(RelatedDetailTabText, RelatedDetailTabUnderline, tab == DetailTab.Related);
    }

    private static void SetDetailTabState(TextBlock text, Border underline, bool isActive)
    {
        text.Foreground = Brush.Parse(isActive ? "#A78BFA" : "#94A3B8");
        text.FontWeight = isActive ? FontWeight.SemiBold : FontWeight.Regular;
        underline.IsVisible = isActive;
    }

    private ActivityMomentViewModel? GetSelectedMoment()
    {
        if (_selectedMomentId is not long selectedId)
        {
            return null;
        }

        return _sessions.FirstOrDefault(session => session.Id == selectedId);
    }

    private async void PinMomentButton_Click(object? sender, RoutedEventArgs e)
    {
        var selected = GetSelectedMoment();
        if (selected is null)
        {
            return;
        }

        await _store.SetPinnedAsync(selected.Id, !selected.IsPinned);
        await ReloadAsync();
    }

    private async void BookmarkMomentButton_Click(object? sender, RoutedEventArgs e)
    {
        var selected = GetSelectedMoment();
        if (selected is null)
        {
            return;
        }

        await _store.SetBookmarkedAsync(selected.Id, !selected.IsBookmarked);
        await ReloadAsync();
    }

    private async void SaveNoteButton_Click(object? sender, RoutedEventArgs e)
    {
        var selected = GetSelectedMoment();
        if (selected is null)
        {
            return;
        }

        await _store.SetNoteAsync(selected.Id, MomentNoteTextBox.Text);
        await ReloadAsync();
    }

    private async void DeleteMomentButton_Click(object? sender, RoutedEventArgs e)
    {
        var selected = GetSelectedMoment();
        if (selected is null)
        {
            return;
        }

        if (!await ConfirmDeleteMomentAsync())
        {
            return;
        }

        await _store.DeleteAsync(selected.Id);
        DeleteCachedPreview(selected.ScreenshotPath);
        DeleteCachedPreview(selected.ThumbnailPath);
        _selectedMomentId = null;
        await ReloadAsync();
    }

    private void OpenMomentButton_Click(object? sender, RoutedEventArgs e)
    {
        var selected = GetSelectedMoment();
        if (selected is null)
        {
            return;
        }

        OpenActivity(selected);
    }

    private void SetActionButtonActive(Button button, bool isActive)
    {
        button.Background = Brush.Parse(isActive ? "#7C5CFF" : "#172743");
        button.BorderBrush = Brush.Parse(isActive ? "#8F79FF" : "#2B3D5B");
        button.Foreground = Brush.Parse(isActive ? "#FFFFFF" : "#CBD5E1");
    }

    private void SetDetailPreview(ActivityMomentViewModel selected)
    {
        DetailPreviewImage.Source = null;
        DetailPreviewHost.IsVisible = false;

        var previewPath = !string.IsNullOrWhiteSpace(selected.ScreenshotPath) && File.Exists(selected.ScreenshotPath)
            ? selected.ScreenshotPath
            : selected.ThumbnailPath;

        if (string.IsNullOrWhiteSpace(previewPath) || !File.Exists(previewPath))
        {
            return;
        }

        try
        {
            DetailPreviewImage.Source = new Bitmap(previewPath);
            DetailPreviewHost.IsVisible = true;
        }
        catch
        {
            DetailPreviewImage.Source = null;
            DetailPreviewHost.IsVisible = false;
        }
    }

    private async Task<bool> ConfirmDeleteMomentAsync()
    {
        var dialog = new Window
        {
            Title = "Delete moment",
            Width = 420,
            Height = 210,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = Brush.Parse("#0F1A2E")
        };

        dialog.Content = new Border
        {
            Background = Brush.Parse("#0F1A2E"),
            BorderBrush = Brush.Parse("#233653"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(22),
            Child = BuildDeleteDialogContent()
        };

        return await dialog.ShowDialog<bool>(this);

        StackPanel BuildDeleteDialogContent()
        {
            var deleteButton = new Button
            {
                Content = "Delete",
                Classes = { "detailDanger" },
                Padding = new Thickness(16, 9)
            };
            var cancelButton = new Button
            {
                Content = "Cancel",
                Classes = { "detailSecondary" },
                Padding = new Thickness(16, 9)
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
                        Text = "Delete this moment?",
                        Foreground = Brush.Parse("#F8FAFC"),
                        FontSize = 20,
                        FontWeight = FontWeight.SemiBold
                    },
                    new TextBlock
                    {
                        Text = "This removes it from your local timeline and deletes cached MomentTrace previews for this moment.",
                        Foreground = Brush.Parse("#94A3B8"),
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

    private static void OpenActivity(ActivityMomentViewModel activity)
    {
        try
        {
            var target = GetOpenTarget(activity);
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch
        {
            // Keep the shell calm if Windows cannot reopen the captured target.
        }
    }

    private static string GetOpenTarget(ActivityMomentViewModel activity)
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

    private static void DeleteCachedPreview(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            var fullPath = Path.GetFullPath(path);
            if (File.Exists(fullPath) && IsMomentTracePreviewPath(fullPath))
            {
                File.Delete(fullPath);
            }
        }
        catch
        {
            // The timeline row has been deleted; locked cache files can be cleaned later.
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

    private static TopAppRow ToTopAppRow(TopApplicationViewModel app)
    {
        var glyph = string.IsNullOrWhiteSpace(app.AppName) ? "?" : app.AppName[..1].ToUpperInvariant();
        var accent = app.AppName.ToLowerInvariant() switch
        {
            var name when name.Contains("edge") => "#0EA5E9",
            var name when name.Contains("code") => "#8B5CF6",
            var name when name.Contains("teams") || name.Contains("slack") => "#22C55E",
            var name when name.Contains("word") || name.Contains("docs") => "#F59E0B",
            _ => "#64748B"
        };

        return new TopAppRow(app.AppName, $"{app.Sessions} sessions", app.Duration, app.PercentOfDay, glyph, accent);
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
            BuildCardSubtitle(session),
            $"{session.Location} · {session.AppName}",
            session.CardTime,
            session.SeenDuration,
            GetPreviewUri(session.ThumbnailPath) ?? GetPreviewUri(session.ScreenshotPath),
            session.IconGlyph,
            session.IconBackground,
            session.IconForeground,
            session.KindLabel,
            session.KindBackground,
            session.KindForeground,
            session.SavedIndicatorText,
            session.HasSavedIndicator,
            false,
            "#16233A",
            "#2C3E5E");
    }

    private static string BuildCardSubtitle(ActivityMomentViewModel session)
    {
        if (!string.IsNullOrWhiteSpace(session.Resource) && session.Category is ActivityCategory.Browsing or ActivityCategory.Research)
        {
            return session.Location;
        }

        if (!string.IsNullOrWhiteSpace(session.Location)
            && !string.Equals(session.Location, session.AppName, StringComparison.OrdinalIgnoreCase)
            && session.Location is not "Development workspace"
            && session.Location is not "Messages and conversations"
            && session.Location is not "Documents and notes"
            && session.Location is not "Visual capture"
            && session.Location is not "System")
        {
            return session.Location;
        }

        return session.AppName;
    }

    private MomentRow ToMomentRow(ActivityMomentViewModel session, bool isSelected)
    {
        var row = ToMomentRow(session);
        return row with
        {
            IsSelected = isSelected,
            CardBackground = isSelected ? "#1B2742" : "#16233A",
            BorderBrush = isSelected ? "#7C5CFF" : "#2C3E5E",
            ShowPreview = !string.IsNullOrWhiteSpace(row.PreviewUri),
            ShowGlyphFallback = string.IsNullOrWhiteSpace(row.PreviewUri)
        };
    }

    private static string? GetPreviewUri(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        return new Uri(path, UriKind.Absolute).AbsoluteUri;
    }

    private sealed record MomentRow(
        long Id,
        string Title,
        string Subtitle,
        string Meta,
        string TimeLabel,
        string SeenLabel,
        string? PreviewUri,
        string Glyph,
        string GlyphBackground,
        string GlyphForeground,
        string KindLabel,
        string KindBackground,
        string KindForeground,
        string SavedIndicator,
        bool HasSavedIndicator,
        bool IsSelected,
        string CardBackground,
        string BorderBrush,
        bool ShowPreview = false,
        bool ShowGlyphFallback = true);

    private sealed record TimelineGroupRow(
        string Name,
        string Summary,
        string TimeRange,
        string AccentBrush,
        string CountLabel,
        IReadOnlyList<MomentRow> Moments);

    private sealed record TopAppRow(string AppName, string Meta, string Duration, string Percent, string Glyph, string AccentBrush);

    private sealed record FocusRow(string Label, string Duration, string AccentBrush);

    private enum DetailTab
    {
        Overview,
        Activity,
        Related
    }
}
