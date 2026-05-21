using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using MomentTrace.Core;

namespace MomentTrace.Windows;

public sealed class ForegroundWindowActivityRecorder(
    IActivityStore store,
    Func<PrivacyFilterOptions>? privacyOptionsProvider = null) : IActivityRecorder
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan EndUpdateInterval = TimeSpan.FromSeconds(15);

    private readonly IActivityStore _store = store;
    private readonly Func<PrivacyFilterOptions>? _privacyOptionsProvider = privacyOptionsProvider;
    private CancellationTokenSource? _recordingCts;
    private Task? _recordingTask;
    private long? _currentActivityId;
    private Snapshot? _currentSnapshot;
    private DateTimeOffset _currentActivityLastWrittenAt;

    public bool IsRecording => _recordingTask is { IsCompleted: false };

    public event EventHandler<TimelineActivity>? ActivityChanged;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (IsRecording)
        {
            return Task.CompletedTask;
        }

        _recordingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _recordingTask = Task.Run(() => RecordLoopAsync(_recordingCts.Token), CancellationToken.None);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_recordingCts is null)
        {
            return;
        }

        await _recordingCts.CancelAsync();

        if (_recordingTask is not null)
        {
            try
            {
                await _recordingTask.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
        }

        await CloseCurrentActivityAsync(DateTimeOffset.Now, cancellationToken);
        _recordingCts.Dispose();
        _recordingCts = null;
        _recordingTask = null;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }

    private async Task RecordLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await CaptureTickAsync(cancellationToken);
            }
            catch
            {
                // Keep recording even if a single process/window cannot be inspected.
            }

            await Task.Delay(PollInterval, cancellationToken);
        }
    }

    private async Task CaptureTickAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.Now;
        var snapshot = Snapshot.Capture();
        if (snapshot is null)
        {
            await CloseCurrentActivityAsync(now, cancellationToken);
            return;
        }

        if (ActivityExclusionFilter.IsExcluded(snapshot.ProcessName, snapshot.AppName, snapshot.WindowTitle))
        {
            await CloseCurrentActivityAsync(now, cancellationToken);
            return;
        }

        if (ShouldExcludeByPrivacyFilter(snapshot.Resource))
        {
            await CloseCurrentActivityAsync(now, cancellationToken);
            return;
        }

        if (_currentSnapshot is null || !_currentSnapshot.SameActivity(snapshot))
        {
            await CloseCurrentActivityAsync(now, cancellationToken);
            await OpenActivityAsync(snapshot, now, cancellationToken);
            return;
        }

        await ExtendCurrentActivityAsync(now, cancellationToken);
    }

    private async Task OpenActivityAsync(Snapshot snapshot, DateTimeOffset now, CancellationToken cancellationToken)
    {
        _currentSnapshot = snapshot;
        _currentActivityLastWrittenAt = now;
        var screenshotPath = snapshot.Kind == MomentTrace.Core.ActivityKind.Browser ? Snapshot.TryCaptureScreenshot(snapshot.WindowHandle) : null;

        var activity = new TimelineActivity
        {
            AppName = snapshot.AppName,
            WindowTitle = snapshot.WindowTitle,
            ProcessName = snapshot.ProcessName,
            ExecutablePath = snapshot.ExecutablePath,
            Resource = snapshot.Resource,
            ScreenshotPath = screenshotPath,
            Kind = snapshot.Kind,
            StartedAt = now,
            EndedAt = now
        };

        _currentActivityId = await _store.AddAsync(activity, cancellationToken);
        ActivityChanged?.Invoke(this, WithId(activity, _currentActivityId.Value));
    }

    private bool ShouldExcludeByPrivacyFilter(string? resource)
    {
        if (_privacyOptionsProvider is null)
        {
            return false;
        }

        try
        {
            return new DomainPrivacyFilter(_privacyOptionsProvider()).ShouldExclude(resource);
        }
        catch
        {
            return false;
        }
    }

    private async Task ExtendCurrentActivityAsync(DateTimeOffset endedAt, CancellationToken cancellationToken)
    {
        if (_currentActivityId is null || endedAt - _currentActivityLastWrittenAt < EndUpdateInterval)
        {
            return;
        }

        await _store.UpdateEndAsync(_currentActivityId.Value, endedAt, cancellationToken);
        _currentActivityLastWrittenAt = endedAt;
    }

    private async Task CloseCurrentActivityAsync(DateTimeOffset endedAt, CancellationToken cancellationToken)
    {
        if (_currentActivityId is not null)
        {
            await _store.UpdateEndAsync(_currentActivityId.Value, endedAt, cancellationToken);
            _currentActivityId = null;
        }

        _currentSnapshot = null;
        _currentActivityLastWrittenAt = default;
    }

    private sealed record Snapshot(
        IntPtr WindowHandle,
        string ProcessName,
        string AppName,
        string WindowTitle,
        string? ExecutablePath,
        string? Resource,
        MomentTrace.Core.ActivityKind Kind)
    {
        public bool SameActivity(Snapshot other)
        {
            return string.Equals(ProcessName, other.ProcessName, StringComparison.OrdinalIgnoreCase)
                && string.Equals(WindowTitle, other.WindowTitle, StringComparison.Ordinal)
                && string.Equals(Resource, other.Resource, StringComparison.OrdinalIgnoreCase);
        }

        public static Snapshot? Capture()
        {
            var handle = GetForegroundWindow();
            if (handle == IntPtr.Zero)
            {
                return null;
            }

            var title = GetWindowTitle(handle);
            if (string.IsNullOrWhiteSpace(title))
            {
                return null;
            }

            _ = GetWindowThreadProcessId(handle, out var processId);

            try
            {
                using var process = Process.GetProcessById((int)processId);
                var processName = process.ProcessName;
                var executablePath = TryGetExecutablePath(process);
                var appName = TryGetDisplayName(process, executablePath);
                var kind = ActivityClassifier.Classify(processName);
                var resource = kind == MomentTrace.Core.ActivityKind.Browser ? TryGetBrowserUrl(handle) : null;

                return new Snapshot(handle, processName, appName, title.Trim(), executablePath, resource, kind);
            }
            catch
            {
                return null;
            }
        }

        private static string TryGetDisplayName(Process process, string? executablePath)
        {
            if (!string.IsNullOrWhiteSpace(executablePath))
            {
                try
                {
                    var description = FileVersionInfo.GetVersionInfo(executablePath).FileDescription;
                    if (!string.IsNullOrWhiteSpace(description))
                    {
                        return description;
                    }
                }
                catch
                {
                }
            }

            return process.ProcessName;
        }

        private static string? TryGetBrowserUrl(IntPtr handle)
        {
            try
            {
                using var automation = new UIA3Automation();
                var root = automation.FromHandle(handle);
                var edits = root.FindAllDescendants(condition => condition.ByControlType(ControlType.Edit));

                foreach (var edit in edits)
                {
                    var value = edit.Patterns.Value.PatternOrDefault?.Value;
                    if (TryNormalizeUrl(value, out var url))
                    {
                        return url;
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        public static string? TryCaptureScreenshot(IntPtr handle)
        {
            try
            {
                if (!GetWindowRect(handle, out var rect))
                {
                    return null;
                }

                var width = rect.Right - rect.Left;
                var height = rect.Bottom - rect.Top;
                if (width <= 0 || height <= 0)
                {
                    return null;
                }

                var screenshotDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "MomentTrace",
                    "screenshots");
                Directory.CreateDirectory(screenshotDirectory);

                var screenshotPath = Path.Combine(
                    screenshotDirectory,
                    $"{DateTimeOffset.Now:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}.png");

                using var bitmap = new Bitmap(width, height);
                using var graphics = Graphics.FromImage(bitmap);
                graphics.CopyFromScreen(rect.Left, rect.Top, 0, 0, new Size(width, height));
                bitmap.Save(screenshotPath, ImageFormat.Png);

                return screenshotPath;
            }
            catch
            {
                return null;
            }
        }

        private static bool TryNormalizeUrl(string? value, out string url)
        {
            url = string.Empty;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var candidate = value.Trim();
            if (!candidate.Contains('.', StringComparison.Ordinal) || candidate.Contains(' ', StringComparison.Ordinal))
            {
                return false;
            }

            if (!candidate.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                && !candidate.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                candidate = $"https://{candidate}";
            }

            if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri)
                || uri.Scheme is not ("http" or "https")
                || string.IsNullOrWhiteSpace(uri.Host))
            {
                return false;
            }

            url = uri.ToString();
            return true;
        }

        private static string? TryGetExecutablePath(Process process)
        {
            try
            {
                return process.MainModule?.FileName;
            }
            catch
            {
                return null;
            }
        }

        private static string GetWindowTitle(IntPtr handle)
        {
            var length = GetWindowTextLength(handle);
            if (length <= 0)
            {
                return string.Empty;
            }

            var builder = new StringBuilder(length + 1);
            _ = GetWindowText(handle, builder, builder.Capacity);
            return builder.ToString();
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    private static TimelineActivity WithId(TimelineActivity activity, long id)
    {
        return new TimelineActivity
        {
            Id = id,
            AppName = activity.AppName,
            WindowTitle = activity.WindowTitle,
            ProcessName = activity.ProcessName,
            ExecutablePath = activity.ExecutablePath,
            Resource = activity.Resource,
            ScreenshotPath = activity.ScreenshotPath,
            Kind = activity.Kind,
            StartedAt = activity.StartedAt,
            EndedAt = activity.EndedAt,
            IsPinned = activity.IsPinned
        };
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct WindowRect
    {
        public readonly int Left;
        public readonly int Top;
        public readonly int Right;
        public readonly int Bottom;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowRect(IntPtr hWnd, out WindowRect lpRect);
}
