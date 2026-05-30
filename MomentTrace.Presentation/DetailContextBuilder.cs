using MomentTrace.Core;

namespace MomentTrace.Presentation;

public sealed class DetailContextBuilder(IActivityClassifier classifier)
{
    private readonly IActivityClassifier _classifier = classifier;

    public DetailContextModel Build(ActivityMomentViewModel selected, IReadOnlyList<TimelineActivity> activities)
    {
        var rawEntries = activities
            .Where(activity => IsRawEntryForMoment(selected, activity))
            .OrderBy(activity => activity.StartedAt)
            .ToList();

        if (rawEntries.Count == 0)
        {
            rawEntries.Add(selected.Activity);
        }

        var selectedCategory = _classifier.Classify(selected.Activity).Category;
        var related = activities
            .Where(activity => activity.Id != selected.Id)
            .Select(activity => new
            {
                Activity = activity,
                Category = _classifier.Classify(activity).Category,
                Distance = Math.Abs((activity.StartedAt - selected.SessionStartedAt).TotalSeconds)
            })
            .Where(item => string.Equals(item.Activity.AppName, selected.AppName, StringComparison.OrdinalIgnoreCase)
                || item.Category == selectedCategory
                || SameMomentTarget(item.Activity, selected.Activity))
            .OrderBy(item => item.Distance)
            .Take(6)
            .Select(item => new RelatedMomentViewModel(
                new ActivityMomentViewModel(item.Activity),
                item.Category == selectedCategory ? "Same activity type" : "Same source"))
            .ToList();

        return new DetailContextModel
        {
            Narrative = GetDetailNarrative(selected),
            RawEntries = rawEntries.Select(activity => new DetailActivityRowViewModel(activity)).ToList(),
            RawEntriesSummary = rawEntries.Count == 1
                ? "This moment currently has one captured entry."
                : $"{rawEntries.Count} raw entries were grouped into this remembered moment.",
            RelatedMoments = related,
            RelatedSummary = related.Count == 0
                ? "No related moments found yet."
                : $"{related.Count} nearby moments from the same source or category."
        };
    }

    private static bool IsRawEntryForMoment(ActivityMomentViewModel selected, TimelineActivity activity)
    {
        if (activity.Id == selected.Id)
        {
            return true;
        }

        return activity.StartedAt.LocalDateTime.Date == selected.SessionStartedAt.LocalDateTime.Date
            && activity.StartedAt >= selected.SessionStartedAt.AddMinutes(-1)
            && activity.EndedAt <= selected.SessionEndedAt.AddMinutes(1)
            && SameMomentTarget(activity, selected.Activity);
    }

    private static bool SameMomentTarget(TimelineActivity left, TimelineActivity right)
    {
        if (!string.Equals(left.ProcessName, right.ProcessName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var leftTarget = GetMomentTarget(left);
        var rightTarget = GetMomentTarget(right);
        return string.Equals(leftTarget, rightTarget, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetMomentTarget(TimelineActivity activity)
    {
        if (!string.IsNullOrWhiteSpace(activity.Resource))
        {
            return NormalizeResource(activity.Resource);
        }

        return string.IsNullOrWhiteSpace(activity.WindowTitle)
            ? activity.AppName
            : activity.WindowTitle.Trim();
    }

    private static string NormalizeResource(string resource)
    {
        if (!Uri.TryCreate(resource.Trim(), UriKind.Absolute, out var uri))
        {
            return resource.Trim().TrimEnd('/');
        }

        return $"{uri.Host}{uri.AbsolutePath.TrimEnd('/')}";
    }

    private static string GetDetailNarrative(ActivityMomentViewModel selected)
    {
        return selected.Category switch
        {
            ActivityCategory.Browsing => $"Browsed {selected.Location} during this remembered moment.",
            ActivityCategory.Research => $"Reviewed and compared web context around {selected.Location}.",
            ActivityCategory.Development => "Worked in a development context with code, tooling, or project windows.",
            ActivityCategory.Communication => "Moved through communication context, messages, or collaboration tools.",
            ActivityCategory.Documents => "Reviewed or edited document-based work.",
            ActivityCategory.ScreenshotCapture => "Captured a screenshot or visual reference for later recall.",
            _ => selected.MemoryDescription
        };
    }
}
