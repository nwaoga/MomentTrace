namespace MomentTrace.Core;

public sealed class PrivacyFilterOptions
{
    public bool ExcludeAdultWebsites { get; init; } = true;

    public IReadOnlyCollection<string> AdultDomains { get; init; } = [];

    public IReadOnlyCollection<string> CustomBlockedDomains { get; init; } = [];

    public IReadOnlyCollection<string> CustomAllowedDomains { get; init; } = [];
}
