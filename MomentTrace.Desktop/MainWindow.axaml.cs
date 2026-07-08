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
    private static readonly string StartupLogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MomentTrace",
        "startup-debug.log");
    private static readonly string[] KnownOpenableExtensions =
    [
        ".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp", ".tif", ".tiff", ".svg",
        ".pdf", ".txt", ".md", ".rtf", ".doc", ".docx", ".xls", ".xlsx", ".csv", ".tsv",
        ".ppt", ".pptx", ".json", ".xml", ".yml", ".yaml", ".cs", ".xaml", ".axaml",
        ".csproj", ".sln", ".sql", ".ps1", ".js", ".ts", ".tsx", ".html", ".css"
    ];
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
    private readonly Dictionary<string, bool> _timelineGroupExpansionStates = new(StringComparer.Ordinal);
    private OverviewPresentationModel? _currentOverview;
    private long? _selectedMomentId;
    private string _searchText = string.Empty;
    private bool _isDetailPaneVisible = true;
    private RailSelection _selectedRailSelection = RailSelection.Timeline;
    private WorkspaceNav _activeWorkspaceNav = WorkspaceNav.Timeline;
    private DateTime? _timelineFocusStart;
    private DateTime? _timelineFocusEnd;
    private DesktopAppThemePalette _currentTheme;
    private ScrollViewer? _pendingDragScrollViewer;
    private Point _scrollDragStartPoint;
    private Vector _scrollDragStartOffset;
    private bool _isScrollDragging;
    private bool _isReloading;
    private bool _reloadPending;
    private bool _isSelectingDateForOverviewNavigation;
    private DispatcherTimer? _overviewMomentsScrollAnimationTimer;
    private double _overviewMomentsScrollFrom;
    private double _overviewMomentsScrollTo;
    private readonly Stopwatch _overviewMomentsScrollClock = new();
    private Point _overviewMomentsDragStartPoint;
    private Vector _overviewMomentsDragStartOffset;
    private bool _isOverviewMomentsDragging;
    private string _overviewMomentsSignature = string.Empty;
    private string _overviewFocusSignature = string.Empty;
    private string _activityStoreSignature = string.Empty;
    private string _timelineRefreshSignature = string.Empty;
    private string _overviewRefreshSignature = string.Empty;
    private string _lastDetailSignature = string.Empty;
    private long? _lastDetailedMomentId;
    private readonly ICommand _overviewHeatmapCommand;
    private readonly ICommand _overviewFocusCommand;
    private static readonly Cursor ScrollReadyCursor = new(StandardCursorType.Hand);
    private static readonly Cursor ScrollDraggingCursor = new(StandardCursorType.SizeAll);
    private static readonly Thickness DefaultOuterShellPadding = new(20, 16, 8, 16);
    private static readonly Thickness DefaultRailSurfacePadding = new(14, 14, 14, 14);
    private static readonly Thickness DefaultTopCommandMargin = new(12, 0, 4, 0);
    private static readonly Thickness DefaultTopCommandPadding = new(8);
    private bool _isMacChromeMode;

    public MainWindow(SqliteActivityStore? store = null, IActivityRecorder? recorder = null)
    {
        StartupTrace("MainWindow: ctor enter");
        InitializeComponent();
        StartupTrace("MainWindow: InitializeComponent complete");
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
        SetActiveNavState();
        ConfigurePlatformChrome();
        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _refreshTimer.Tick += async (_, _) => await ReloadAsync();
        AddHandler(InputElement.PointerWheelChangedEvent, GlobalPointerWheelChanged, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(InputElement.PointerPressedEvent, GlobalPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(InputElement.PointerMovedEvent, GlobalPointerMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(InputElement.PointerReleasedEvent, GlobalPointerReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(InputElement.KeyDownEvent, MainWindow_KeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
        if (_recorder is not null)
        {
            _recorder.ActivityChanged += Recorder_ActivityChanged;
        }
        ConfigureGrabScrollSurfaces();
        Opened += async (_, _) =>
        {
            ApplyPlatformLayoutMetrics();
            ApplyOverviewResponsiveLayout();
            InitializeProfileRail();
            _refreshTimer.Start();
            await ReloadAsync();
        };
        Closed += (_, _) =>
        {
            _refreshTimer.Stop();
            if (_recorder is not null)
            {
                _recorder.ActivityChanged -= Recorder_ActivityChanged;
            }
        };
        SizeChanged += (_, _) => ApplyOverviewResponsiveLayout();
        StartupTrace("MainWindow: ctor complete");
    }

    private void Recorder_ActivityChanged(object? sender, TimelineActivity e)
    {
        Dispatcher.UIThread.Post(async () =>
        {
            _activityStoreSignature = string.Empty;
            await ReloadAsync();
        }, DispatcherPriority.Background);
    }

    private void ConfigurePlatformChrome()
    {
        _isMacChromeMode = OperatingSystem.IsMacOS();
        if (!_isMacChromeMode)
        {
            // Keep native resize hit-testing without drawing native caption
            // buttons over the custom shell controls.
            WindowDecorations = WindowDecorations.BorderOnly;
            ExtendClientAreaToDecorationsHint = true;
            ExtendClientAreaTitleBarHeightHint = 0;
            return;
        }

        WindowDecorations = WindowDecorations.Full;
        ExtendClientAreaToDecorationsHint = true;
        ExtendClientAreaTitleBarHeightHint = -1;

        MinimizeShellButton.IsVisible = false;
        MaximizeShellButton.IsVisible = false;
        CloseShellButton.IsVisible = false;

        ApplyPlatformLayoutMetrics();
    }

    private void ApplyPlatformLayoutMetrics()
    {
        if (!_isMacChromeMode)
        {
            OuterShell.Padding = DefaultOuterShellPadding;
            RailSurface.Padding = DefaultRailSurfacePadding;
            TopCommandSurface.Margin = DefaultTopCommandMargin;
            TopCommandSurface.Padding = DefaultTopCommandPadding;
            TopCommandSurface.BorderThickness = new Thickness(0);
            return;
        }

        var titleBarInset = Math.Max(WindowDecorationMargin.Top, 28);
        OuterShell.Padding = new Thickness(DefaultOuterShellPadding.Left, 0, DefaultOuterShellPadding.Right, DefaultOuterShellPadding.Bottom);
        RailSurface.Padding = new Thickness(
            DefaultRailSurfacePadding.Left,
            titleBarInset + 14,
            DefaultRailSurfacePadding.Right,
            DefaultRailSurfacePadding.Bottom);
        TopCommandSurface.Margin = new Thickness(DefaultTopCommandMargin.Left, 0, DefaultTopCommandMargin.Right, DefaultTopCommandMargin.Bottom);
        TopCommandSurface.Padding = new Thickness(
            DefaultTopCommandPadding.Left,
            titleBarInset + 4,
            DefaultTopCommandPadding.Right,
            DefaultTopCommandPadding.Bottom);
        TopCommandSurface.BorderThickness = new Thickness(0);
    }

    private void MainWindow_KeyDown(object? sender, KeyEventArgs e)
    {
        if (!_isMacChromeMode || (e.KeyModifiers & KeyModifiers.Meta) == 0)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.OemComma:
                OpenSettingsWindow();
                e.Handled = true;
                break;
            case Key.F:
                FocusSearch();
                e.Handled = true;
                break;
            case Key.D1:
                _activeWorkspaceNav = WorkspaceNav.Timeline;
                _ = ActivateTimelineSelectionAsync(RailSelection.Timeline);
                e.Handled = true;
                break;
            case Key.D2:
                _activeWorkspaceNav = WorkspaceNav.Overview;
                ShowOverviewPage();
                e.Handled = true;
                break;
            case Key.W:
                Close();
                e.Handled = true;
                break;
        }
    }

    private void FocusSearch()
    {
        SearchTextBox.Focus();
        SearchTextBox.SelectAll();
    }

    private void OpenSettingsWindow()
    {
        SettingsNavButton_Click(SettingsNavButton, new RoutedEventArgs());
    }

    private async Task ReloadAsync()
    {
        if (_isReloading)
        {
            _reloadPending = true;
            return;
        }

        _isReloading = true;
        try
        {
            do
            {
                _reloadPending = false;
                await _store.InitializeAsync();

                var activities = await _store.QueryAsync(new TimelineQuery { Limit = 1500 });
                var storeSignature = BuildStoreSignature(activities);
                if (string.Equals(storeSignature, _activityStoreSignature, StringComparison.Ordinal))
                {
                    continue;
                }

                _activityStoreSignature = storeSignature;
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
            while (_reloadPending);
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
        var shouldResetDetailTab = false;
        _sessions = _timelineBuilder.BuildSessions(
            _visibleActivities
                .Where(activity => activity.StartedAt.LocalDateTime.Date == selectedDate)
                .ToList())
            .Where(MatchesSearch)
            .Where(MatchesRailSelection)
            .Where(MatchesTimelineFocus)
            .ToList();
        _timelineGroups = _timelineBuilder.BuildGroups(_sessions);
        ApplyTimelineGroupExpansionState();
        if (_sessions.Count == 0)
        {
            _selectedMomentId = null;
        }
        else if (_selectedMomentId is null || _sessions.All(session => session.Id != _selectedMomentId.Value))
        {
            _selectedMomentId = _sessions[0].Id;
            shouldResetDetailTab = true;
        }

        EnsureSelectedMomentIsVisibleInTimeline();

        var refreshSignature = BuildTimelineRefreshSignature(selectedDate);
        var timelineChanged = !string.Equals(refreshSignature, _timelineRefreshSignature, StringComparison.Ordinal);
        if (timelineChanged)
        {
            TimelineGroupsItems.ItemsSource = BuildTimelineGroupRows();
            _timelineRefreshSignature = refreshSignature;
        }

        UpdateTimelineEmptyState();
        UpdateTimelineFocusBanner();
        UpdateRailNavSubtitles();
        TimelineCountText.Text = $"{_sessions.Count} moments on {TimelineDateFilterBuilder.FormatDateLabel(selectedDate, DateTime.Today)}";
        TimelineSubtitleText.Text = BuildTimelineSubtitle();

        if (_sessions.Count > 0 && _selectedMomentId is long selectedId)
        {
            var selected = _sessions.FirstOrDefault(session => session.Id == selectedId) ?? _sessions[0];
            if (_isDetailPaneVisible)
            {
                var detailSignature = BuildDetailSignature(selected);
                if (_lastDetailedMomentId != selected.Id
                    || !string.Equals(detailSignature, _lastDetailSignature, StringComparison.Ordinal))
                {
                    ShowDetail(selected);
                    _lastDetailedMomentId = selected.Id;
                    _lastDetailSignature = detailSignature;
                }

                if (timelineChanged && shouldResetDetailTab)
                {
                    ScrollSelectedMomentIntoView();
                }
            }
        }
        else
        {
            ClearDetailPanel();
            CloseDetailPane();
        }
    }

    private async Task RefreshOverviewAsync()
    {
        await Task.Yield();

        var selectedDate = GetSelectedDate();
        _currentOverview = _overviewBuilder.Build(_visibleActivities, selectedDate);
        var overview = _currentOverview;
        var overviewRefreshSignature = BuildOverviewRefreshSignature(overview, selectedDate);
        if (string.Equals(overviewRefreshSignature, _overviewRefreshSignature, StringComparison.Ordinal))
        {
            return;
        }

        _overviewRefreshSignature = overviewRefreshSignature;
        var overviewMomentRows = BuildOverviewMomentRows(overview.KeyMoments).ToList();
        var topApplicationRows = overview.TopApplications
            .Take(4)
            .Select(ToOverviewTopApplicationRow)
            .ToList();
        var focusBlocks = overview.FocusBlocks.Take(16).ToList();

        OverviewDateText.Text = overview.DateLabel;
        OverviewLeadText.Text = BuildOverviewLeadText(overview);
        ApplyOverviewTimelineContextState();
        OverviewSummaryItems.ItemsSource = overview.DaySummaryMetrics;
        OverviewHeatmapControl.Items = overview.HeatmapSegments;
        OverviewHeatmapControl.LegendItems = overview.Legend;
        var momentsSignature = BuildOverviewMomentsSignature(overview.KeyMoments);
        var focusSignature = BuildOverviewFocusSignature(overview.FocusBlocks);
        var shouldResetMomentsRail = !string.Equals(momentsSignature, _overviewMomentsSignature, StringComparison.Ordinal);
        var shouldResetFocusRail = !string.Equals(focusSignature, _overviewFocusSignature, StringComparison.Ordinal);
        _overviewMomentsSignature = momentsSignature;
        _overviewFocusSignature = focusSignature;

        OverviewMomentsItems.ItemsSource = overviewMomentRows;
        OverviewCategoryBreakdownControl.Items = overview.CategoryBreakdown;
        OverviewTopApplicationsItems.ItemsSource = topApplicationRows;
        OverviewFocusRhythmControl.Items = focusBlocks;
        ApplyOverviewState(overviewMomentRows, topApplicationRows, focusBlocks, overview.Sessions.Count == 0);
        ResetOverviewRailOffsets(shouldResetMomentsRail, shouldResetFocusRail);
    }

    private void TimelineNavButton_Click(object? sender, RoutedEventArgs e)
    {
        _activeWorkspaceNav = WorkspaceNav.Timeline;
        _ = ActivateTimelineSelectionAsync(RailSelection.Timeline);
    }

    private void OverviewNavButton_Click(object? sender, RoutedEventArgs e)
    {
        _activeWorkspaceNav = WorkspaceNav.Overview;
        ShowOverviewPage();
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

    private void ApplyDetailPaneVisibility()
    {
        DetailSurface.IsVisible = _isDetailPaneVisible;
        DetailScrim.IsVisible = _isDetailPaneVisible;
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
        SearchSurface.Background = new SolidColorBrush(_currentTheme.WithAlpha(_currentTheme.TopCommand, Math.Clamp(_preferences.Opacity + 0.08, 0.0, 1.0)));
        SearchSurface.BorderBrush = new SolidColorBrush(_currentTheme.Accent);
        SearchTextBox.Background = Brushes.Transparent;
        SearchIconText.Foreground = new SolidColorBrush(_currentTheme.AccentText);
    }

    private void SearchTextBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        SearchSurface.Background = new SolidColorBrush(_currentTheme.WithAlpha(_currentTheme.TopCommand, Math.Clamp(_preferences.Opacity + 0.08, 0.0, 1.0)));
        SearchSurface.BorderBrush = new SolidColorBrush(_currentTheme.Stroke);
        SearchTextBox.Background = Brushes.Transparent;
        SearchIconText.Foreground = new SolidColorBrush(_currentTheme.SubtleText);
    }

    private void CloseDetailPane()
    {
        _isDetailPaneVisible = false;
        ApplyDetailPaneVisibility();
    }

    private void CloseDetailButton_Click(object? sender, RoutedEventArgs e) => CloseDetailPane();

    private void ToolbarDragRegion_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_isMacChromeMode)
        {
            return;
        }

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

        if (_isSelectingDateForOverviewNavigation)
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
        ApplyOverviewTimelineContextState();

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
        _isDetailPaneVisible = true;
        ApplyDetailPaneVisibility();
        _timelineRefreshSignature = string.Empty;
        TimelineGroupsItems.ItemsSource = BuildTimelineGroupRows();
        _timelineRefreshSignature = BuildTimelineRefreshSignature(GetSelectedDate());
        ShowDetail(selected);
        _lastDetailedMomentId = selected.Id;
        _lastDetailSignature = BuildDetailSignature(selected);
        ScrollSelectedMomentIntoView();
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
        await RefreshAfterLocalMutationAsync(selected.Id);
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
        DetailMetaSourceText.Text = FormatDetailSource(selected.TechnicalSource);
        ToolTip.SetTip(DetailMetaSourceText, selected.TechnicalSource);
        DetailMetaStartedText.Text = FormatDetailTimestamp(selected.SessionStartedAt.LocalDateTime);
        DetailMetaEndedText.Text = FormatDetailTimestamp(selected.SessionEndedAt.LocalDateTime);
        DetailMetaCategoryText.Text = selected.CategoryLabel;
        DetailNarrativeText.Text = detail.Narrative;
        ActivityTabSummaryText.Text = detail.RawEntriesSummary;
        RelatedSummaryText.Text = detail.RelatedSummary;
        RawEntriesItems.ItemsSource = detail.RawEntries;
        RelatedMomentsItems.ItemsSource = detail.RelatedMoments;
        CapturedActivitySection.IsVisible = detail.RawEntries.Count > 0;
        RelatedMomentsSection.IsVisible = detail.RelatedMoments.Count > 0;
        PinMomentButtonText.Text = selected.IsPinned ? "Pinned" : "Pin";
        BookmarkMomentButtonText.Text = selected.IsBookmarked ? "Bookmarked" : "Bookmark";
        MomentNoteTextBox.Text = selected.Note ?? string.Empty;
        SetActionButtonActive(PinMomentButton, selected.IsPinned);
        SetActionButtonActive(BookmarkMomentButton, selected.IsBookmarked);
        SetDetailActionsEnabled(true);
        SetDetailPreview(selected);
        RefreshDetailSectionHeaders();
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
        DetailSubtitleText.Text = "Click a moment to inspect it";
        DetailMetaSpanText.Text = "Local only";
        DetailMetaSourceText.Text = "MomentTrace";
        DetailMetaStartedText.Text = "Local only";
        DetailMetaEndedText.Text = "Local only";
        DetailMetaCategoryText.Text = "Moment";
        DetailNarrativeText.Text = "Select a moment to inspect its grouped raw activity and nearby related work.";
        ActivityTabSummaryText.Text = "Choose a moment to inspect the raw entries grouped into it.";
        RelatedSummaryText.Text = "Choose a moment to see nearby activity from the same source or category.";
        RawEntriesItems.ItemsSource = Array.Empty<DetailActivityRowViewModel>();
        RelatedMomentsItems.ItemsSource = Array.Empty<RelatedMomentViewModel>();
        CapturedActivitySection.IsVisible = false;
        RelatedMomentsSection.IsVisible = false;
        PinMomentButtonText.Text = "Pin";
        BookmarkMomentButtonText.Text = "Bookmark";
        MomentNoteTextBox.Text = string.Empty;
        SetActionButtonActive(PinMomentButton, false);
        SetActionButtonActive(BookmarkMomentButton, false);
        SetDetailActionsEnabled(false);
        DetailPreviewImage.Source = null;
        DetailPreviewHost.IsVisible = false;
        RefreshDetailSectionHeaders();
    }

    private void TimelineGroupToggleButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { CommandParameter: TimelineGroupRow row })
        {
            return;
        }

        row.Group.ToggleExpanded();
        _timelineGroupExpansionStates[row.Group.ExpansionKey] = row.Group.IsExpanded;
        TimelineGroupsItems.ItemsSource = BuildTimelineGroupRows();
    }

    private void RefreshDetailSectionHeaders()
    {
        var rawCount = RawEntriesItems.ItemsSource?.Cast<object>().Count() ?? 0;
        var relatedCount = RelatedMomentsItems.ItemsSource?.Cast<object>().Count() ?? 0;

        OverviewNarrativeHeaderText.Text = "Summary";
        DetailPreviewSection.IsVisible = DetailPreviewHost.IsVisible;
        MetadataHeaderText.Text = "Details";
        NoteHeaderText.Text = "Note";
        CapturedActivityHeaderText.Text = rawCount == 0
            ? "Captured activity"
            : $"Captured activity ({rawCount})";
        RelatedMomentsHeaderText.Text = relatedCount == 0
            ? "Related"
            : $"Related ({relatedCount})";
    }

    private static string FormatDetailTimestamp(DateTime timestamp) =>
        timestamp.ToString("ddd d MMM, HH:mm:ss");

    private static string FormatDetailSource(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return "MomentTrace";
        }

        var fileName = Path.GetFileName(source.Trim());
        return string.IsNullOrWhiteSpace(fileName) ? source : fileName;
    }

    private ActivityMomentViewModel? GetSelectedMoment()
    {
        if (_selectedMomentId is not long selectedId)
        {
            return null;
        }

        return _sessions.FirstOrDefault(session => session.Id == selectedId);
    }

    private void EnsureSelectedMomentIsVisibleInTimeline()
    {
        if (_selectedMomentId is not long selectedId)
        {
            return;
        }

        var containingGroup = _timelineGroups.FirstOrDefault(group => group.ContainsActivity(selectedId));
        if (containingGroup is null || containingGroup.IsActivityVisibleInPreview(selectedId))
        {
            return;
        }

        containingGroup.SetExpanded(true);
        _timelineGroupExpansionStates[containingGroup.ExpansionKey] = true;
    }

    private void ApplyTimelineGroupExpansionState()
    {
        foreach (var group in _timelineGroups)
        {
            if (_timelineGroupExpansionStates.TryGetValue(group.ExpansionKey, out var isExpanded))
            {
                group.SetExpanded(isExpanded);
            }
        }
    }

    private async void PinMomentButton_Click(object? sender, RoutedEventArgs e)
    {
        var selected = GetSelectedMoment();
        if (selected is null)
        {
            return;
        }

        await _store.SetPinnedAsync(selected.Id, !selected.IsPinned);
        await RefreshAfterLocalMutationAsync(selected.Id);
    }

    private async void BookmarkMomentButton_Click(object? sender, RoutedEventArgs e)
    {
        var selected = GetSelectedMoment();
        if (selected is null)
        {
            return;
        }

        await _store.SetBookmarkedAsync(selected.Id, !selected.IsBookmarked);
        await RefreshAfterLocalMutationAsync(selected.Id);
    }

    private async void SaveNoteButton_Click(object? sender, RoutedEventArgs e)
    {
        var selected = GetSelectedMoment();
        if (selected is null)
        {
            return;
        }

        await _store.SetNoteAsync(selected.Id, MomentNoteTextBox.Text);
        await RefreshAfterLocalMutationAsync(selected.Id);
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
        await RefreshAfterLocalMutationAsync(null);
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

    private void SetDetailActionsEnabled(bool isEnabled)
    {
        OpenMomentButton.IsEnabled = isEnabled;
        PinMomentButton.IsEnabled = isEnabled;
        BookmarkMomentButton.IsEnabled = isEnabled;
        DeleteMomentButton.IsEnabled = isEnabled;
        MomentNoteTextBox.IsReadOnly = !isEnabled;
        SaveNoteButton.IsEnabled = isEnabled;
        OpenMomentButton.Opacity = isEnabled ? 1 : 0.45;
        PinMomentButton.Opacity = isEnabled ? 1 : 0.45;
        BookmarkMomentButton.Opacity = isEnabled ? 1 : 0.45;
        DeleteMomentButton.Opacity = isEnabled ? 1 : 0.45;
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

    private void OpenActivity(ActivityMomentViewModel activity)
    {
        try
        {
            var candidate = ResolveBestOpenActivity(activity);

            if (TryOpenMacApplication(candidate))
            {
                return;
            }

            var target = GetOpenTarget(candidate);
            if (string.IsNullOrWhiteSpace(target))
            {
                return;
            }

            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch
        {
            // Keep the shell calm if Windows cannot reopen the captured target.
        }
    }

    private TimelineActivity ResolveBestOpenActivity(ActivityMomentViewModel selected)
    {
        var candidates = _visibleActivities
            .Where(activity => IsCandidateForOpen(selected, activity))
            .OrderByDescending(activity => ScoreOpenCandidate(selected, activity))
            .ThenByDescending(activity => activity.StartedAt)
            .ToList();

        return candidates.FirstOrDefault() ?? selected.Activity;
    }

    private static bool IsCandidateForOpen(ActivityMomentViewModel selected, TimelineActivity activity)
    {
        if (!string.Equals(activity.ProcessName, selected.ProcessName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return activity.StartedAt.LocalDateTime.Date == selected.SessionStartedAt.LocalDateTime.Date
            && activity.StartedAt >= selected.SessionStartedAt.AddMinutes(-1)
            && activity.StartedAt <= selected.SessionEndedAt.AddMinutes(1);
    }

    private static int ScoreOpenCandidate(ActivityMomentViewModel selected, TimelineActivity activity)
    {
        var score = 0;

        if ((selected.Category == ActivityCategory.Browsing || selected.Category == ActivityCategory.Research)
            && IsWebUrl(activity.Resource))
        {
            score += 100;
        }

        if (TryGetBundleIdentifier(activity.Resource) is not null)
        {
            score += 80;
        }

        if (!string.IsNullOrWhiteSpace(activity.BundleIdentifier))
        {
            score += 40;
        }

        if (!string.IsNullOrWhiteSpace(activity.ExecutablePath))
        {
            score += 20;
        }

        if (!string.IsNullOrWhiteSpace(activity.Resource)
            && !activity.Resource.StartsWith("pid:", StringComparison.OrdinalIgnoreCase))
        {
            score += 10;
        }

        if (!string.IsNullOrWhiteSpace(activity.WindowTitle)
            && string.Equals(activity.WindowTitle, selected.Activity.WindowTitle, StringComparison.Ordinal))
        {
            score += 5;
        }

        return score;
    }

    private static string? GetOpenTarget(TimelineActivity activity)
    {
        if (IsWebUrl(activity.Resource))
        {
            return activity.Resource!;
        }

        if (TryResolveLocalPath(activity, out var localPath))
        {
            return localPath;
        }

        if (LooksLikeExplorerMoment(activity))
        {
            return null;
        }

        return !string.IsNullOrWhiteSpace(activity.ExecutablePath)
            ? activity.ExecutablePath
            : activity.ProcessName;
    }

    private static bool TryOpenMacApplication(TimelineActivity activity)
    {
        if (!OperatingSystem.IsMacOS())
        {
            return false;
        }

        var bundleIdentifier = TryGetBundleIdentifier(activity.Resource);
        if (string.IsNullOrWhiteSpace(bundleIdentifier))
        {
            return false;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "/usr/bin/open",
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("-b");
        startInfo.ArgumentList.Add(bundleIdentifier);

        using var process = Process.Start(startInfo);
        return process is not null;
    }

    private static string? TryGetBundleIdentifier(string? resource)
    {
        if (string.IsNullOrWhiteSpace(resource)
            || !resource.StartsWith("app://", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return resource["app://".Length..].Trim();
    }

    private static bool TryResolveLocalPath(TimelineActivity activity, out string path)
    {
        path = string.Empty;

        if (TryUseExistingPath(activity.Resource, out path))
        {
            return true;
        }

        foreach (var candidate in EnumerateWindowPathCandidates(activity))
        {
            if (TryUseExistingPath(candidate, out path))
            {
                return true;
            }

            if (TryResolvePathFromCandidate(candidate, activity, out path))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryResolvePathFromCandidate(string? candidate, TimelineActivity activity, out string path)
    {
        path = string.Empty;

        var fileName = TryExtractFileName(candidate);
        if (!string.IsNullOrWhiteSpace(fileName))
        {
            foreach (var root in GetOpenSearchRoots())
            {
                try
                {
                    var directPath = Path.Combine(root, fileName);
                    if (File.Exists(directPath))
                    {
                        path = directPath;
                        return true;
                    }

                    var match = Directory.EnumerateFiles(root, fileName, SearchOption.AllDirectories).FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(match))
                    {
                        path = match;
                        return true;
                    }
                }
                catch
                {
                    // Ignore inaccessible folders and keep trying likely roots.
                }
            }
        }

        if (!LooksLikeExplorerMoment(activity))
        {
            return false;
        }

        var folderName = TryExtractFolderName(candidate);
        if (string.IsNullOrWhiteSpace(folderName))
        {
            return false;
        }

        foreach (var root in GetOpenSearchRoots())
        {
            try
            {
                var directPath = Path.Combine(root, folderName);
                if (Directory.Exists(directPath))
                {
                    path = directPath;
                    return true;
                }

                var match = Directory.EnumerateDirectories(root, folderName, SearchOption.AllDirectories).FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(match))
                {
                    path = match;
                    return true;
                }
            }
            catch
            {
                // Ignore inaccessible folders and keep trying likely roots.
            }
        }

        return false;
    }

    private static IEnumerable<string?> EnumerateWindowPathCandidates(TimelineActivity activity)
    {
        yield return activity.Resource;
        yield return activity.WindowTitle;
        yield return activity.DisplayTitle;
    }

    private static bool LooksLikeExplorerMoment(TimelineActivity activity)
    {
        return activity.ProcessName.Equals("explorer", StringComparison.OrdinalIgnoreCase)
            || activity.AppName.Contains("Explorer", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryUseExistingPath(string? candidate, out string path)
    {
        path = string.Empty;
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        var trimmed = candidate.Trim().Trim('"');
        try
        {
            if (Path.IsPathRooted(trimmed) && (File.Exists(trimmed) || Directory.Exists(trimmed)))
            {
                path = trimmed;
                return true;
            }
        }
        catch
        {
        }

        return false;
    }

    private static string? TryExtractFileName(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return null;
        }

        var parts = candidate
            .Split([" - ", " – ", " — ", " | "], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Append(candidate);

        foreach (var part in parts)
        {
            foreach (var extension in KnownOpenableExtensions)
            {
                var index = part.LastIndexOf(extension, StringComparison.OrdinalIgnoreCase);
                if (index < 0)
                {
                    continue;
                }

                var value = part[..(index + extension.Length)].Trim().Trim('"');
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                var fileName = Path.GetFileName(value);
                if (!string.IsNullOrWhiteSpace(fileName))
                {
                    return fileName;
                }
            }
        }

        return null;
    }

    private static string? TryExtractFolderName(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return null;
        }

        var parts = candidate
            .Split([" - ", " – ", " — ", " | "], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var part in parts)
        {
            var trimmed = part.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                continue;
            }

            if (trimmed.Contains(Path.DirectorySeparatorChar)
                || trimmed.Contains(Path.AltDirectorySeparatorChar))
            {
                var name = Path.GetFileName(trimmed.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                if (!string.IsNullOrWhiteSpace(name))
                {
                    return name;
                }
            }

            if (trimmed.Equals("File Explorer", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (Path.HasExtension(trimmed))
            {
                continue;
            }

            return trimmed;
        }

        return null;
    }

    private static IEnumerable<string> GetOpenSearchRoots()
    {
        var roots = new List<string?>
        {
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads")
        };

        var oneDrive = Environment.GetEnvironmentVariable("OneDrive");
        if (!string.IsNullOrWhiteSpace(oneDrive))
        {
            roots.Add(oneDrive);
            roots.Add(Path.Combine(oneDrive, "Pictures"));
            roots.Add(Path.Combine(oneDrive, "Desktop"));
            roots.Add(Path.Combine(oneDrive, "Documents"));
            roots.Add(Path.Combine(oneDrive, "Downloads"));
        }

        return roots
            .Where(root => !string.IsNullOrWhiteSpace(root) && Directory.Exists(root))
            .Distinct(StringComparer.OrdinalIgnoreCase)!;
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
        if (selection != RailSelection.Timeline)
        {
            _activeWorkspaceNav = WorkspaceNav.None;
        }

        ShowTimelinePage();
        ApplyOverviewTimelineContextState();
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
        _isDetailPaneVisible = false;
        ApplyDetailPaneVisibility();
        TimelinePage.IsVisible = false;
        OverviewPage.IsVisible = true;
        ApplyOverviewTimelineContextState();
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

    private void UpdateTimelineEmptyState()
    {
        if (_sessions.Count > 0)
        {
            TimelineEmptyState.IsVisible = false;
            TimelineScrollViewer.IsVisible = true;
            return;
        }

        var title = GetEmptyRailSelectionText();
        var detail = BuildTimelineEmptyDetail();
        ShowTimelineStatusState(title, detail, GetTimelineEmptyActionLabel());
    }

    private string BuildTimelineEmptyDetail()
    {
        if (!string.IsNullOrWhiteSpace(_searchText))
        {
            return $"No moments matched \"{_searchText}\". Try clearing search or changing the current rail filter.";
        }

        if (_timelineFocusStart.HasValue && _timelineFocusEnd.HasValue)
        {
            return $"No moments fell inside {_timelineFocusStart.Value:HH:mm} - {_timelineFocusEnd.Value:HH:mm}. Clear the focused time block or pick a different range.";
        }

        return _selectedRailSelection switch
        {
            RailSelection.Timeline => "Captured activity will appear here once MomentTrace records local moments for this day.",
            RailSelection.Browsing => "Try another date or switch back to All activity to see browsing moments.",
            RailSelection.Development => "No coding, tooling, or terminal moments matched this date and filter combination.",
            RailSelection.Communication => "No conversation, meeting, or collaboration moments matched this view.",
            RailSelection.Documents => "No file, note, or document moments matched this view.",
            RailSelection.SavedMoments => "Pinned, bookmarked, and noted moments will collect here once you save them.",
            RailSelection.Notes => "Moments with personal notes will appear here after you save one.",
            RailSelection.Bookmarks => "Bookmarked moments will appear here once you mark activity for later.",
            RailSelection.Apps => "Application-led moments will appear here when desktop or system surfaces are captured.",
            RailSelection.Websites => "Website moments will appear here when browser activity is captured for this date.",
            RailSelection.Files => "File and document moments will appear here when document surfaces are captured.",
            _ => "Captured activity will appear here once local moments are available."
        };
    }

    private string? GetTimelineEmptyActionLabel()
    {
        if (!string.IsNullOrWhiteSpace(_searchText))
        {
            return "Clear search";
        }

        if (_timelineFocusStart.HasValue && _timelineFocusEnd.HasValue)
        {
            return "Clear focus range";
        }

        if (_selectedRailSelection != RailSelection.Timeline)
        {
            return "Show all activity";
        }

        return GetSelectedDate() != DateTime.Today
            ? "Jump to today"
            : null;
    }

    private void ShowTimelineStatusState(string title, string detail, string? actionLabel)
    {
        TimelineEmptyTitleText.Text = title;
        TimelineEmptyDetailText.Text = detail;
        TimelineEmptyActionButtonText.Text = actionLabel ?? string.Empty;
        TimelineEmptyActionButton.IsVisible = !string.IsNullOrWhiteSpace(actionLabel);
        TimelineEmptyState.IsVisible = true;
        TimelineScrollViewer.IsVisible = false;
    }

    private void UpdateTimelineFocusBanner()
    {
        if (!_timelineFocusStart.HasValue || !_timelineFocusEnd.HasValue)
        {
            TimelineFocusBanner.IsVisible = false;
            return;
        }

        TimelineFocusBannerText.Text =
            $"Showing moments between {_timelineFocusStart.Value:HH:mm} and {_timelineFocusEnd.Value:HH:mm}. Clear focus to return to the full day.";
        TimelineFocusBanner.IsVisible = true;
    }

    private async void TimelineFocusClearButton_Click(object? sender, RoutedEventArgs e)
    {
        ClearTimelineFocusRange();
        ApplyOverviewTimelineContextState();
        _timelineRefreshSignature = string.Empty;
        await RefreshTimelineAsync();
    }

    private static string BuildStoreSignature(IReadOnlyList<TimelineActivity> activities)
    {
        if (activities.Count == 0)
        {
            return "empty";
        }

        return string.Join("|",
            activities.Count,
            string.Join(",",
                activities.Select(activity =>
                    $"{activity.Id}:{activity.StartedAt.UtcTicks}:{activity.EndedAt.UtcTicks}:{activity.IsPinned}:{activity.IsBookmarked}:{activity.Note}")));
    }

    private void InvalidatePresentationCaches()
    {
        _activityStoreSignature = string.Empty;
        _timelineRefreshSignature = string.Empty;
        _overviewRefreshSignature = string.Empty;
        _overviewMomentsSignature = string.Empty;
        _overviewFocusSignature = string.Empty;
        _lastDetailSignature = string.Empty;
        _lastDetailedMomentId = null;
    }

    private async Task RefreshAfterLocalMutationAsync(long? selectedMomentId)
    {
        _selectedMomentId = selectedMomentId;
        InvalidatePresentationCaches();
        await ReloadAsync();
    }

    private string BuildTimelineRefreshSignature(DateTime selectedDate)
    {
        return string.Join("|",
            selectedDate.ToString("yyyyMMdd"),
            _searchText,
            _selectedRailSelection,
            _timelineFocusStart?.Ticks,
            _timelineFocusEnd?.Ticks,
            _preferences.TimelineViewMode,
            _selectedMomentId,
            string.Join(",", _timelineGroups.Select(group => $"{group.ExpansionKey}:{group.IsExpanded}")),
            string.Join(",", _sessions.Select(BuildMomentMutationSignature)));
    }

    private static string BuildDetailSignature(ActivityMomentViewModel session)
    {
        return string.Join("|",
            session.Id,
            session.SessionEndedAt.UtcTicks,
            session.SeenDuration,
            session.IsPinned,
            session.IsBookmarked,
            session.Note,
            session.Title,
            session.MemoryDescription);
    }

    private static string BuildOverviewRefreshSignature(OverviewPresentationModel overview, DateTime selectedDate)
    {
        return string.Join("|",
            selectedDate.ToString("yyyyMMdd"),
            overview.TotalActive.Ticks,
            overview.Sessions.Count,
            string.Join(",", overview.Sessions.Select(BuildMomentMutationSignature)),
            string.Join(",", overview.DaySummaryMetrics.Select(metric => $"{metric.Label}:{metric.Value}:{metric.Detail}")),
            string.Join(",", overview.KeyMoments.Select(moment => $"{moment.Id}:{moment.IsPinned}:{moment.Title}:{moment.Subtitle}:{moment.Duration}")),
            string.Join(",", overview.TopApplications.Select(app => $"{app.AppName}:{app.Duration}")),
            string.Join(",", overview.CategoryBreakdown.Select(item => $"{item.Label}:{item.Duration.Ticks}")),
            string.Join(",", overview.HeatmapSegments.Select(segment => segment.Intensity.ToString("F2"))),
            string.Join(",", overview.FocusBlocks.Select(block => $"{block.MomentId}:{block.Duration.TotalSeconds:F0}")));
    }

    private static string BuildMomentMutationSignature(ActivityMomentViewModel session)
    {
        return $"{session.Id}:{session.SessionEndedAt.UtcTicks}:{session.SeenDuration}:{session.IsPinned}:{session.IsBookmarked}:{session.Note}";
    }

    private void ScrollSelectedMomentIntoView()
    {
        if (_selectedMomentId is not long selectedId)
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            var groupIndex = _timelineGroups.ToList().FindIndex(group => group.ContainsActivity(selectedId));
            if (groupIndex < 0)
            {
                return;
            }

            var estimatedOffset = groupIndex * 228d;
            var maxOffset = Math.Max(0, TimelineScrollViewer.Extent.Height - TimelineScrollViewer.Viewport.Height);
            TimelineScrollViewer.Offset = new Vector(0, Math.Clamp(estimatedOffset, 0, maxOffset));
        }, DispatcherPriority.Loaded);
    }

    private void ApplyOverviewResponsiveLayout()
    {
        if (OverviewStoryGrid is null || OverviewStoryLeftColumn is null || OverviewStoryRightColumn is null)
        {
            return;
        }

        var overviewWidth = OverviewPage.Bounds.Width > 0 ? OverviewPage.Bounds.Width : Bounds.Width;
        var narrow = overviewWidth < 860;
        if (narrow)
        {
            OverviewStoryGrid.ColumnDefinitions = new ColumnDefinitions("1*");
            OverviewStoryGrid.RowDefinitions = new RowDefinitions("Auto,Auto");
            Grid.SetColumn(OverviewStoryLeftColumn, 0);
            Grid.SetRow(OverviewStoryLeftColumn, 0);
            Grid.SetColumn(OverviewStoryRightColumn, 0);
            Grid.SetRow(OverviewStoryRightColumn, 1);
            OverviewStoryGrid.ColumnSpacing = 0;
            OverviewStoryGrid.RowSpacing = 14;
        }
        else
        {
            OverviewStoryGrid.ColumnDefinitions = new ColumnDefinitions("1.78*,0.72*");
            OverviewStoryGrid.RowDefinitions = new RowDefinitions("Auto");
            Grid.SetColumn(OverviewStoryLeftColumn, 0);
            Grid.SetRow(OverviewStoryLeftColumn, 0);
            Grid.SetColumn(OverviewStoryRightColumn, 1);
            Grid.SetRow(OverviewStoryRightColumn, 0);
            OverviewStoryGrid.ColumnSpacing = 14;
            OverviewStoryGrid.RowSpacing = 0;
        }
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

    private void ApplyOverviewTimelineContextState()
    {
        var context = BuildOverviewTimelineContextText();
        OverviewTimelineContextBanner.IsVisible = !string.IsNullOrWhiteSpace(context);
        OverviewTimelineContextText.Text = context ?? string.Empty;
    }

    private string? BuildOverviewTimelineContextText()
    {
        if (_timelineFocusStart.HasValue && _timelineFocusEnd.HasValue)
        {
            return $"Timeline is still focused to {_timelineFocusStart.Value:HH:mm} - {_timelineFocusEnd.Value:HH:mm} from your overview drill-in.";
        }

        if (!string.IsNullOrWhiteSpace(_searchText))
        {
            return $"Timeline search is still filtering moments by \"{_searchText}\".";
        }

        return _selectedRailSelection switch
        {
            RailSelection.Browsing => "Timeline is still narrowed to browsing moments.",
            RailSelection.Development => "Timeline is still narrowed to development moments.",
            RailSelection.Communication => "Timeline is still narrowed to communication moments.",
            RailSelection.Documents => "Timeline is still narrowed to document moments.",
            RailSelection.SavedMoments => "Timeline is still narrowed to saved moments.",
            RailSelection.Notes => "Timeline is still narrowed to moments with notes.",
            RailSelection.Bookmarks => "Timeline is still narrowed to bookmarked moments.",
            RailSelection.Apps => "Timeline is still narrowed to application-led moments.",
            RailSelection.Websites => "Timeline is still narrowed to website moments.",
            RailSelection.Files => "Timeline is still narrowed to file and document moments.",
            _ => null
        };
    }

    private void OverviewTimelineContextResumeButton_Click(object? sender, RoutedEventArgs e)
    {
        ShowTimelinePage();
    }

    private async void OverviewTimelineContextClearButton_Click(object? sender, RoutedEventArgs e)
    {
        ClearTimelineFocusRange();
        ClearSearchFilter();
        _selectedRailSelection = RailSelection.Timeline;
        SetActiveNavState();
        ApplyOverviewTimelineContextState();
        await RefreshTimelineAsync();
    }

    private async void TimelineEmptyActionButton_Click(object? sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_searchText))
        {
            ClearSearchFilter();
            ApplyOverviewTimelineContextState();
            await RefreshTimelineAsync();
            return;
        }

        if (_timelineFocusStart.HasValue && _timelineFocusEnd.HasValue)
        {
            ClearTimelineFocusRange();
            ApplyOverviewTimelineContextState();
            await RefreshTimelineAsync();
            return;
        }

        if (_selectedRailSelection != RailSelection.Timeline)
        {
            _selectedRailSelection = RailSelection.Timeline;
            SetActiveNavState();
            ApplyOverviewTimelineContextState();
            await RefreshTimelineAsync();
            return;
        }

        if (GetSelectedDate() != DateTime.Today)
        {
            SelectTimelineDate(DateTime.Today);
            await RefreshTimelineAsync();
            await RefreshOverviewAsync();
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
        var selected = FindOverviewMoment(momentId);
        ClearTimelineFocusRange();
        ClearSearchFilter();
        SelectTimelineDate(selected?.SessionStartedAt.LocalDateTime.Date);
        _selectedRailSelection = RailSelection.Timeline;
        _selectedMomentId = momentId;
        _activeWorkspaceNav = WorkspaceNav.Timeline;
        _isDetailPaneVisible = true;
        ApplyDetailPaneVisibility();
        ShowTimelinePage();
        ApplyOverviewTimelineContextState();
        await RefreshTimelineAsync();
    }

    private async Task ShowTimelineRangeAsync(DateTime start, DateTime end)
    {
        ClearSearchFilter();
        SelectTimelineDate(start.Date);
        _selectedRailSelection = RailSelection.Timeline;
        _timelineFocusStart = start;
        _timelineFocusEnd = end;
        _selectedMomentId = null;
        ShowTimelinePage();
        ApplyOverviewTimelineContextState();
        await RefreshTimelineAsync();
    }

    private void SelectTimelineDate(DateTime? date)
    {
        if (date is null)
        {
            return;
        }

        var selectedDate = date.Value.Date;
        var selectedOption = DateFilterComboBox.SelectedItem as DateFilterOption;
        if (selectedOption?.Date == selectedDate)
        {
            return;
        }

        var match = (DateFilterComboBox.ItemsSource as IEnumerable<DateFilterOption>)
            ?.FirstOrDefault(option => option.Date == selectedDate);

        if (match is null)
        {
            return;
        }

        _isSelectingDateForOverviewNavigation = true;
        try
        {
            DateFilterComboBox.SelectedItem = match;
        }
        finally
        {
            _isSelectingDateForOverviewNavigation = false;
        }
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

    private void OverviewMomentsScrollLeft_Click(object? sender, RoutedEventArgs e)
    {
        ScrollOverviewMomentsBy(-GetOverviewMomentsScrollStep());
    }

    private void OverviewMomentsScrollRight_Click(object? sender, RoutedEventArgs e)
    {
        ScrollOverviewMomentsBy(GetOverviewMomentsScrollStep());
    }

    private double GetOverviewMomentsScrollStep()
    {
        return OverviewMomentsScrollViewer.Bounds.Width > 0
            ? Math.Max(240d, OverviewMomentsScrollViewer.Bounds.Width * 0.82d)
            : 320d;
    }

    private void ScrollOverviewMomentsBy(double delta)
    {
        var maxOffset = Math.Max(0d, OverviewMomentsScrollViewer.Extent.Width - OverviewMomentsScrollViewer.Viewport.Width);
        var nextX = Math.Clamp(OverviewMomentsScrollViewer.Offset.X + delta, 0d, maxOffset);
        AnimateOverviewMomentsScrollTo(nextX);
    }

    private void ResetOverviewRailOffsets(bool resetMomentsRail, bool resetFocusRail)
    {
        if (!resetMomentsRail && !resetFocusRail)
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            if (resetMomentsRail)
            {
                _overviewMomentsScrollAnimationTimer?.Stop();
                OverviewMomentsScrollViewer.Offset = new Vector(0, OverviewMomentsScrollViewer.Offset.Y);
            }

            if (resetFocusRail)
            {
                OverviewFocusRhythmControl.ResetScrollPosition();
            }
        }, DispatcherPriority.Background);
    }

    private static string BuildOverviewMomentsSignature(IEnumerable<KeyMomentViewModel> moments)
    {
        return string.Join("|", moments.Select(moment => $"{moment.Id}:{moment.IsPinned}:{moment.Title}:{moment.Subtitle}:{moment.Duration}"));
    }

    private static string BuildOverviewFocusSignature(IEnumerable<FocusBlockViewModel> blocks)
    {
        return string.Join("|", blocks.Take(16).Select(block => $"{block.MomentId}:{block.StartedAt?.ToUnixTimeSeconds()}:{block.Duration.TotalSeconds:F0}"));
    }

    private void ApplyOverviewState(
        IReadOnlyList<OverviewMomentRow> momentRows,
        IReadOnlyList<OverviewTopApplicationRow> topApplicationRows,
        IReadOnlyList<FocusBlockViewModel> focusBlocks,
        bool isEmptyDay)
    {
        var hasMoments = momentRows.Count > 0;
        var showsMomentRailControls = momentRows.Count > 2;

        OverviewEmptyDayBanner.IsVisible = isEmptyDay;
        OverviewSummaryItems.Opacity = isEmptyDay ? 0.72 : 1;
        OverviewMomentsEmptyState.IsVisible = !hasMoments;
        OverviewMomentsScrollViewer.IsVisible = hasMoments;
        OverviewMomentsScrollLeftButton.IsVisible = showsMomentRailControls;
        OverviewMomentsScrollRightButton.IsVisible = showsMomentRailControls;
        OverviewMomentsLeftFade.IsVisible = showsMomentRailControls;
        OverviewMomentsRightFade.IsVisible = showsMomentRailControls;
        OverviewMomentsItems.HorizontalAlignment = showsMomentRailControls
            ? HorizontalAlignment.Left
            : HorizontalAlignment.Center;
        OverviewMomentsRailHost.MinHeight = momentRows.Count switch
        {
            0 => 188d,
            1 => 228d,
            2 => 244d,
            _ => 276d
        };

        if (!hasMoments)
        {
            _overviewMomentsScrollAnimationTimer?.Stop();
            OverviewMomentsScrollViewer.Offset = new Vector(0, OverviewMomentsScrollViewer.Offset.Y);
        }

        OverviewTopApplicationsItems.IsVisible = topApplicationRows.Count > 0;
        OverviewTopApplicationsEmptyState.IsVisible = topApplicationRows.Count == 0;

        if (focusBlocks.Count == 0)
        {
            OverviewFocusRhythmControl.Subtitle = "Your activity flow throughout the day";
        }
        else if (focusBlocks.Count <= 2)
        {
            OverviewFocusRhythmControl.Subtitle = "A compact view of the captured runs so far";
        }
        else
        {
            OverviewFocusRhythmControl.Subtitle = "Your activity flow throughout the day";
        }
    }

    private static string BuildOverviewLeadText(OverviewPresentationModel overview)
    {
        if (overview.Sessions.Count == 0)
        {
            return "No captured activity yet for this day.";
        }

        if (overview.Sessions.Count <= 2)
        {
            var applicationLabel = overview.TopApplications.Count == 1
                ? overview.TopApplications[0].AppName
                : $"{overview.TopApplications.Count} applications";
            return $"{overview.Sessions.Count} moment{(overview.Sessions.Count == 1 ? string.Empty : "s")} captured so far across {applicationLabel} with {OverviewPresentationBuilder.FormatDuration(overview.TotalActive)} active time.";
        }

        return $"{overview.Sessions.Count} moments captured across {overview.TopApplications.Count} applications with {OverviewPresentationBuilder.FormatDuration(overview.TotalActive)} active time.";
    }

    private static IEnumerable<OverviewMomentRow> BuildOverviewMomentRows(IReadOnlyList<KeyMomentViewModel> moments)
    {
        var compactWidth = moments.Count <= 2 ? 244d : 282d;

        return moments.Select(moment => new OverviewMomentRow(
            moment.Id,
            moment.Title,
            moment.Subtitle,
            moment.Timestamp,
            moment.Duration,
            moment.Category,
            moment.AccentFill,
            moment.AccentText,
            moment.Source,
            moment.ThumbnailPath,
            moment.IsPinned ? "Pinned" : "Pin",
            compactWidth));
    }

    private void OverviewMomentsScrollViewer_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(OverviewMomentsScrollViewer).Properties.IsLeftButtonPressed is false)
        {
            return;
        }

        _overviewMomentsDragStartPoint = e.GetPosition(OverviewMomentsScrollViewer);
        _overviewMomentsDragStartOffset = OverviewMomentsScrollViewer.Offset;
        _isOverviewMomentsDragging = false;
    }

    private void OverviewMomentsScrollViewer_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (e.GetCurrentPoint(OverviewMomentsScrollViewer).Properties.IsLeftButtonPressed is false)
        {
            ReleaseOverviewMomentsDrag(e);
            return;
        }

        var delta = e.GetPosition(OverviewMomentsScrollViewer) - _overviewMomentsDragStartPoint;
        if (!_isOverviewMomentsDragging && Math.Abs(delta.X) < 4)
        {
            return;
        }

        if (!_isOverviewMomentsDragging)
        {
            _isOverviewMomentsDragging = true;
            e.Pointer.Capture(OverviewMomentsScrollViewer);
        }

        var maxOffset = Math.Max(0, OverviewMomentsScrollViewer.Extent.Width - OverviewMomentsScrollViewer.Viewport.Width);
        var nextX = Math.Clamp(_overviewMomentsDragStartOffset.X - delta.X, 0, maxOffset);
        OverviewMomentsScrollViewer.Offset = new Vector(nextX, OverviewMomentsScrollViewer.Offset.Y);
        e.Handled = true;
    }

    private void OverviewMomentsScrollViewer_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        ReleaseOverviewMomentsDrag(e);
    }

    private void ReleaseOverviewMomentsDrag(PointerEventArgs e)
    {
        e.Pointer.Capture(null);
        _isOverviewMomentsDragging = false;
    }

    private void AnimateOverviewMomentsScrollTo(double targetX)
    {
        _overviewMomentsScrollFrom = OverviewMomentsScrollViewer.Offset.X;
        _overviewMomentsScrollTo = targetX;

        if (Math.Abs(_overviewMomentsScrollTo - _overviewMomentsScrollFrom) < 1d)
        {
            OverviewMomentsScrollViewer.Offset = new Vector(_overviewMomentsScrollTo, OverviewMomentsScrollViewer.Offset.Y);
            return;
        }

        _overviewMomentsScrollAnimationTimer ??= new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _overviewMomentsScrollAnimationTimer.Tick -= OverviewMomentsScrollAnimationTimer_Tick;
        _overviewMomentsScrollAnimationTimer.Tick += OverviewMomentsScrollAnimationTimer_Tick;

        _overviewMomentsScrollClock.Restart();
        _overviewMomentsScrollAnimationTimer.Start();
    }

    private void OverviewMomentsScrollAnimationTimer_Tick(object? sender, EventArgs e)
    {
        const double durationMs = 220d;

        var progress = Math.Clamp(_overviewMomentsScrollClock.Elapsed.TotalMilliseconds / durationMs, 0d, 1d);
        var eased = 1d - Math.Pow(1d - progress, 3d);
        var nextX = _overviewMomentsScrollFrom + ((_overviewMomentsScrollTo - _overviewMomentsScrollFrom) * eased);
        OverviewMomentsScrollViewer.Offset = new Vector(nextX, OverviewMomentsScrollViewer.Offset.Y);

        if (progress >= 1d)
        {
            _overviewMomentsScrollAnimationTimer?.Stop();
            OverviewMomentsScrollViewer.Offset = new Vector(_overviewMomentsScrollTo, OverviewMomentsScrollViewer.Offset.Y);
        }
    }

    private void SetActiveNavState()
    {
        var isOverview = OverviewPage.IsVisible;

        SetNavItemState(TimelineNavHighlight, TimelineNavAccent, TimelineNavButton, TimelineNavIcon, TimelineNavTitle, TimelineNavSubtitle,
            !isOverview && _activeWorkspaceNav == WorkspaceNav.Timeline && _selectedRailSelection == RailSelection.Timeline);
        SetNavItemState(OverviewNavHighlight, OverviewNavAccent, OverviewNavButton, OverviewNavIcon, OverviewNavTitle, OverviewNavSubtitle,
            isOverview && _activeWorkspaceNav == WorkspaceNav.Overview);
        SetNavItemState(SavedMomentsNavHighlight, SavedMomentsNavAccent, SavedMomentsNavButton, SavedMomentsNavIcon, SavedMomentsNavTitle, SavedMomentsNavSubtitle,
            !isOverview && _selectedRailSelection == RailSelection.SavedMoments);
        SetNavItemState(NotesNavHighlight, NotesNavAccent, NotesNavButton, NotesNavIcon, NotesNavTitle, NotesNavSubtitle,
            !isOverview && _selectedRailSelection == RailSelection.Notes);
        SetNavItemState(BookmarksNavHighlight, BookmarksNavAccent, BookmarksNavButton, BookmarksNavIcon, BookmarksNavTitle, BookmarksNavSubtitle,
            !isOverview && _selectedRailSelection == RailSelection.Bookmarks);
        SetNavItemState(AppsNavHighlight, AppsNavAccent, AppsNavButton, AppsNavIcon, AppsNavTitle, AppsNavSubtitle,
            !isOverview && _selectedRailSelection == RailSelection.Apps);
        SetNavItemState(WebsitesNavHighlight, WebsitesNavAccent, WebsitesNavButton, WebsitesNavIcon, WebsitesNavTitle, WebsitesNavSubtitle,
            !isOverview && _selectedRailSelection == RailSelection.Websites);
        SetNavItemState(FilesNavHighlight, FilesNavAccent, FilesNavButton, FilesNavIcon, FilesNavTitle, FilesNavSubtitle,
            !isOverview && _selectedRailSelection == RailSelection.Files);
    }

    private void SetNavItemState(Border highlight, Border accent, Button button, TextBlock icon, TextBlock title, TextBlock subtitle, bool isActive)
    {
        highlight.Background = isActive
            ? new SolidColorBrush(_currentTheme.ActiveSurface)
            : Brushes.Transparent;
        accent.Background = isActive
            ? new SolidColorBrush(_currentTheme.Accent)
            : Brushes.Transparent;
        button.Background = Brushes.Transparent;
        icon.Foreground = isActive
            ? new SolidColorBrush(_currentTheme.AccentText)
            : new SolidColorBrush(_currentTheme.MutedText);
        title.Foreground = isActive
            ? new SolidColorBrush(_currentTheme.AccentText)
            : new SolidColorBrush(_currentTheme.Text);
        subtitle.Foreground = isActive
            ? new SolidColorBrush(_currentTheme.AccentText)
            : new SolidColorBrush(_currentTheme.SubtleText);
    }

    private void UpdateRailNavSubtitles()
    {
        var selectedDate = GetSelectedDate();
        TimelineNavSubtitle.Text = _sessions.Count switch
        {
            0 => "No moments today",
            1 => "1 moment today",
            _ => $"{_sessions.Count} moments today"
        };
    }

    private void InitializeProfileRail()
    {
        var userName = Environment.UserName;
        ProfileNameText.Text = string.IsNullOrWhiteSpace(userName) ? "Local user" : userName;
        ProfileInitialText.Text = string.IsNullOrWhiteSpace(userName)
            ? "?"
            : char.ToUpperInvariant(userName[0]).ToString();
    }

    private static void StartupTrace(string message)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StartupLogPath)!);
            File.AppendAllText(StartupLogPath, $"{DateTime.Now:O} | {message}{Environment.NewLine}");
        }
        catch
        {
        }
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
        OverviewHeatmapControl.ApplyThemePalette(_currentTheme);
        _timelineRefreshSignature = string.Empty;
        _overviewRefreshSignature = string.Empty;
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
        var pageStrength = Math.Clamp(opacity, 0.0, 1.0);
        var panelStrength = Math.Clamp(opacity + 0.08, 0.0, 1.0);

        if (_isMacChromeMode)
        {
            pageStrength = Math.Clamp(opacity * 0.82, 0.0, 0.92);
            panelStrength = Math.Clamp(opacity + 0.02, 0.0, 0.9);
        }

        OuterShell.Background = new SolidColorBrush(_currentTheme.WithAlpha(_currentTheme.Base, pageStrength));
        RailSurface.Background = new SolidColorBrush(_currentTheme.WithAlpha(_currentTheme.Rail, panelStrength));
        TopCommandSurface.Background = Brushes.Transparent;
        TopCommandSurface.BorderBrush = Brushes.Transparent;
        DetailSurface.Background = new SolidColorBrush(_currentTheme.WithAlpha(_currentTheme.Detail, panelStrength));
        SearchSurface.Background = new SolidColorBrush(_currentTheme.WithAlpha(_currentTheme.TopCommand, panelStrength));
    }

    private void ApplyTimelineViewMode(string? viewMode)
    {
        _preferences.TimelineViewMode = string.Equals(viewMode, "List", StringComparison.OrdinalIgnoreCase) ? "List" : "Cards";
        _timelineRefreshSignature = string.Empty;
        _lastDetailSignature = string.Empty;
        _lastDetailedMomentId = null;
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
                $"{group.ActivityCount} moment{(group.ActivityCount == 1 ? string.Empty : "s")}",
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
            BuildCardMeta(session),
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

        if (!string.IsNullOrWhiteSpace(session.AppName)
            && !string.Equals(session.Title, session.AppName, StringComparison.OrdinalIgnoreCase))
        {
            return session.AppName;
        }

        return session.CategoryLabel;
    }

    private static string BuildCardMeta(ActivityMomentViewModel session)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(session.CategoryLabel)
            && !string.Equals(session.CategoryLabel, BuildCardSubtitle(session), StringComparison.OrdinalIgnoreCase))
        {
            parts.Add(session.CategoryLabel);
        }

        if (!string.IsNullOrWhiteSpace(session.Location)
            && !string.Equals(session.Location, session.AppName, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(session.Location, BuildCardSubtitle(session), StringComparison.OrdinalIgnoreCase)
            && session.Location is not "Development workspace"
            && session.Location is not "Messages and conversations"
            && session.Location is not "Documents and notes"
            && session.Location is not "Visual capture"
            && session.Location is not "System")
        {
            parts.Add(session.Location);
        }

        if (!string.IsNullOrWhiteSpace(session.AppName)
            && !string.Equals(session.AppName, BuildCardSubtitle(session), StringComparison.OrdinalIgnoreCase))
        {
            parts.Add(session.AppName);
        }

        return parts.Count == 0 ? session.CategoryLabel : string.Join(" · ", parts.Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private MomentRow ToMomentRow(ActivityMomentViewModel session, bool isSelected)
    {
        var row = ToMomentRow(session);
        return row with
        {
            IsSelected = isSelected,
            CardBackground = isSelected
                ? ColorToHex(_currentTheme.AccentSoft)
                : ColorToHex(_currentTheme.RaisedSurface),
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

    private sealed record OverviewMomentRow(
        long Id,
        string Title,
        string Subtitle,
        string Timestamp,
        string Duration,
        string Category,
        string AccentFill,
        string AccentText,
        string? Source,
        string? ThumbnailPath,
        string PinActionLabel,
        double CardWidth);

    private enum WorkspaceNav
    {
        None,
        Timeline,
        Overview
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
