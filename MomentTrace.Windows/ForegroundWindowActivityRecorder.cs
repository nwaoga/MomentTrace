using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using MomentTrace.Core;

namespace MomentTrace.Windows;

public sealed class ForegroundWindowActivityRecorder(IActivityStore store) : IActivityRecorder
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

    private readonly IActivityStore _store = store;
    private CancellationTokenSource? _recordingCts;
    private Task? _recordingTask;
    private long? _currentActivityId;
    private Snapshot? _currentSnapshot;

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

        if (_currentSnapshot is null || !_currentSnapshot.SameActivity(snapshot))
        {
            await CloseCurrentActivityAsync(now, cancellationToken);
            await OpenActivityAsync(snapshot, now, cancellationToken);
            return;
        }

        if (_currentActivityId is not null)
        {
            await _store.UpdateEndAsync(_currentActivityId.Value, now, cancellationToken);
        }
    }

    private async Task OpenActivityAsync(Snapshot snapshot, DateTimeOffset now, CancellationToken cancellationToken)
    {
        _currentSnapshot = snapshot;
        var activity = new TimelineActivity
        {
            AppName = snapshot.AppName,
            WindowTitle = snapshot.WindowTitle,
            ProcessName = snapshot.ProcessName,
            ExecutablePath = snapshot.ExecutablePath,
            Kind = ActivityClassifier.Classify(snapshot.ProcessName),
            StartedAt = now,
            EndedAt = now
        };

        _currentActivityId = await _store.AddAsync(activity, cancellationToken);
        ActivityChanged?.Invoke(this, WithId(activity, _currentActivityId.Value));
    }

    private async Task CloseCurrentActivityAsync(DateTimeOffset endedAt, CancellationToken cancellationToken)
    {
        if (_currentActivityId is not null)
        {
            await _store.UpdateEndAsync(_currentActivityId.Value, endedAt, cancellationToken);
            _currentActivityId = null;
        }

        _currentSnapshot = null;
    }

    private sealed record Snapshot(string ProcessName, string AppName, string WindowTitle, string? ExecutablePath)
    {
        public bool SameActivity(Snapshot other)
        {
            return string.Equals(ProcessName, other.ProcessName, StringComparison.OrdinalIgnoreCase)
                && string.Equals(WindowTitle, other.WindowTitle, StringComparison.Ordinal);
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

                return new Snapshot(processName, appName, title.Trim(), executablePath);
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
            Kind = activity.Kind,
            StartedAt = activity.StartedAt,
            EndedAt = activity.EndedAt,
            IsPinned = activity.IsPinned
        };
    }
}
