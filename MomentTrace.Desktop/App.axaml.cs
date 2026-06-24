using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Avalonia.Markup.Xaml;
using MomentTrace.Core;
using MomentTrace.Storage;
#if WINDOWS
using MomentTrace.Windows;
#else
using MomentTrace.MacProbe;
#endif

namespace MomentTrace.Desktop;

public partial class App : Application
{
    private IActivityRecorder? _recorder;
    private SqliteActivityStore? _store;
    private static readonly string StartupLogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MomentTrace",
        "startup-debug.log");

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        StartupTrace("OnFrameworkInitializationCompleted: enter");
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            StartupTrace("OnFrameworkInitializationCompleted: desktop lifetime available");
            _store = new SqliteActivityStore(SqliteActivityStore.DefaultDatabasePath);
            StartupTrace("OnFrameworkInitializationCompleted: store created");
            _recorder = CreateRecorder(_store);
            StartupTrace($"OnFrameworkInitializationCompleted: recorder created = {_recorder is not null}");
            var mainWindow = new MainWindow(_store, _recorder);
            StartupTrace("OnFrameworkInitializationCompleted: main window constructed");
            desktop.MainWindow = mainWindow;
            StartupTrace("OnFrameworkInitializationCompleted: main window assigned");
            desktop.Exit += Desktop_Exit;
            Dispatcher.UIThread.Post(() => EnsureMainWindowVisible(mainWindow), DispatcherPriority.Loaded);
            StartupTrace("OnFrameworkInitializationCompleted: visibility post queued");

            if (OperatingSystem.IsWindows())
            {
                SingleInstanceGuard.RegisterShowWindowHandler(() =>
                    Dispatcher.UIThread.Invoke(BringMainWindowToFront));
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
        if (_recorder is not null)
        {
            await _recorder.DisposeAsync();
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
