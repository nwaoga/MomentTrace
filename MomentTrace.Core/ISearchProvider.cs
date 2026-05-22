namespace MomentTrace.Core;

public interface ISearchProvider
{
    bool Matches(TimelineActivity activity, string? query, IActivityClassifier classifier);
}
