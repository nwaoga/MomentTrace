using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using MomentTrace.Core;
using MomentTrace.Storage;
#if WINDOWS
using MomentTrace.Windows;
#else
using MomentTrace.Mac;
#endif

namespace MomentTrace.Desktop;

public partial class App : Application
{
    private IActivityRecorder? _recorder;
    private SqliteActivityStore? _store;
    private IClassicDesktopStyleApplicationLifetime? _desktop;
    private MainWindow? _mainWindow;
    private TrayIcon? _trayIcon;
    private NativeMenuItem? _pauseRecordingMenuItem;
    private NativeMenuItem? _resumeRecordingMenuItem;
    private bool _isExplicitShutdown;
    private static readonly string StartupLogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MomentTrace",
        "startup-debug.log");

    public override void Initialize()
    {
        Name = "MomentTrace";
        AvaloniaXamlLoader.Load(this);
        Name = "MomentTrace";
    }

    public override void OnFrameworkInitializationCompleted()
    {
        StartupTrace("OnFrameworkInitializationCompleted: enter");
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            StartupTrace("OnFrameworkInitializationCompleted: desktop lifetime available");
            _desktop = desktop;
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            _store = new SqliteActivityStore(SqliteActivityStore.DefaultDatabasePath);
            StartupTrace("OnFrameworkInitializationCompleted: store created");
            _recorder = CreateRecorder(_store);
            StartupTrace($"OnFrameworkInitializationCompleted: recorder created = {_recorder is not null}");
            var mainWindow = new MainWindow(_store, _recorder);
            _mainWindow = mainWindow;
            StartupTrace("OnFrameworkInitializationCompleted: main window constructed");
            desktop.MainWindow = mainWindow;
            StartupTrace("OnFrameworkInitializationCompleted: main window assigned");
            desktop.Exit += Desktop_Exit;
            mainWindow.Closing += MainWindow_Closing;
            Dispatcher.UIThread.Post(() => EnsureMainWindowVisible(mainWindow), DispatcherPriority.Loaded);
            StartupTrace("OnFrameworkInitializationCompleted: visibility post queued");

            if (OperatingSystem.IsWindows())
            {
                SingleInstanceGuard.RegisterShowWindowHandler(() =>
                    Dispatcher.UIThread.Invoke(BringMainWindowToFront));
            }

            if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS())
            {
                CreateTrayIcon();
            }

            if (_recorder is not null)
            {
                Dispatcher.UIThread.Post(InitializeStoreAndRecorderAsync);
            }
        }

        base.OnFrameworkInitializationCompleted();
        StartupTrace("OnFrameworkInitializationCompleted: base completed");
    }

    private async void InitializeStoreAndRecorderAsync()
    {
        if (_store is null || _recorder is null)
        {
            return;
        }

        try
        {
            await _store.InitializeAsync();
            await _recorder.StartAsync();
        }
        catch
        {
            // Recorder startup should not prevent the shell from appearing.
        }
    }

    private static IActivityRecorder? CreateRecorder(SqliteActivityStore store)
    {
#if WINDOWS
        return new ForegroundWindowActivityRecorder(store, () => DesktopWindowPreferences.Load().ToPrivacyFilterOptions());
#else
        return OperatingSystem.IsMacOS() ? new MacForegroundActivityRecorder(store) : null;
#endif
    }

    private async void Desktop_Exit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        _trayIcon?.Dispose();

        if (_recorder is not null)
        {
            await _recorder.DisposeAsync();
        }
    }

    private void MainWindow_Closing(object? sender, WindowClosingEventArgs e)
    {
        if (_isExplicitShutdown || _trayIcon is null)
        {
            return;
        }

        e.Cancel = true;
        HideMainWindow();
    }

    private void CreateTrayIcon()
    {
        var openMenuItem = new NativeMenuItem(OperatingSystem.IsMacOS() ? "Show MomentTrace" : "Open MomentTrace");
        openMenuItem.Click += (_, _) => ShowMainWindow();

        _pauseRecordingMenuItem = new NativeMenuItem("Pause recording");
        _pauseRecordingMenuItem.Click += async (_, _) => await StopRecordingAsync();

        _resumeRecordingMenuItem = new NativeMenuItem("Resume recording");
        _resumeRecordingMenuItem.Click += async (_, _) => await StartRecordingAsync();

        var exitMenuItem = new NativeMenuItem("Exit");
        exitMenuItem.Click += async (_, _) => await ExitApplicationAsync();

        var menu = new NativeMenu();
        menu.Add(openMenuItem);
        menu.Add(_pauseRecordingMenuItem);
        menu.Add(_resumeRecordingMenuItem);
        menu.Add(new NativeMenuItemSeparator());
        menu.Add(exitMenuItem);

        _trayIcon = new TrayIcon
        {
            Icon = LoadTrayIcon(),
            ToolTipText = $"MomentTrace {AppBuildIdentity.ShortVersion}",
            Menu = menu,
            IsVisible = true
        };
        _trayIcon.Clicked += (_, _) => ShowMainWindow();

        if (OperatingSystem.IsMacOS())
        {
            MacOSProperties.SetIsTemplateIcon(_trayIcon, true);
        }

        UpdateTrayMenuState();
    }

    private static WindowIcon LoadTrayIcon()
    {
        var iconPath = OperatingSystem.IsMacOS()
            ? "avares://MomentTrace.Desktop/Assets/MomentTrace.Template.png"
            : "avares://MomentTrace.Desktop/Assets/MomentTrace.png";
        return new WindowIcon(AssetLoader.Open(new Uri(iconPath)));
    }

    private void ShowMainWindow()
    {
        if (_mainWindow is null)
        {
            return;
        }

        EnsureMainWindowVisible(_mainWindow);

        if (_mainWindow.WindowState == WindowState.Minimized)
        {
            _mainWindow.WindowState = WindowState.Normal;
        }

        _mainWindow.ShowInTaskbar = true;
        _mainWindow.Activate();
    }

    private void HideMainWindow()
    {
        if (_mainWindow is null)
        {
            return;
        }

        _mainWindow.ShowInTaskbar = false;
        _mainWindow.Hide();
    }

    private async Task StopRecordingAsync()
    {
        if (_recorder is null || !_recorder.IsRecording)
        {
            UpdateTrayMenuState();
            return;
        }

        await _recorder.StopAsync();
        UpdateTrayMenuState();
    }

    private async Task StartRecordingAsync()
    {
        if (_recorder is null || _recorder.IsRecording)
        {
            UpdateTrayMenuState();
            return;
        }

        await _recorder.StartAsync();
        UpdateTrayMenuState();
    }

    private async Task ExitApplicationAsync()
    {
        _isExplicitShutdown = true;

        if (_recorder is not null && _recorder.IsRecording)
        {
            await _recorder.StopAsync();
        }

        _desktop?.Shutdown();
    }

    private void UpdateTrayMenuState()
    {
        var canPause = _recorder is not null && _recorder.IsRecording;
        var canResume = _recorder is not null && !_recorder.IsRecording;

        if (_pauseRecordingMenuItem is not null)
        {
            _pauseRecordingMenuItem.IsEnabled = canPause;
        }

        if (_resumeRecordingMenuItem is not null)
        {
            _resumeRecordingMenuItem.IsEnabled = canResume;
        }
    }

    private static void EnsureMainWindowVisible(Window mainWindow)
    {
        StartupTrace("EnsureMainWindowVisible: enter");
        if (!mainWindow.IsVisible)
        {
            mainWindow.Show();
            StartupTrace("EnsureMainWindowVisible: Show called");
        }

        mainWindow.Activate();
        StartupTrace("EnsureMainWindowVisible: Activate called");
    }

    private void BringMainWindowToFront()
    {
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop
            || desktop.MainWindow is not Window mainWindow)
        {
            return;
        }

        EnsureMainWindowVisible(mainWindow);

        if (mainWindow.WindowState == WindowState.Minimized)
        {
            mainWindow.WindowState = WindowState.Normal;
        }

        mainWindow.Activate();
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
}
