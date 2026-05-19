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
    private readonly Action _recordingChanged;
    private bool _isUpdatingStartupCheckBox;
    private bool _isUpdatingOpacitySlider;

    public SettingsWindow(
        IActivityRecorder recorder,
        IStartupRegistration startupRegistration,
        double currentOpacity,
        Action<double> opacityChanged,
        Action recordingChanged)
    {
        _recorder = recorder;
        _startupRegistration = startupRegistration;
        _opacityChanged = opacityChanged;
        _recordingChanged = recordingChanged;

        InitializeComponent();
        LoadCurrentSettings(currentOpacity);
        LoadWhereILive();
    }

    private void LoadCurrentSettings(double currentOpacity)
    {
        _isUpdatingStartupCheckBox = true;
        StartWithWindowsCheckBox.IsChecked = _startupRegistration.IsEnabled();
        _isUpdatingStartupCheckBox = false;

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

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
