namespace MomentTrace.Core;

public interface IActivityRecorder : IAsyncDisposable
{
    bool IsRecording { get; }

    event EventHandler<TimelineActivity>? ActivityChanged;

    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);
}
