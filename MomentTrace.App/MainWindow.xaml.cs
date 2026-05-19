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
    private readonly ObservableCollection<ActivityItemViewModel> _activities = [];
    private readonly ICollectionView _activityView;
    private bool _allowClose;
    private bool _isUpdatingStartupCheckBox;
    private bool _isUpdatingOpacitySlider;
    private bool _canSaveOpacity;

    public MainWindow(IActivityStore store, IActivityRecorder recorder, IStartupRegistration startupRegistration)
    {
        _store = store;
        _recorder = recorder;
        _startupRegistration = startupRegistration;

        InitializeComponent();

        SearchBox.Text = string.Empty;
        _activityView = CollectionViewSource.GetDefaultView(_activities);
        _activityView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ActivityItemViewModel.DateGroup)));
        TimelineList.ItemsSource = _activityView;

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
        _isUpdatingStartupCheckBox = true;
        StartWithWindowsCheckBox.IsChecked = _startupRegistration.IsEnabled();
        _isUpdatingStartupCheckBox = false;

        ApplySavedOpacity();
        _canSaveOpacity = true;
        await _recorder.StartAsync();
        UpdateRecordingStatus();
        await LoadActivitiesAsync();
    }

    private async void Recorder_ActivityChanged(object? sender, TimelineActivity e)
    {
        await Dispatcher.InvokeAsync(async () => await LoadActivitiesAsync());
    }

    private async Task LoadActivitiesAsync()
    {
        var query = new TimelineQuery
        {
            SearchText = SearchBox.Text,
            Kind = GetSelectedKind(),
            Limit = 300
        };

        var activities = await _store.QueryAsync(query);
        _activities.Clear();

        foreach (var activity in activities)
        {
            _activities.Add(new ActivityItemViewModel(activity));
        }

        if (TimelineList.SelectedItem is null && _activities.Count > 0)
        {
            TimelineList.SelectedIndex = 0;
        }
    }

    private MomentTrace.Core.ActivityKind? GetSelectedKind()
    {
        if (KindFilter.SelectedItem is not ComboBoxItem item || item.Content is not string content || content == "All")
        {
            return null;
        }

        return Enum.TryParse<MomentTrace.Core.ActivityKind>(content, out var kind) ? kind : null;
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

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await LoadActivitiesAsync();
    }

    private async void TodayButton_Click(object sender, RoutedEventArgs e)
    {
        SearchBox.Text = string.Empty;
        KindFilter.SelectedIndex = 0;
        await LoadActivitiesAsync();
    }

    private async void PinnedButton_Click(object sender, RoutedEventArgs e)
    {
        SearchBox.Text = string.Empty;
        KindFilter.SelectedIndex = 0;
        await LoadActivitiesAsync();

        var pinned = _activities.FirstOrDefault(activity => activity.Activity.IsPinned);
        if (pinned is not null)
        {
            TimelineList.SelectedItem = pinned;
            TimelineList.ScrollIntoView(pinned);
        }
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

        UpdateRecordingStatus();
        await LoadActivitiesAsync();
    }

    private void UpdateRecordingStatus()
    {
        RecordingButton.Content = _recorder.IsRecording ? "Pause" : "Resume";
        StatusText.Text = _recorder.IsRecording
            ? "Recording foreground window activity with timestamps."
            : "Recording is paused.";
    }

    private void TimelineList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TimelineList.SelectedItem is not ActivityItemViewModel selected)
        {
            DetailKindText.Text = "Select an activity";
            DetailTitleText.Text = "No activity selected";
            DetailAppText.Text = string.Empty;
            DetailStartedText.Text = string.Empty;
            DetailEndedText.Text = string.Empty;
            DetailDurationText.Text = string.Empty;
            DetailProcessText.Text = string.Empty;
            DetailIconImage.Source = null;
            DetailIconFallbackText.Visibility = Visibility.Visible;
            return;
        }

        var activity = selected.Activity;
        DetailKindText.Text = selected.KindLabel;
        DetailTitleText.Text = activity.DisplayTitle;
        DetailAppText.Text = selected.Location;
        DetailStartedText.Text = $"{activity.StartedAt.LocalDateTime:dddd, d MMM yyyy HH:mm:ss}";
        DetailEndedText.Text = $"{activity.EndedAt.LocalDateTime:dddd, d MMM yyyy HH:mm:ss}";
        DetailDurationText.Text = selected.Duration;
        DetailProcessText.Text = string.IsNullOrWhiteSpace(activity.ExecutablePath)
            ? activity.ProcessName
            : $"{activity.ProcessName} · {activity.ExecutablePath}";
        DetailIconImage.Source = selected.ProgramIcon;
        DetailIconFallbackText.Text = selected.IconGlyph;
        DetailIconFallbackText.Visibility = selected.ProgramIcon is null ? Visibility.Visible : Visibility.Collapsed;
        PinButton.Content = activity.IsPinned ? "Unpin" : "Pin";
    }

    private async void PinButton_Click(object sender, RoutedEventArgs e)
    {
        if (TimelineList.SelectedItem is not ActivityItemViewModel selected)
        {
            return;
        }

        await _store.SetPinnedAsync(selected.Id, !selected.Activity.IsPinned);
        await LoadActivitiesAsync();
    }

    private void OpenAppButton_Click(object sender, RoutedEventArgs e)
    {
        if (TimelineList.SelectedItem is not ActivityItemViewModel selected)
        {
            return;
        }

        try
        {
            var target = !string.IsNullOrWhiteSpace(selected.ExecutablePath)
                ? selected.ExecutablePath
                : selected.ProcessName;
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch
        {
            System.Media.SystemSounds.Exclamation.Play();
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        Hide();
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
        if (_isUpdatingOpacitySlider || !_canSaveOpacity)
        {
            return;
        }

        var opacity = Math.Clamp(OpacitySlider.Value / 100, 0.55, 1.0);
        ApplySurfaceTransparency(opacity);
        OpacityValueText.Text = $"{Math.Round(OpacitySlider.Value)}%";
        _preferences.Opacity = opacity;
        _preferences.Save();
    }

    private void ApplySavedOpacity()
    {
        var opacity = Math.Clamp(_preferences.Opacity, 0.55, 1.0);
        Opacity = 1.0;
        ApplySurfaceTransparency(opacity);

        _isUpdatingOpacitySlider = true;
        OpacitySlider.Value = opacity * 100;
        OpacityValueText.Text = $"{Math.Round(OpacitySlider.Value)}%";
        _isUpdatingOpacitySlider = false;
    }

    private void ApplySurfaceTransparency(double opacity)
    {
        var panelAlpha = (byte)Math.Round(Math.Clamp(opacity, 0.55, 1.0) * 255);
        var shellAlpha = (byte)Math.Round(Math.Clamp(opacity - 0.12, 0.38, 0.9) * 255);

        WindowSurface.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(shellAlpha, 246, 248, 251));
        RailSurface.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(panelAlpha, 241, 245, 249));
        TopCommandSurface.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(panelAlpha, 255, 255, 255));
        DetailSurface.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(panelAlpha, 255, 255, 255));
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
