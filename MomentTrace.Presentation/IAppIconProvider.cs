namespace MomentTrace.Presentation;

public interface IAppIconProvider<TIcon>
{
    TIcon? GetIcon(string? executablePath, string? appName, string? processName);
}
