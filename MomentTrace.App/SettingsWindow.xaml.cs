using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using MomentTrace.Core;
using MomentTrace.Storage;

namespace MomentTrace.App;

public partial class SettingsWindow : Window
{
    private readonly IActivityRecorder _recorder;
    private readonly IStartupRegistration _startupRegistration;
    private readonly Action<double> _opacityChanged;
    private readonly Action<string> _timelineViewModeChanged;
    private readonly Action _recordingChanged;
    private bool _isUpdatingStartupCheckBox;
    private bool _isUpdatingOpacitySlider;
    private bool _isUpdatingTimelineViewMode;

    public SettingsWindow(
        IActivityRecorder recorder,
        IStartupRegistration startupRegistration,
        double currentOpacity,
        string currentTimelineViewMode,
        Action<double> opacityChanged,
        Action<string> timelineViewModeChanged,
        Action recordingChanged)
    {
        _recorder = recorder;
        _startupRegistration = startupRegistration;
        _opacityChanged = opacityChanged;
        _timelineViewModeChanged = timelineViewModeChanged;
        _recordingChanged = recordingChanged;

        InitializeComponent();
        LoadCurrentSettings(currentOpacity, currentTimelineViewMode);
        LoadWhereILive();
    }

    private void LoadCurrentSettings(double currentOpacity, string currentTimelineViewMode)
    {
        _isUpdatingStartupCheckBox = true;
        StartWithWindowsCheckBox.IsChecked = _startupRegistration.IsEnabled();
        _isUpdatingStartupCheckBox = false;

        _isUpdatingTimelineViewMode = true;
        TimelineViewModeComboBox.SelectedIndex = string.Equals(currentTimelineViewMode, "List", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        _isUpdatingTimelineViewMode = false;

        _isUpdatingOpacitySlider = true;
        OpacitySlider.Value = Math.Clamp(currentOpacity, 0.55, 1.0) * 100;
        OpacityValueText.Text = $"{Math.Round(OpacitySlider.Value)}%";
        _isUpdatingOpacitySlider = false;

        UpdateRecordingButton();
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
        _opacityChanged(Math.Clamp(OpacitySlider.Value / 100, 0.55, 1.0));
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

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
