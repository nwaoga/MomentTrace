using MomentTrace.Core;
using MomentTrace.MacProbe;
using MomentTrace.Storage;

if (!OperatingSystem.IsMacOS())
{
    Console.WriteLine("MomentTrace.MacProbe only records on macOS.");
    Console.WriteLine("Run this project on a Mac to prove active app/window capture.");
    return;
}

var store = new SqliteActivityStore(SqliteActivityStore.DefaultDatabasePath);
await store.InitializeAsync();

await using var recorder = new MacForegroundActivityRecorder(store);
using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, args) =>
{
    args.Cancel = true;
    cancellation.Cancel();
};

recorder.ActivityChanged += (_, activity) =>
{
    Console.WriteLine($"{activity.StartedAt:HH:mm:ss} {activity.AppName} - {activity.DisplayTitle}");
};

Console.WriteLine("MomentTrace macOS recorder probe");
Console.WriteLine($"Database: {SqliteActivityStore.DefaultDatabasePath}");
Console.WriteLine("Press Ctrl+C to stop.");

await recorder.StartAsync(cancellation.Token);

try
{
    await Task.Delay(Timeout.InfiniteTimeSpan, cancellation.Token);
}
catch (OperationCanceledException)
{
}
finally
{
    await recorder.StopAsync();
}
