namespace MomentTrace.Core;

public sealed class LocalMetadataSearchProvider : ISearchProvider
{
    public static LocalMetadataSearchProvider Default { get; } = new();

    public bool Matches(TimelineActivity activity, string? query, IActivityClassifier classifier)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        var classification = classifier.Classify(activity);
        var haystack = string.Join(
            " ",
            activity.AppName,
            activity.WindowTitle,
            activity.ProcessName,
            activity.Resource,
            classification.Title,
            classification.Description,
            classification.Category);

        return query
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .All(term => haystack.Contains(term, StringComparison.OrdinalIgnoreCase));
    }
}
