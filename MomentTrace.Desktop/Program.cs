using Avalonia;
using System;
using System.IO;
using System.Runtime.Versioning;
using System.Threading;

namespace MomentTrace.Desktop;

class Program
{
    private static readonly string StartupLogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MomentTrace",
        "startup-debug.log");

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        StartupTrace("Program.Main: enter");
        if (OperatingSystem.IsWindows() && !SingleInstanceGuard.TryAcquireFirstInstance())
        {
            StartupTrace("Program.Main: existing instance detected");
            SingleInstanceGuard.SignalExistingInstance();
            return;
        }

        try
        {
            StartupTrace("Program.Main: starting Avalonia lifetime");
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
            StartupTrace("Program.Main: Avalonia lifetime exited");
        }
        finally
        {
            StartupTrace("Program.Main: releasing single instance");
            SingleInstanceGuard.Release();
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        StartupTrace("Program.BuildAvaloniaApp: configure");
        var builder = AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .LogToTrace();

        if (!OperatingSystem.IsMacOS())
        {
            builder = builder.WithInterFont();
        }

        return builder;
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

internal static class SingleInstanceGuard
{
    private const string SingleInstanceMutexName = @"Local\MomentTrace.SingleInstance";
    private const string ShowWindowEventName = @"Local\MomentTrace.ShowWindow";

    private static Mutex? _singleInstanceMutex;
    private static EventWaitHandle? _showWindowEvent;
    private static RegisteredWaitHandle? _showWindowRegistration;
    private static bool _ownsSingleInstance;

    public static bool TryAcquireFirstInstance()
    {
        _singleInstanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var isFirstInstance);
        _ownsSingleInstance = isFirstInstance;
        return isFirstInstance;
    }

    [SupportedOSPlatform("windows")]
    public static void SignalExistingInstance()
    {
        try
        {
            using var showWindowEvent = EventWaitHandle.OpenExisting(ShowWindowEventName);
            showWindowEvent.Set();
        }
        catch
        {
#if WINDOWS
            System.Media.SystemSounds.Exclamation.Play();
#endif
        }
    }

    public static void RegisterShowWindowHandler(Action showWindow)
    {
        _showWindowEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowWindowEventName);
        _showWindowRegistration = ThreadPool.RegisterWaitForSingleObject(
            _showWindowEvent,
            (_, _) => showWindow(),
            null,
            Timeout.InfiniteTimeSpan,
            executeOnlyOnce: false);
    }

    public static void Release()
    {
        _showWindowRegistration?.Unregister(null);
        _showWindowEvent?.Dispose();
        if (_ownsSingleInstance)
        {
            _singleInstanceMutex?.ReleaseMutex();
        }

        _singleInstanceMutex?.Dispose();
    }
}
