namespace MomentTrace.Core;

public interface IStartupRegistration
{
    bool IsEnabled();

    void SetEnabled(bool enabled);
}
