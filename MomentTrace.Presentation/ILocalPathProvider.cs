namespace MomentTrace.Presentation;

public interface ILocalPathProvider
{
    string DatabasePath { get; }

    string SettingsPath { get; }

    string ScreenshotDirectory { get; }

    string ThumbnailDirectory { get; }
}
