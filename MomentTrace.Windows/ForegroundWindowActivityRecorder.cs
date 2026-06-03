using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
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
    private static readonly TimeSpan ActivityGapThreshold = TimeSpan.FromMinutes(5);

    private readonly IActivityStore _store = store;
    private readonly Func<PrivacyFilterOptions>? _privacyOptionsProvider = privacyOptionsProvider;
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

        if (ShouldExcludeByPrivacyFilter(snapshot))
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
        var screenshotPath = Snapshot.TryCaptureScreenshot(snapshot.WindowHandle);
        var thumbnailPath = Snapshot.TryCreateImageThumbnail(screenshotPath ?? snapshot.ImageFilePath);

        var activity = new TimelineActivity
        {
            AppName = snapshot.AppName,
            WindowTitle = snapshot.WindowTitle,
            ProcessName = snapshot.ProcessName,
            ExecutablePath = snapshot.ExecutablePath,
            Resource = snapshot.Resource,
            ScreenshotPath = screenshotPath,
            ThumbnailPath = thumbnailPath,
            Kind = snapshot.Kind,
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

    private bool ShouldExcludeByPrivacyFilter(Snapshot snapshot)
    {
        if (_privacyOptionsProvider is null)
        {
            return false;
        }

        try
        {
            return new UserPrivacyFilter(_privacyOptionsProvider()).ShouldExclude(
                snapshot.Resource,
                snapshot.ProcessName,
                snapshot.AppName,
                snapshot.ExecutablePath);
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
        string? ImageFilePath,
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
                var imageFilePath = TryFindImageFile(title);

                return new Snapshot(handle, processName, appName, title.Trim(), executablePath, resource, imageFilePath, kind);
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
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "MomentTrace",
                    "thumbnails");
                Directory.CreateDirectory(thumbnailDirectory);

                var cacheKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{info.FullName}|{info.LastWriteTimeUtc.Ticks}|{info.Length}")));
                var thumbnailPath = Path.Combine(thumbnailDirectory, $"{cacheKey}.jpg");
                if (File.Exists(thumbnailPath))
                {
                    return thumbnailPath;
                }

                using var source = Image.FromFile(info.FullName);
                var maxWidth = 420;
                var maxHeight = 260;
                var scale = Math.Min((double)maxWidth / source.Width, (double)maxHeight / source.Height);
                scale = Math.Min(1.0, scale);
                var width = Math.Max(1, (int)Math.Round(source.Width * scale));
                var height = Math.Max(1, (int)Math.Round(source.Height * scale));

                using var thumbnail = new Bitmap(width, height);
                using (var graphics = Graphics.FromImage(thumbnail))
                {
                    graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                    graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    graphics.DrawImage(source, 0, 0, width, height);
                }

                thumbnail.Save(thumbnailPath, ImageFormat.Jpeg);
                return thumbnailPath;
            }
            catch
            {
                return null;
            }
        }

        private static string? TryFindImageFile(string windowTitle)
        {
            var fileName = TryExtractImageFileName(windowTitle);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return null;
            }

            if (Path.IsPathRooted(fileName) && File.Exists(fileName))
            {
                return fileName;
            }

            foreach (var root in GetImageSearchRoots())
            {
                try
                {
                    var directPath = Path.Combine(root, fileName);
                    if (File.Exists(directPath))
                    {
                        return directPath;
                    }

                    foreach (var path in Directory.EnumerateFiles(root, fileName, SearchOption.AllDirectories).Take(1))
                    {
                        return path;
                    }
                }
                catch
                {
                }
            }

            return null;
        }

        private static string? TryExtractImageFileName(string windowTitle)
        {
            var extensions = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp", ".tif", ".tiff" };
            var parts = windowTitle
                .Split([" - ", " – ", " — ", " | "], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Append(windowTitle);

            foreach (var part in parts)
            {
                foreach (var extension in extensions)
                {
                    var index = part.LastIndexOf(extension, StringComparison.OrdinalIgnoreCase);
                    if (index < 0)
                    {
                        continue;
                    }

                    var candidate = part[..(index + extension.Length)].Trim().Trim('"');
                    var invalid = Path.GetInvalidFileNameChars();
                    if (candidate.IndexOfAny(invalid) >= 0 && !Path.IsPathRooted(candidate))
                    {
                        continue;
                    }

                    return candidate;
                }
            }

            return null;
        }

        private static IEnumerable<string> GetImageSearchRoots()
        {
            var roots = new List<string?>
            {
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads")
            };

            var oneDrive = Environment.GetEnvironmentVariable("OneDrive");
            if (!string.IsNullOrWhiteSpace(oneDrive))
            {
                roots.Add(Path.Combine(oneDrive, "Pictures"));
                roots.Add(Path.Combine(oneDrive, "Desktop"));
                roots.Add(Path.Combine(oneDrive, "Documents"));
                roots.Add(Path.Combine(oneDrive, "Downloads"));
            }

            return roots
                .Where(root => !string.IsNullOrWhiteSpace(root) && Directory.Exists(root))
                .Distinct(StringComparer.OrdinalIgnoreCase)!;
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
            ThumbnailPath = activity.ThumbnailPath,
            Kind = activity.Kind,
            StartedAt = activity.StartedAt,
            EndedAt = activity.EndedAt,
            IsPinned = activity.IsPinned,
            Note = activity.Note,
            IsBookmarked = activity.IsBookmarked
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
