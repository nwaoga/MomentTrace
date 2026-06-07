using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
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
using System.Windows.Input;

namespace MomentTrace.Desktop;

public partial class MainWindow : Window
{
    private static readonly DesktopAppIconProvider AppIconProvider = new();
    private readonly TimelinePresentationBuilder _timelineBuilder = new(
        RuleBasedActivityClassifier.Default,
        RuleBasedSessionSummarizer.Default);
    private readonly OverviewPresentationBuilder _overviewBuilder;
    private readonly DetailContextBuilder _detailContextBuilder = new(RuleBasedActivityClassifier.Default);
    private readonly SqliteActivityStore _store;
    private readonly IActivityRecorder? _recorder;
    private readonly DesktopWindowPreferences _preferences;
    private readonly DispatcherTimer _refreshTimer;
    private readonly List<TimelineActivity> _visibleActivities = [];
    private IReadOnlyList<ActivityMomentViewModel> _sessions = [];
    private IReadOnlyList<TimelineSessionGroup> _timelineGroups = [];
    private OverviewPresentationModel? _currentOverview;
    private readonly Dictionary<string, bool> _detailSectionExpanded = new(StringComparer.Ordinal)
    {
        ["OverviewNarrative"] = true,
        ["Preview"] = true,
        ["Metadata"] = true,
        ["Note"] = true,
        ["RecentActivity"] = true,
        ["CapturedActivity"] = true,
        ["RelatedMoments"] = true
    };
    private long? _selectedMomentId;
    private string _searchText = string.Empty;
    private DetailTab _selectedDetailTab = DetailTab.Overview;
    private bool _isDetailPaneVisible = true;
    private RailSelection _selectedRailSelection = RailSelection.Timeline;
    private DateTime? _timelineFocusStart;
    private DateTime? _timelineFocusEnd;
    private DesktopAppThemePalette _currentTheme;
    private ScrollViewer? _pendingDragScrollViewer;
    private Point _scrollDragStartPoint;
    private Vector _scrollDragStartOffset;
    private bool _isScrollDragging;
    private bool _isReloading;
    private readonly ICommand _overviewHeatmapCommand;
    private readonly ICommand _overviewFocusCommand;
    private static readonly Cursor ScrollReadyCursor = new(StandardCursorType.Hand);
    private static readonly Cursor ScrollDraggingCursor = new(StandardCursorType.SizeAll);

    public MainWindow(SqliteActivityStore? store = null, IActivityRecorder? recorder = null)
    {
        InitializeComponent();
        _store = store ?? new SqliteActivityStore(SqliteActivityStore.DefaultDatabasePath);
        _recorder = recorder;
        _preferences = DesktopWindowPreferences.Load();
        _currentTheme = DesktopAppThemePalette.Find(_preferences.Theme);
        _overviewBuilder = new OverviewPresentationBuilder(_timelineBuilder);
        _overviewHeatmapCommand = new DelegateCommand<HeatmapSegmentViewModel>(segment => _ = ShowTimelineRangeAsync(segment.BlockStart, segment.BlockEnd));
        _overviewFocusCommand = new DelegateCommand<FocusBlockViewModel>(block => _ = HandleOverviewFocusBlockAsync(block));
        TypeFilterComboBox.ItemsSource = new[] { "All" };
        TypeFilterComboBox.SelectedIndex = 0;
        OverviewHeatmapControl.ItemCommand = _overviewHeatmapCommand;
        OverviewFocusRhythmControl.ItemCommand = _overviewFocusCommand;
        ApplyTheme(_preferences.Theme);
        ApplySurfaceTransparency(_preferences.Opacity);
        ApplyTimelineViewMode(_preferences.TimelineViewMode);
        ApplyDetailPaneVisibility();
        ApplyDetailSectionStates();
        SetActiveNavState();
        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _refreshTimer.Tick += async (_, _) => await ReloadAsync();
        AddHandler(InputElement.PointerWheelChangedEvent, GlobalPointerWheelChanged, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(InputElement.PointerPressedEvent, GlobalPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(InputElement.PointerMovedEvent, GlobalPointerMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(InputElement.PointerReleasedEvent, GlobalPointerReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
        ConfigureGrabScrollSurfaces();
        Opened += async (_, _) =>
        {
            _refreshTimer.Start();
            await ReloadAsync();
        };
        Closed += (_, _) => _refreshTimer.Stop();
    }

    private async Task ReloadAsync()
    {
        if (_isReloading)
        {
            return;
        }

        _isReloading = true;
        try
        {
        await _store.InitializeAsync();

        var activities = await _store.QueryAsync(new TimelineQuery { Limit = 1500 });
        _visibleActivities.Clear();
        var privacyFilter = CurrentPrivacyFilter();
        _visibleActivities.AddRange(activities
            .Where(activity => !ActivityExclusionFilter.IsExcluded(activity))
            .Where(activity => !privacyFilter.ShouldExclude(activity)));

        var dateFilters = TimelineDateFilterBuilder.Build(_visibleActivities, DateTime.Today);
        DateFilterComboBox.ItemsSource = dateFilters;
        DateFilterComboBox.SelectedItem ??= dateFilters.FirstOrDefault();

        await RefreshTimelineAsync();
        await RefreshOverviewAsync();
        }
        finally
        {
            _isReloading = false;
        }
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
            .Where(MatchesRailSelection)
            .Where(MatchesTimelineFocus)
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
        TimelineSubtitleText.Text = BuildTimelineSubtitle();

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

        OverviewDateText.Text = overview.DateLabel;
        OverviewLeadText.Text = overview.Sessions.Count == 0
            ? "No captured activity yet for this day."
            : $"{overview.Sessions.Count} moments captured across {overview.TopApplications.Count} applications with {OverviewPresentationBuilder.FormatDuration(overview.TotalActive)} active time.";
        OverviewSummaryItems.ItemsSource = overview.DaySummaryMetrics;
        OverviewHeatmapControl.Items = overview.HeatmapSegments;
        OverviewHeatmapControl.LegendItems = overview.Legend;
        OverviewMomentsItems.ItemsSource = overview.KeyMoments;
        OverviewCategoryBreakdownControl.Items = overview.CategoryBreakdown;
        OverviewTopApplicationsItems.ItemsSource = overview.TopApplications
            .Take(4)
            .Select(ToOverviewTopApplicationRow)
            .ToList();
        OverviewFocusRhythmControl.Items = overview.FocusBlocks.Take(10).ToList();
    }

    private void TimelineNavButton_Click(object? sender, RoutedEventArgs e)
    {
        _ = ActivateTimelineSelectionAsync(RailSelection.Timeline);
    }

    private void OverviewNavButton_Click(object? sender, RoutedEventArgs e)
    {
        ShowOverviewPage();
    }

    private void BrowsingNavButton_Click(object? sender, RoutedEventArgs e)
    {
        _ = ActivateTimelineSelectionAsync(RailSelection.Browsing);
    }

    private void DevelopmentNavButton_Click(object? sender, RoutedEventArgs e)
    {
        _ = ActivateTimelineSelectionAsync(RailSelection.Development);
    }

    private void CommunicationNavButton_Click(object? sender, RoutedEventArgs e)
    {
        _ = ActivateTimelineSelectionAsync(RailSelection.Communication);
    }

    private void DocumentsNavButton_Click(object? sender, RoutedEventArgs e)
    {
        _ = ActivateTimelineSelectionAsync(RailSelection.Documents);
    }

    private void SavedMomentsNavButton_Click(object? sender, RoutedEventArgs e)
    {
        _ = ActivateTimelineSelectionAsync(RailSelection.SavedMoments);
    }

    private void NotesNavButton_Click(object? sender, RoutedEventArgs e)
    {
        _ = ActivateTimelineSelectionAsync(RailSelection.Notes);
    }

    private void BookmarksNavButton_Click(object? sender, RoutedEventArgs e)
    {
        _ = ActivateTimelineSelectionAsync(RailSelection.Bookmarks);
    }

    private void AppsNavButton_Click(object? sender, RoutedEventArgs e)
    {
        _ = ActivateTimelineSelectionAsync(RailSelection.Apps);
    }

    private void WebsitesNavButton_Click(object? sender, RoutedEventArgs e)
    {
        _ = ActivateTimelineSelectionAsync(RailSelection.Websites);
    }

    private void FilesNavButton_Click(object? sender, RoutedEventArgs e)
    {
        _ = ActivateTimelineSelectionAsync(RailSelection.Files);
    }

    private async void SettingsNavButton_Click(object? sender, RoutedEventArgs e)
    {
        var settingsWindow = new SettingsWindow(
            _store,
            _recorder,
            _preferences,
            _preferences.TimelineViewMode,
            _preferences.Theme,
            SaveOpacity,
            SaveTimelineViewMode,
            SaveTheme,
            ReloadAsync)
        {
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        await settingsWindow.ShowDialog(this);
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
        SearchSurface.Background = new SolidColorBrush(_currentTheme.WithAlpha(_currentTheme.TopCommand, Math.Clamp(_preferences.Opacity + 0.08, 0.65, 1.0)));
        SearchSurface.BorderBrush = new SolidColorBrush(_currentTheme.Accent);
        SearchTextBox.Background = Brushes.Transparent;
        SearchIconText.Foreground = new SolidColorBrush(_currentTheme.Accent);
    }

    private void SearchTextBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        SearchSurface.Background = new SolidColorBrush(_currentTheme.WithAlpha(_currentTheme.TopCommand, Math.Clamp(_preferences.Opacity + 0.08, 0.65, 1.0)));
        SearchSurface.BorderBrush = new SolidColorBrush(_currentTheme.Stroke);
        SearchTextBox.Background = Brushes.Transparent;
        SearchIconText.Foreground = new SolidColorBrush(_currentTheme.SubtleText);
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
            visual is Button
                or TextBox
                or ComboBox
                or ScrollBar
                or Thumb
                or Track
                or Slider
                or ScrollViewer);
    }

    private async void DateFilterComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        ClearTimelineFocusRange();
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

    private void GlobalPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        var scrollViewer = GetScrollViewerUnderPointer(e);

        if (scrollViewer is null)
        {
            return;
        }

        ApplyWheelDelta(scrollViewer, e);
    }

    private void GlobalPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed is false)
        {
            return;
        }

        if (TryBeginWindowDrag(e))
        {
            return;
        }

        if (e.Source is Visual sourceVisual && IsScrollDragBlockedElement(sourceVisual))
        {
            return;
        }

        var scrollViewer = GetScrollViewerUnderPointer(e);
        if (scrollViewer is null)
        {
            return;
        }

        _pendingDragScrollViewer = scrollViewer;
        _scrollDragStartPoint = e.GetPosition(this);
        _scrollDragStartOffset = scrollViewer.Offset;
        _isScrollDragging = false;
    }

    private bool TryBeginWindowDrag(PointerPressedEventArgs e)
    {
        if (e.Source is not Visual sourceVisual
            || IsInteractiveChromeElement(sourceVisual)
            || !IsWindowChromeDragPoint(e))
        {
            return false;
        }

        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }
        else
        {
            BeginMoveDrag(e);
        }

        e.Handled = true;
        return true;
    }

    private bool IsWindowChromeDragPoint(PointerEventArgs e)
    {
        var point = e.GetPosition(this);
        return point.Y <= 84;
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

    private void ConfigureGrabScrollSurfaces()
    {
        foreach (var scrollViewer in GetGrabScrollSurfaces())
        {
            scrollViewer.Cursor = ScrollReadyCursor;
        }
    }

    private IEnumerable<ScrollViewer> GetGrabScrollSurfaces()
    {
        yield return TimelineScrollViewer;
        yield return OverviewDetailTabContent;
        yield return ActivityDetailTabContent;
        yield return RelatedDetailTabContent;
        yield return OverviewPage;
    }

    private static bool IsScrollDragBlockedElement(Visual sourceVisual)
    {
        return sourceVisual.GetSelfAndVisualAncestors().Any(visual =>
            visual is Button
                or TextBox
                or ComboBox
                or Slider
                or ScrollBar
                or Thumb
                or Track);
    }

    private ScrollViewer? GetScrollViewerUnderPointer(PointerEventArgs e)
    {
        return GetGrabScrollSurfaces()
            .Where(scrollViewer => scrollViewer.IsVisible)
            .Where(CanScrollVertically)
            .FirstOrDefault(scrollViewer => IsPointerInside(e, scrollViewer));
    }

    private static bool IsPointerInside(PointerEventArgs e, Control control)
    {
        var point = e.GetPosition(control);
        return point.X >= 0
            && point.Y >= 0
            && point.X <= control.Bounds.Width
            && point.Y <= control.Bounds.Height;
    }

    private static bool CanScrollVertically(ScrollViewer scrollViewer)
    {
        return scrollViewer.Extent.Height > scrollViewer.Viewport.Height + 0.5;
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

    private void TimelineMomentButton_Click(object? sender, RoutedEventArgs e)
    {
        var row = sender switch
        {
            Button { CommandParameter: MomentRow commandRow } => commandRow,
            Control { DataContext: MomentRow dataRow } => dataRow,
            _ => null
        };

        if (row is null)
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

    private async void OverviewMomentButton_Click(object? sender, RoutedEventArgs e)
    {
        if (!TryGetMomentId(sender, out var momentId))
        {
            return;
        }

        await NavigateToTimelineMomentAsync(momentId);
    }

    private void OverviewOpenMomentButton_Click(object? sender, RoutedEventArgs e)
    {
        if (!TryGetMomentId(sender, out var momentId))
        {
            return;
        }

        var selected = FindOverviewMoment(momentId);
        if (selected is null)
        {
            return;
        }

        OpenActivity(selected);
    }

    private async void OverviewPinMomentButton_Click(object? sender, RoutedEventArgs e)
    {
        if (!TryGetMomentId(sender, out var momentId))
        {
            return;
        }

        var selected = FindOverviewMoment(momentId);
        if (selected is null)
        {
            return;
        }

        await _store.SetPinnedAsync(selected.Id, !selected.IsPinned);
        await ReloadAsync();
    }

    private void ShowDetail(ActivityMomentViewModel selected)
    {
        var detail = _detailContextBuilder.Build(selected, _visibleActivities);
        DetailGlyphText.Text = selected.IconGlyph;
        DetailGlyphText.Foreground = Brush.Parse(selected.IconForeground);
        SetDetailIcon(selected);
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
        RefreshDetailSectionHeaders();
        SelectDetailTab(_selectedDetailTab);
    }

    private DateTime GetSelectedDate()
    {
        return DateFilterComboBox.SelectedItem is DateFilterOption option ? option.Date : DateTime.Today;
    }

    private void ClearDetailPanel()
    {
        DetailGlyphHost.Background = new SolidColorBrush(_currentTheme.Toolbar);
        DetailGlyphText.Text = "MT";
        DetailGlyphText.Foreground = new SolidColorBrush(_currentTheme.Accent);
        DetailGlyphText.IsVisible = true;
        DetailIconImage.Source = null;
        DetailIconImage.IsVisible = false;
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
        RefreshDetailSectionHeaders();
        SelectDetailTab(DetailTab.Overview);
    }

    private void TimelineGroupToggleButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { CommandParameter: TimelineGroupRow row })
        {
            return;
        }

        row.Group.ToggleExpanded();
        TimelineGroupsItems.ItemsSource = BuildTimelineGroupRows();
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

    private void DetailSectionToggleButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { CommandParameter: string sectionKey }
            || !_detailSectionExpanded.ContainsKey(sectionKey))
        {
            return;
        }

        _detailSectionExpanded[sectionKey] = !_detailSectionExpanded[sectionKey];
        ApplyDetailSectionState(sectionKey);
    }

    private void SetDetailTabState(TextBlock text, Border underline, bool isActive)
    {
        text.Foreground = new SolidColorBrush(isActive ? _currentTheme.Accent : _currentTheme.MutedText);
        text.FontWeight = isActive ? FontWeight.SemiBold : FontWeight.Regular;
        underline.IsVisible = isActive;
    }

    private void ApplyDetailSectionStates()
    {
        foreach (var sectionKey in _detailSectionExpanded.Keys)
        {
            ApplyDetailSectionState(sectionKey);
        }
    }

    private void ApplyDetailSectionState(string sectionKey)
    {
        var isExpanded = _detailSectionExpanded[sectionKey];
        var toggleText = isExpanded ? "Hide" : "Show";
        var toggleGlyph = isExpanded ? "⌃" : "⌄";

        switch (sectionKey)
        {
            case "OverviewNarrative":
                OverviewNarrativeSection.IsVisible = isExpanded;
                OverviewNarrativeToggleText.Text = toggleText;
                OverviewNarrativeToggleGlyph.Text = toggleGlyph;
                break;
            case "Preview":
                DetailPreviewSection.IsVisible = isExpanded;
                PreviewToggleText.Text = toggleText;
                PreviewToggleGlyph.Text = toggleGlyph;
                break;
            case "Metadata":
                MetadataSection.IsVisible = isExpanded;
                MetadataToggleText.Text = toggleText;
                MetadataToggleGlyph.Text = toggleGlyph;
                break;
            case "Note":
                NoteSection.IsVisible = isExpanded;
                NoteToggleText.Text = toggleText;
                NoteToggleGlyph.Text = toggleGlyph;
                break;
            case "RecentActivity":
                RecentActivitySection.IsVisible = isExpanded;
                RecentActivityToggleText.Text = toggleText;
                RecentActivityToggleGlyph.Text = toggleGlyph;
                break;
            case "CapturedActivity":
                CapturedActivitySection.IsVisible = isExpanded;
                CapturedActivityToggleText.Text = toggleText;
                CapturedActivityToggleGlyph.Text = toggleGlyph;
                break;
            case "RelatedMoments":
                RelatedMomentsSection.IsVisible = isExpanded;
                RelatedMomentsToggleText.Text = toggleText;
                RelatedMomentsToggleGlyph.Text = toggleGlyph;
                break;
        }
    }

    private void RefreshDetailSectionHeaders()
    {
        var rawCount = RawEntriesItems.ItemsSource?.Cast<object>().Count() ?? 0;
        var relatedCount = RelatedMomentsItems.ItemsSource?.Cast<object>().Count() ?? 0;

        CapturedActivityHeaderText.Text = rawCount == 0
            ? "Captured activity"
            : $"Captured activity · {rawCount} entr{(rawCount == 1 ? "y" : "ies")}";
        RelatedMomentsHeaderText.Text = relatedCount == 0
            ? "Related moments"
            : $"Related moments · {relatedCount}";
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
        button.Background = new SolidColorBrush(isActive ? _currentTheme.Accent : _currentTheme.Toolbar);
        button.BorderBrush = new SolidColorBrush(isActive ? _currentTheme.Accent : _currentTheme.PanelEdgeStrong);
        button.Foreground = new SolidColorBrush(isActive ? _currentTheme.AccentText : _currentTheme.Text);
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

    private void SetDetailIcon(ActivityMomentViewModel selected)
    {
        DetailIconImage.Source = null;
        DetailIconImage.IsVisible = false;
        DetailGlyphText.IsVisible = true;
        DetailIconImage.Stretch = Stretch.Uniform;

        var previewImage = ShouldPreferPreviewInDetailHeader(selected)
            ? GetPreviewImage(selected.ThumbnailPath) ?? GetPreviewImage(selected.ScreenshotPath)
            : null;
        if (previewImage is not null)
        {
            DetailGlyphHost.Background = new SolidColorBrush(_currentTheme.RaisedSurface);
            DetailIconImage.Source = previewImage;
            DetailIconImage.Stretch = Stretch.UniformToFill;
            DetailIconImage.IsVisible = true;
            DetailGlyphText.IsVisible = false;
            return;
        }

        var iconImage = GetIconImage(selected);
        if (iconImage is not null)
        {
            DetailGlyphHost.Background = new SolidColorBrush(_currentTheme.RaisedSurface);
            DetailIconImage.Source = iconImage;
            DetailIconImage.IsVisible = true;
            DetailGlyphText.IsVisible = false;
            return;
        }

        DetailGlyphHost.Background = Brush.Parse(selected.IconBackground);
    }

    private static bool ShouldPreferPreviewInDetailHeader(ActivityMomentViewModel selected)
    {
        return selected.ProcessName.Equals("ApplicationFrameHost", StringComparison.OrdinalIgnoreCase)
            || selected.ProcessName.Equals("ShellExperienceHost", StringComparison.OrdinalIgnoreCase)
            || selected.ProcessName.Equals("StartMenuExperienceHost", StringComparison.OrdinalIgnoreCase);
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
            WindowDecorations = Avalonia.Controls.WindowDecorations.None,
            Background = Brushes.Transparent
        };

        dialog.Content = new Border
        {
            Background = new SolidColorBrush(_currentTheme.Base),
            BorderBrush = new SolidColorBrush(_currentTheme.Stroke),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(22),
            Child = BuildDeleteDialogContent()
        };

        return await dialog.ShowDialog<bool>(this);

        StackPanel BuildDeleteDialogContent()
        {
            var deleteButton = new Button
            {
                Content = "Delete",
                Background = new SolidColorBrush(_currentTheme.DangerFill),
                BorderBrush = new SolidColorBrush(_currentTheme.Danger),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(9),
                Foreground = new SolidColorBrush(_currentTheme.Text),
                Padding = new Thickness(16, 9),
                MinWidth = 86,
                HorizontalContentAlignment = HorizontalAlignment.Center
            };
            var cancelButton = new Button
            {
                Content = "Cancel",
                Background = new SolidColorBrush(_currentTheme.Toolbar),
                BorderBrush = new SolidColorBrush(_currentTheme.PanelEdgeStrong),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(9),
                Foreground = new SolidColorBrush(_currentTheme.Text),
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
                        Text = "Delete this moment?",
                        Foreground = new SolidColorBrush(_currentTheme.Text),
                        FontSize = 20,
                        FontWeight = FontWeight.SemiBold
                    },
                    new TextBlock
                    {
                        Text = "This removes it from your local timeline and deletes cached MomentTrace previews for this moment.",
                        Foreground = new SolidColorBrush(_currentTheme.MutedText),
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

    private async Task ActivateTimelineSelectionAsync(RailSelection selection)
    {
        ClearTimelineFocusRange();
        _selectedRailSelection = selection;
        ShowTimelinePage();
        await RefreshTimelineAsync();
    }

    private void ShowTimelinePage()
    {
        TimelinePage.IsVisible = true;
        OverviewPage.IsVisible = false;
        SetActiveNavState();
    }

    private void ShowOverviewPage()
    {
        TimelinePage.IsVisible = false;
        OverviewPage.IsVisible = true;
        SetActiveNavState();
    }

    private string BuildTimelineSubtitle()
    {
        var focusSuffix = _timelineFocusStart.HasValue && _timelineFocusEnd.HasValue
            ? $" Filtered to {_timelineFocusStart.Value:HH:mm} - {_timelineFocusEnd.Value:HH:mm}."
            : string.Empty;

        if (_sessions.Count == 0)
        {
            return GetEmptyRailSelectionText() + focusSuffix;
        }

        return (_selectedRailSelection switch
        {
            RailSelection.Timeline => "Local moments grouped from the shared presentation layer",
            RailSelection.Browsing => "Browsing moments from websites and research sessions.",
            RailSelection.Development => "Development moments from code, tooling, and project windows.",
            RailSelection.Communication => "Communication moments from conversations, meetings, and collaboration tools.",
            RailSelection.Documents => "Document moments from files, notes, and reading surfaces.",
            RailSelection.SavedMoments => "Saved moments you pinned, bookmarked, or annotated.",
            RailSelection.Notes => "Moments with personal notes attached.",
            RailSelection.Bookmarks => "Bookmarked moments kept for later recall.",
            RailSelection.Apps => "Application-led moments across desktop and system surfaces.",
            RailSelection.Websites => "Website moments captured from browser activity.",
            RailSelection.Files => "File and document moments captured from document surfaces.",
            _ => "Local moments grouped from the shared presentation layer"
        }) + focusSuffix;
    }

    private string GetEmptyRailSelectionText()
    {
        return _selectedRailSelection switch
        {
            RailSelection.Timeline => "No activity captured for this day yet",
            RailSelection.Browsing => "No browsing moments matched this day",
            RailSelection.Development => "No development moments matched this day",
            RailSelection.Communication => "No communication moments matched this day",
            RailSelection.Documents => "No document moments matched this day",
            RailSelection.SavedMoments => "No saved moments matched this day",
            RailSelection.Notes => "No noted moments matched this day",
            RailSelection.Bookmarks => "No bookmarked moments matched this day",
            RailSelection.Apps => "No application moments matched this day",
            RailSelection.Websites => "No website moments matched this day",
            RailSelection.Files => "No file moments matched this day",
            _ => "No activity captured for this day yet"
        };
    }

    private bool MatchesRailSelection(ActivityMomentViewModel session)
    {
        return _selectedRailSelection switch
        {
            RailSelection.Timeline => true,
            RailSelection.Browsing => session.Category is ActivityCategory.Browsing or ActivityCategory.Research,
            RailSelection.Development => session.Category == ActivityCategory.Development,
            RailSelection.Communication => session.Category == ActivityCategory.Communication,
            RailSelection.Documents => session.Category == ActivityCategory.Documents,
            RailSelection.SavedMoments => session.IsPinned || session.IsBookmarked || session.HasNote,
            RailSelection.Notes => session.HasNote,
            RailSelection.Bookmarks => session.IsBookmarked,
            RailSelection.Apps => session.Activity.Kind is MomentTrace.Core.ActivityKind.App or MomentTrace.Core.ActivityKind.Development or MomentTrace.Core.ActivityKind.System,
            RailSelection.Websites => session.Activity.Kind == MomentTrace.Core.ActivityKind.Browser,
            RailSelection.Files => session.Activity.Kind == MomentTrace.Core.ActivityKind.Document,
            _ => true
        };
    }

    private bool MatchesTimelineFocus(ActivityMomentViewModel session)
    {
        if (!_timelineFocusStart.HasValue || !_timelineFocusEnd.HasValue)
        {
            return true;
        }

        var sessionStart = session.SessionStartedAt.LocalDateTime;
        var sessionEnd = session.SessionEndedAt.LocalDateTime;
        return sessionEnd > _timelineFocusStart.Value && sessionStart < _timelineFocusEnd.Value;
    }

    private void ClearTimelineFocusRange()
    {
        _timelineFocusStart = null;
        _timelineFocusEnd = null;
    }

    private void ClearSearchFilter()
    {
        _searchText = string.Empty;
        if (!string.IsNullOrEmpty(SearchTextBox.Text))
        {
            SearchTextBox.Text = string.Empty;
        }
    }

    private ActivityMomentViewModel? FindOverviewMoment(long momentId)
    {
        return _currentOverview?.Sessions.FirstOrDefault(session => session.Id == momentId)
            ?? _sessions.FirstOrDefault(session => session.Id == momentId);
    }

    private static bool TryGetMomentId(object? sender, out long momentId)
    {
        var candidate = sender switch
        {
            Button { CommandParameter: long id } => id,
            Control { DataContext: KeyMomentViewModel keyMoment } => keyMoment.Id,
            _ => 0
        };

        if (candidate <= 0)
        {
            momentId = 0;
            return false;
        }

        momentId = candidate;
        return true;
    }

    private async Task NavigateToTimelineMomentAsync(long momentId)
    {
        ClearTimelineFocusRange();
        ClearSearchFilter();
        _selectedRailSelection = RailSelection.Timeline;
        _selectedMomentId = momentId;
        ShowTimelinePage();
        await RefreshTimelineAsync();
    }

    private async Task ShowTimelineRangeAsync(DateTime start, DateTime end)
    {
        ClearSearchFilter();
        _selectedRailSelection = RailSelection.Timeline;
        _timelineFocusStart = start;
        _timelineFocusEnd = end;
        _selectedMomentId = null;
        ShowTimelinePage();
        await RefreshTimelineAsync();
    }

    private async Task HandleOverviewFocusBlockAsync(FocusBlockViewModel block)
    {
        if (block.MomentId is long momentId)
        {
            await NavigateToTimelineMomentAsync(momentId);
            return;
        }

        if (block.StartedAt is DateTimeOffset startedAt)
        {
            await ShowTimelineRangeAsync(startedAt.LocalDateTime, startedAt.LocalDateTime + block.Duration);
        }
    }

    private void SetActiveNavState()
    {
        var isOverview = OverviewPage.IsVisible;

        SetWorkspaceButtonState(TimelineNavButton, TimelineNavHighlight, !isOverview && _selectedRailSelection == RailSelection.Timeline);
        SetWorkspaceButtonState(OverviewNavButton, OverviewNavHighlight, isOverview);

        SetRailButtonState(BrowsingNavButton, !isOverview && _selectedRailSelection == RailSelection.Browsing);
        SetRailButtonState(DevelopmentNavButton, !isOverview && _selectedRailSelection == RailSelection.Development);
        SetRailButtonState(CommunicationNavButton, !isOverview && _selectedRailSelection == RailSelection.Communication);
        SetRailButtonState(DocumentsNavButton, !isOverview && _selectedRailSelection == RailSelection.Documents);
        SetRailButtonState(SavedMomentsNavButton, !isOverview && _selectedRailSelection == RailSelection.SavedMoments);
        SetRailButtonState(NotesNavButton, !isOverview && _selectedRailSelection == RailSelection.Notes);
        SetRailButtonState(BookmarksNavButton, !isOverview && _selectedRailSelection == RailSelection.Bookmarks);
        SetRailButtonState(AppsNavButton, !isOverview && _selectedRailSelection == RailSelection.Apps);
        SetRailButtonState(WebsitesNavButton, !isOverview && _selectedRailSelection == RailSelection.Websites);
        SetRailButtonState(FilesNavButton, !isOverview && _selectedRailSelection == RailSelection.Files);
    }

    private void SetWorkspaceButtonState(Button button, Border highlight, bool isActive)
    {
        highlight.Background = Brushes.Transparent;
        button.Background = isActive ? new SolidColorBrush(_currentTheme.ActiveSurface) : Brushes.Transparent;
        button.BorderBrush = isActive ? new SolidColorBrush(_currentTheme.ActiveEdge) : Brushes.Transparent;
        button.Foreground = new SolidColorBrush(isActive ? _currentTheme.AccentText : _currentTheme.MutedText);
        button.FontWeight = isActive ? FontWeight.SemiBold : FontWeight.Regular;
    }

    private void SetRailButtonState(Button button, bool isActive)
    {
        button.Background = isActive ? new SolidColorBrush(_currentTheme.ActiveSurface) : Brushes.Transparent;
        button.BorderBrush = isActive ? new SolidColorBrush(_currentTheme.ActiveEdge) : Brushes.Transparent;
        button.Foreground = new SolidColorBrush(isActive ? _currentTheme.AccentText : _currentTheme.MutedText);
        button.FontWeight = isActive ? FontWeight.SemiBold : FontWeight.Regular;
    }

    private void SaveOpacity(double opacity)
    {
        _preferences.Opacity = opacity;
        _preferences.Save();
        ApplySurfaceTransparency(opacity);
    }

    private void SaveTimelineViewMode(string timelineViewMode)
    {
        _preferences.TimelineViewMode = string.Equals(timelineViewMode, "List", StringComparison.OrdinalIgnoreCase)
            ? "List"
            : "Cards";
        _preferences.Save();
        ApplyTimelineViewMode(_preferences.TimelineViewMode);
    }

    private void SaveTheme(string theme)
    {
        _preferences.Theme = DesktopAppThemePalette.Find(theme).Key;
        _preferences.Save();
        ApplyTheme(_preferences.Theme);
    }

    private void ApplyTheme(string theme)
    {
        _currentTheme = DesktopAppThemePalette.Find(theme);
        _currentTheme.ApplyTo(this);
        ApplySurfaceTransparency(_preferences.Opacity);
        if (SearchTextBox.IsFocused)
        {
            SearchTextBox_GotFocus(SearchTextBox, new RoutedEventArgs());
        }
        else
        {
            SearchTextBox_LostFocus(SearchTextBox, new RoutedEventArgs());
        }
        SetActiveNavState();
        _ = RefreshTimelineAsync();
        _ = RefreshOverviewAsync();
    }

    private void ApplySurfaceTransparency(double opacity)
    {
        var pageStrength = Math.Clamp(opacity, 0.55, 1.0);
        var panelStrength = Math.Clamp(opacity + 0.08, 0.65, 1.0);

        OuterShell.Background = new SolidColorBrush(_currentTheme.WithAlpha(_currentTheme.Base, pageStrength));
        RailSurface.Background = new SolidColorBrush(_currentTheme.WithAlpha(_currentTheme.Rail, panelStrength));
        TopCommandSurface.Background = new SolidColorBrush(_currentTheme.WithAlpha(_currentTheme.TopCommand, panelStrength));
        TopCommandSurface.BorderBrush = new SolidColorBrush(_currentTheme.Stroke);
        DetailSurface.Background = new SolidColorBrush(_currentTheme.WithAlpha(_currentTheme.Detail, panelStrength));
        SearchSurface.Background = new SolidColorBrush(_currentTheme.WithAlpha(_currentTheme.TopCommand, panelStrength));
    }

    private void ApplyTimelineViewMode(string? viewMode)
    {
        _preferences.TimelineViewMode = string.Equals(viewMode, "List", StringComparison.OrdinalIgnoreCase) ? "List" : "Cards";
        _ = RefreshTimelineAsync();
    }

    private UserPrivacyFilter CurrentPrivacyFilter()
    {
        return new UserPrivacyFilter(_preferences.ToPrivacyFilterOptions());
    }

    private IReadOnlyList<TimelineGroupRow> BuildTimelineGroupRows()
    {
        return _timelineGroups
            .Select(group => new TimelineGroupRow(
                group,
                group.Name,
                group.Summary,
                group.TimeRange,
                group.AccentBrush,
                $"{group.VisibleActivities.Count} moment{(group.VisibleActivities.Count == 1 ? string.Empty : "s")}",
                group.ToggleText,
                group.ToggleGlyph,
                group.HasMoreThanPreviewCount,
                _preferences.TimelineViewMode == "Cards",
                _preferences.TimelineViewMode == "List",
                group.VisibleActivities
                    .Select(session => ToMomentRow(session, session.Id == _selectedMomentId))
                    .ToList()))
            .ToList();
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

    private MomentRow ToMomentRow(ActivityMomentViewModel session)
    {
        var previewImage = GetPreviewImage(session.ThumbnailPath) ?? GetPreviewImage(session.ScreenshotPath);
        var iconImage = GetIconImage(session);
        var hasPreview = previewImage is not null;
        var hasIcon = iconImage is not null;

        return new MomentRow(
            session.Id,
            session.Title,
            BuildCardSubtitle(session),
            $"{session.Location} · {session.AppName}",
            session.CardTime,
            session.SeenDuration,
            previewImage,
            iconImage,
            session.IconGlyph,
            session.IconBackground,
            session.IconForeground,
            session.KindLabel,
            session.KindBackground,
            session.KindForeground,
            session.SavedIndicatorText,
            session.HasSavedIndicator,
            false,
            ColorToHex(_currentTheme.RaisedSurface),
            ColorToHex(_currentTheme.Stroke),
            hasPreview,
            !hasPreview && hasIcon,
            hasIcon,
            !hasIcon,
            !hasPreview && !hasIcon);
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
            CardBackground = ColorToHex(_currentTheme.RaisedSurface),
            BorderBrush = isSelected ? ColorToHex(_currentTheme.Accent) : ColorToHex(_currentTheme.Stroke)
        };
    }

    private static string ColorToHex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

    private static Bitmap? GetPreviewImage(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            return new Bitmap(path);
        }
        catch
        {
            return null;
        }
    }

    private static Bitmap? GetIconImage(ActivityMomentViewModel session)
    {
        var path = AppIconProvider.GetIcon(session.ExecutablePath, session.AppName, session.ProcessName);
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            return new Bitmap(path);
        }
        catch
        {
            return null;
        }
    }

    private static OverviewTopApplicationRow ToOverviewTopApplicationRow(TopApplicationViewModel app)
    {
        var glyph = string.IsNullOrWhiteSpace(app.AppName) ? "?" : app.AppName[..1].ToUpperInvariant();
        var accent = app.Representative.Kind switch
        {
            MomentTrace.Core.ActivityKind.Browser => "#0EA5E9",
            MomentTrace.Core.ActivityKind.Development => "#22C55E",
            MomentTrace.Core.ActivityKind.Document => "#F59E0B",
            MomentTrace.Core.ActivityKind.System => "#64748B",
            _ => "#7C5CFF"
        };

        return new OverviewTopApplicationRow(
            app.AppName,
            app.Duration,
            glyph,
            accent,
            Math.Max(app.Percent * 160d, 12d));
    }

    private sealed record MomentRow(
        long Id,
        string Title,
        string Subtitle,
        string Meta,
        string TimeLabel,
        string SeenLabel,
        Bitmap? PreviewImage,
        Bitmap? IconImage,
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
        bool ShowPreviewImage = false,
        bool ShowHeroIcon = false,
        bool ShowSmallIcon = false,
        bool ShowSmallGlyph = true,
        bool ShowGlyphFallback = true);

    private sealed record TimelineGroupRow(
        TimelineSessionGroup Group,
        string Name,
        string Summary,
        string TimeRange,
        string AccentBrush,
        string CountLabel,
        string ToggleText,
        string ToggleGlyph,
        bool HasExpandablePreview,
        bool ShowCards,
        bool ShowList,
        IReadOnlyList<MomentRow> Moments);

    private sealed record OverviewTopApplicationRow(
        string AppName,
        string Duration,
        string Glyph,
        string AccentBrush,
        double BarWidth);

    private enum DetailTab
    {
        Overview,
        Activity,
        Related
    }

    private enum RailSelection
    {
        Timeline,
        Browsing,
        Development,
        Communication,
        Documents,
        SavedMoments,
        Notes,
        Bookmarks,
        Apps,
        Websites,
        Files
    }
}

file sealed class DelegateCommand<T> : ICommand where T : class
{
    private readonly Action<T> _execute;

    public DelegateCommand(Action<T> execute)
    {
        _execute = execute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter)
    {
        return parameter is T;
    }

    public void Execute(object? parameter)
    {
        if (parameter is T value)
        {
            _execute(value);
        }
    }
}
