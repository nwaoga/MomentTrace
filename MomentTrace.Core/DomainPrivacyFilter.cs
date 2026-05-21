namespace MomentTrace.Core;

public sealed class DomainPrivacyFilter(PrivacyFilterOptions options)
{
    private readonly HashSet<string> _adultDomains = NormalizeDomains(options.AdultDomains);
    private readonly HashSet<string> _blockedDomains = NormalizeDomains(options.CustomBlockedDomains);
    private readonly HashSet<string> _allowedDomains = NormalizeDomains(options.CustomAllowedDomains);

    public bool ShouldExclude(TimelineActivity activity)
    {
        return ShouldExclude(activity.Resource);
    }

    public bool ShouldExclude(string? resource)
    {
        if (!TryGetHost(resource, out var host))
        {
            return false;
        }

        if (MatchesAny(host, _allowedDomains))
        {
            return false;
        }

        return MatchesAny(host, _blockedDomains)
            || (options.ExcludeAdultWebsites && MatchesAny(host, _adultDomains));
    }

    private static bool TryGetHost(string? resource, out string host)
    {
        host = string.Empty;
        if (string.IsNullOrWhiteSpace(resource))
        {
            return false;
        }

        var candidate = resource.Trim();
        if (!candidate.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !candidate.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            candidate = $"https://{candidate}";
        }

        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri)
            || string.IsNullOrWhiteSpace(uri.Host))
        {
            return false;
        }

        host = NormalizeDomain(uri.Host);
        return host.Length > 0;
    }

    private static bool MatchesAny(string host, HashSet<string> domains)
    {
        if (domains.Count == 0)
        {
            return false;
        }

        for (var current = host; current.Length > 0;)
        {
            if (domains.Contains(current))
            {
                return true;
            }

            var dotIndex = current.IndexOf('.', StringComparison.Ordinal);
            if (dotIndex < 0 || dotIndex == current.Length - 1)
            {
                return false;
            }

            current = current[(dotIndex + 1)..];
        }

        return false;
    }

    private static HashSet<string> NormalizeDomains(IEnumerable<string> domains)
    {
        return domains
            .Select(ParseDomainLine)
            .Where(domain => !string.IsNullOrWhiteSpace(domain))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public static string ParseDomainLine(string line)
    {
        var candidate = line.Split('#', 2)[0].Trim();
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return string.Empty;
        }

        if (candidate.StartsWith("0.0.0.0 ", StringComparison.Ordinal)
            || candidate.StartsWith("127.0.0.1 ", StringComparison.Ordinal))
        {
            candidate = candidate.Split(' ', StringSplitOptions.RemoveEmptyEntries).ElementAtOrDefault(1) ?? string.Empty;
        }

        if (candidate.StartsWith("||", StringComparison.Ordinal))
        {
            candidate = candidate[2..];
        }

        candidate = candidate.TrimStart('.').TrimEnd('^').TrimEnd('/');
        if (Uri.TryCreate(candidate, UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.Host))
        {
            candidate = uri.Host;
        }

        return NormalizeDomain(candidate);
    }

    private static string NormalizeDomain(string domain)
    {
        return domain
            .Trim()
            .TrimEnd('.')
            .ToLowerInvariant();
    }
}
