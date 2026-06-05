using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
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

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var store = new SqliteActivityStore(SqliteActivityStore.DefaultDatabasePath);
            await store.InitializeAsync();

            _recorder = CreateRecorder(store);
            if (_recorder is not null)
            {
                await _recorder.StartAsync();
            }

            desktop.Exit += Desktop_Exit;
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
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
}
