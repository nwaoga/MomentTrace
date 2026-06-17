using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using MomentTrace.Core;

namespace MomentTrace.MacProbe;

public sealed class MacForegroundActivityRecorder(IActivityStore store) : IActivityRecorder
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan EndUpdateInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan ActivityGapThreshold = TimeSpan.FromMinutes(5);

    private readonly IActivityStore _store = store;
    private CancellationTokenSource? _recordingCts;
    private Task? _recordingTask;
    private long? _currentActivityId;
    private Snapshot? _currentSnapshot;
    private DateTimeOffset _currentActivityStartedAt;
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
                // A permission prompt or transient app switch should not stop the probe.
            }

            await Task.Delay(PollInterval, cancellationToken);
        }
    }

    private async Task CaptureTickAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.Now;
        var snapshot = await Snapshot.CaptureAsync(cancellationToken);
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

        if (_currentSnapshot is null || !_currentSnapshot.SameActivity(snapshot))
        {
            await CloseCurrentActivityAsync(now, cancellationToken);
            await OpenActivityAsync(snapshot, now, cancellationToken);
            return;
        }

        if (ShouldSplitCurrentActivity(now))
        {
            await CloseCurrentActivityAsync(_currentActivityLastWrittenAt, cancellationToken);
            await OpenActivityAsync(snapshot, now, cancellationToken);
            return;
        }

        await ExtendCurrentActivityAsync(now, cancellationToken);
    }

    private async Task OpenActivityAsync(Snapshot snapshot, DateTimeOffset now, CancellationToken cancellationToken)
    {
        _currentSnapshot = snapshot;
        _currentActivityStartedAt = now;
        _currentActivityLastWrittenAt = now;
        var screenshotPath = Snapshot.TryCaptureScreenshot();
        var thumbnailPath = Snapshot.TryCreateImageThumbnail(screenshotPath);

        var activity = new TimelineActivity
        {
            AppName = snapshot.AppName,
            WindowTitle = snapshot.WindowTitle,
            ProcessName = snapshot.ProcessName,
            BundleIdentifier = snapshot.BundleIdentifier,
            ExecutablePath = snapshot.ExecutablePath,
            Resource = snapshot.Resource,
            ScreenshotPath = screenshotPath,
            ThumbnailPath = thumbnailPath,
            Kind = ActivityClassifier.Classify(snapshot.ProcessName),
            StartedAt = now,
            EndedAt = now
        };

        _currentActivityId = await _store.AddAsync(activity, cancellationToken);
        ActivityChanged?.Invoke(this, WithId(activity, _currentActivityId.Value));
    }

    private bool ShouldSplitCurrentActivity(DateTimeOffset now)
    {
        return now.LocalDateTime.Date != _currentActivityStartedAt.LocalDateTime.Date
            || now - _currentActivityLastWrittenAt > ActivityGapThreshold;
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

    private static TimelineActivity WithId(TimelineActivity activity, long id)
    {
        return new TimelineActivity
        {
            Id = id,
            AppName = activity.AppName,
            WindowTitle = activity.WindowTitle,
            ProcessName = activity.ProcessName,
            BundleIdentifier = activity.BundleIdentifier,
            ExecutablePath = activity.ExecutablePath,
            Resource = activity.Resource,
            ScreenshotPath = activity.ScreenshotPath,
            ThumbnailPath = activity.ThumbnailPath,
            Kind = activity.Kind,
            StartedAt = activity.StartedAt,
            EndedAt = activity.EndedAt,
            IsPinned = activity.IsPinned,
            Note = activity.Note,
            IsBookmarked = activity.IsBookmarked
        };
    }

    private sealed record Snapshot(
        string ProcessName,
        string AppName,
        string WindowTitle,
        string? BundleIdentifier,
        string? Resource,
        string? ExecutablePath)
    {
        public bool SameActivity(Snapshot other)
        {
            return string.Equals(ProcessName, other.ProcessName, StringComparison.OrdinalIgnoreCase)
                && string.Equals(WindowTitle, other.WindowTitle, StringComparison.Ordinal)
                && string.Equals(Resource, other.Resource, StringComparison.OrdinalIgnoreCase);
        }

        public static async Task<Snapshot?> CaptureAsync(CancellationToken cancellationToken)
        {
            const string script = """
                tell application "System Events"
                    set frontApp to first application process whose frontmost is true
                    set appName to name of frontApp
                    set appPid to unix id of frontApp
                    set bundleId to ""
                    try
                        set bundleId to bundle identifier of frontApp
                    end try
                    set windowTitle to ""
                    try
                        set windowTitle to value of attribute "AXTitle" of window 1 of frontApp
                    end try
                    set appPath to ""
                    try
                        set appPath to POSIX path of (file of frontApp as alias)
                    end try
                    return appName & linefeed & appPid & linefeed & bundleId & linefeed & appPath & linefeed & windowTitle
                end tell
                """;

            var output = await RunOsaScriptAsync(script, cancellationToken);
            if (string.IsNullOrWhiteSpace(output))
            {
                return null;
            }

            var lines = output.Replace("\r\n", "\n").Split('\n', 5);
            var appName = lines.ElementAtOrDefault(0)?.Trim();
            var pid = lines.ElementAtOrDefault(1)?.Trim();
            var bundleIdentifier = lines.ElementAtOrDefault(2)?.Trim();
            var appPath = lines.ElementAtOrDefault(3)?.Trim();
            var title = lines.ElementAtOrDefault(4)?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(appName))
            {
                return null;
            }

            bundleIdentifier = await ResolveBundleIdentifierAsync(bundleIdentifier, appPath, cancellationToken);

            var processName = appName.Replace(" ", string.Empty);
            var resource = BuildResource(bundleIdentifier, appPath, pid);
            return new Snapshot(
                processName,
                appName,
                title,
                string.IsNullOrWhiteSpace(bundleIdentifier) ? null : bundleIdentifier,
                resource,
                string.IsNullOrWhiteSpace(appPath) ? null : appPath);
        }

        private static async Task<string?> ResolveBundleIdentifierAsync(string? bundleIdentifier, string? appPath, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(bundleIdentifier))
            {
                return bundleIdentifier.Trim();
            }

            if (string.IsNullOrWhiteSpace(appPath) || !appPath.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var resolved = await RunCommandAsync(
                "/usr/bin/mdls",
                cancellationToken,
                "-name",
                "kMDItemCFBundleIdentifier",
                "-raw",
                appPath.Trim());

            if (string.IsNullOrWhiteSpace(resolved))
            {
                return null;
            }

            var candidate = resolved.Trim();
            return string.Equals(candidate, "(null)", StringComparison.OrdinalIgnoreCase) ? null : candidate;
        }

        private static string? BuildResource(string? bundleIdentifier, string? appPath, string? pid)
        {
            if (!string.IsNullOrWhiteSpace(bundleIdentifier))
            {
                return $"app://{bundleIdentifier.Trim()}";
            }

            if (!string.IsNullOrWhiteSpace(appPath))
            {
                return appPath.Trim();
            }

            return string.IsNullOrWhiteSpace(pid) ? null : $"pid:{pid.Trim()}";
        }

        public static string? TryCaptureScreenshot()
        {
            try
            {
                var screenshotDirectory = Path.Combine(
                    MomentTracePaths.ScreenshotsDirectory);
                Directory.CreateDirectory(screenshotDirectory);

                var screenshotPath = Path.Combine(
                    screenshotDirectory,
                    $"{DateTimeOffset.Now:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}.png");

                var startInfo = new ProcessStartInfo
                {
                    FileName = "/usr/sbin/screencapture",
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false
                };
                startInfo.ArgumentList.Add("-x");
                startInfo.ArgumentList.Add(screenshotPath);

                using var process = Process.Start(startInfo);
                if (process is null)
                {
                    return null;
                }

                process.WaitForExit();
                return process.ExitCode == 0 && File.Exists(screenshotPath) ? screenshotPath : null;
            }
            catch
            {
                return null;
            }
        }

        public static string? TryCreateImageThumbnail(string? imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            {
                return null;
            }

            try
            {
                var info = new FileInfo(imagePath);
                var thumbnailDirectory = Path.Combine(
                    MomentTracePaths.ThumbnailsDirectory);
                Directory.CreateDirectory(thumbnailDirectory);

                var cacheKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{info.FullName}|{info.LastWriteTimeUtc.Ticks}|{info.Length}")));
                var thumbnailPath = Path.Combine(thumbnailDirectory, $"{cacheKey}.jpg");
                if (File.Exists(thumbnailPath))
                {
                    return thumbnailPath;
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = "/usr/bin/sips",
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false
                };
                startInfo.ArgumentList.Add("-s");
                startInfo.ArgumentList.Add("format");
                startInfo.ArgumentList.Add("jpeg");
                startInfo.ArgumentList.Add("--resampleHeightWidthMax");
                startInfo.ArgumentList.Add("420");
                startInfo.ArgumentList.Add(imagePath);
                startInfo.ArgumentList.Add("--out");
                startInfo.ArgumentList.Add(thumbnailPath);

                using var process = Process.Start(startInfo);
                if (process is null)
                {
                    return null;
                }

                process.WaitForExit();
                return process.ExitCode == 0 && File.Exists(thumbnailPath) ? thumbnailPath : null;
            }
            catch
            {
                return null;
            }
        }

        private static async Task<string?> RunOsaScriptAsync(string script, CancellationToken cancellationToken)
        {
            return await RunCommandAsync("/usr/bin/osascript", cancellationToken, "-e", script);
        }

        private static async Task<string?> RunCommandAsync(string fileName, CancellationToken cancellationToken, params string[] arguments)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            };

            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return null;
            }

            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            var output = await outputTask;
            _ = await errorTask;

            return process.ExitCode == 0 ? output.Trim() : null;
        }
    }
}
