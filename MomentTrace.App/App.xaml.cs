using System.Drawing;
using System.IO;
using System.Windows;
using Forms = System.Windows.Forms;
using MomentTrace.Core;
using MomentTrace.Storage;
using MomentTrace.Windows;

namespace MomentTrace.App;

public partial class App : System.Windows.Application
{
    private Forms.NotifyIcon? _notifyIcon;
    private MainWindow? _mainWindow;
    private IActivityRecorder? _recorder;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var store = new SqliteActivityStore(SqliteActivityStore.DefaultDatabasePath);
        await store.InitializeAsync();

        _recorder = new ForegroundWindowActivityRecorder(store);
        var startupRegistration = new WindowsStartupRegistration("MomentTrace", GetExecutablePath(), "Nwaoga Timeline");

        _mainWindow = new MainWindow(store, _recorder, startupRegistration);
        CreateTrayIcon();

        if (e.Args.Any(arg => string.Equals(arg, "--minimized", StringComparison.OrdinalIgnoreCase)))
        {
            _mainWindow.Loaded += (_, _) => _mainWindow.Hide();
            _mainWindow.ShowInTaskbar = false;
            _mainWindow.Show();
            _mainWindow.Hide();
        }
        else
        {
            _mainWindow.Show();
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_recorder is not null)
        {
            await _recorder.DisposeAsync();
        }

        _notifyIcon?.Dispose();
        base.OnExit(e);
    }

    private void CreateTrayIcon()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open MomentTrace", null, (_, _) => ShowMainWindow());
        menu.Items.Add("Pause Recording", null, async (_, _) => await StopRecordingAsync());
        menu.Items.Add("Resume Recording", null, async (_, _) => await StartRecordingAsync());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, async (_, _) => await ExitApplicationAsync());

        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "MomentTrace",
            ContextMenuStrip = menu,
            Visible = true
        };
        _notifyIcon.DoubleClick += (_, _) => ShowMainWindow();
    }

    private static string GetExecutablePath()
    {
        if (!string.IsNullOrWhiteSpace(Environment.ProcessPath))
        {
            return Environment.ProcessPath;
        }

        return Path.Combine(AppContext.BaseDirectory, "MomentTrace.App.exe");
    }

    private void ShowMainWindow()
    {
        if (_mainWindow is null)
        {
            return;
        }

        _mainWindow.ShowInTaskbar = true;
        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    private async Task StopRecordingAsync()
    {
        if (_recorder is not null)
        {
            await _recorder.StopAsync();
        }
    }

    private async Task StartRecordingAsync()
    {
        if (_recorder is not null)
        {
            await _recorder.StartAsync();
        }
    }

    private async Task ExitApplicationAsync()
    {
        if (_mainWindow is not null)
        {
            _mainWindow.AllowClose();
        }

        if (_recorder is not null)
        {
            await _recorder.DisposeAsync();
        }

        Shutdown();
    }
}
