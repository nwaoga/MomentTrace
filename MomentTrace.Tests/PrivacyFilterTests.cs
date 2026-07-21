using MomentTrace.Core;

namespace MomentTrace.Tests;

public sealed class PrivacyFilterTests
{
    [Fact]
    public void DomainPrivacyFilter_ExcludesBlockedAndAdultDomains_ButAllowsExplicitAllowList()
    {
        var filter = new DomainPrivacyFilter(new PrivacyFilterOptions
        {
            ExcludeAdultWebsites = true,
            AdultDomains = ["adult.example", "pornhub.com"],
            CustomBlockedDomains = ["blocked.example"],
            CustomAllowedDomains = ["safe.blocked.example", "docs.pornhub.com"]
        });

        Assert.True(filter.ShouldExclude("https://blocked.example/page"));
        Assert.True(filter.ShouldExclude("sub.blocked.example"));
        Assert.True(filter.ShouldExclude("https://video.pornhub.com/watch"));
        Assert.False(filter.ShouldExclude("https://safe.blocked.example/guide"));
        Assert.False(filter.ShouldExclude("https://docs.pornhub.com/article"));
        Assert.False(filter.ShouldExclude("not a url"));
    }

    [Fact]
    public void DomainPrivacyFilter_ParseDomainLine_NormalizesCommonBlocklistFormats()
    {
        Assert.Equal("example.com", DomainPrivacyFilter.ParseDomainLine("0.0.0.0 example.com"));
        Assert.Equal("example.com", DomainPrivacyFilter.ParseDomainLine("127.0.0.1 example.com"));
        Assert.Equal("tracking.example.com", DomainPrivacyFilter.ParseDomainLine("||tracking.example.com^"));
        Assert.Equal("docs.example.com", DomainPrivacyFilter.ParseDomainLine("https://docs.example.com/path"));
        Assert.Equal(string.Empty, DomainPrivacyFilter.ParseDomainLine("# comment only"));
    }

    [Fact]
    public void UserPrivacyFilter_ExcludesConfiguredAppsAcrossProcessAppAndExecutableNames()
    {
        var filter = new UserPrivacyFilter(new PrivacyFilterOptions
        {
            CustomBlockedApps = ["Code", "Slack.exe"]
        });

        Assert.True(filter.ShouldExclude("code", "Visual Studio Code", null));
        Assert.True(filter.ShouldExclude("explorer", "Slack", @"C:\Users\me\AppData\Local\slack.exe"));
        Assert.False(filter.ShouldExclude("explorer", "Notepad", @"C:\Windows\notepad.exe"));
    }

    [Fact]
    public void UserPrivacyFilter_ExcludesActivityWhenEitherAppOrDomainMatches()
    {
        var filter = new UserPrivacyFilter(new PrivacyFilterOptions
        {
            CustomBlockedApps = ["Discord"],
            CustomBlockedDomains = ["blocked.example"]
        });

        var blockedAppActivity = CreateActivity(resource: "https://safe.example", processName: "discord", appName: "Discord");
        var blockedDomainActivity = CreateActivity(resource: "https://blocked.example/home", processName: "chrome", appName: "Chrome");
        var safeActivity = CreateActivity(resource: "https://safe.example", processName: "chrome", appName: "Chrome");

        Assert.True(filter.ShouldExclude(blockedAppActivity));
        Assert.True(filter.ShouldExclude(blockedDomainActivity));
        Assert.False(filter.ShouldExclude(safeActivity));
    }

    [Fact]
    public void ActivityExclusionFilter_ExcludesSelfAndLockScreenSurfaces()
    {
        Assert.True(ActivityExclusionFilter.IsExcluded("momenttrace", "MomentTrace", "MomentTrace"));
        Assert.True(ActivityExclusionFilter.IsExcluded("MomentTrace.Desktop", "MomentTrace", "MomentTrace"));
        Assert.True(ActivityExclusionFilter.IsExcluded("MomentTrace.Desktop.exe", "MomentTrace", "MomentTrace"));
        Assert.True(ActivityExclusionFilter.IsExcluded("explorer", "MomentTrace", "MomentTrace"));
        Assert.True(ActivityExclusionFilter.IsExcluded("explorer", "Other", "Other", @"C:\Apps\MomentTrace.Desktop.exe"));
        Assert.True(ActivityExclusionFilter.IsExcluded("explorer", "Windows", "Default lock screen"));
        Assert.False(ActivityExclusionFilter.IsExcluded("code", "Codex", "Working on tests"));
    }

    private static TimelineActivity CreateActivity(string? resource, string processName, string appName)
    {
        return new TimelineActivity
        {
            Id = 1,
            AppName = appName,
            WindowTitle = appName,
            ProcessName = processName,
            Resource = resource,
            Kind = ActivityKind.App,
            StartedAt = DateTimeOffset.UtcNow,
            EndedAt = DateTimeOffset.UtcNow.AddMinutes(1)
        };
    }
}
