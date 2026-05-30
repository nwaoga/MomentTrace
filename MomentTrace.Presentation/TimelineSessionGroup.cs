using System.ComponentModel;
using MomentTrace.Core;

namespace MomentTrace.Presentation;

public sealed class TimelineSessionGroup : INotifyPropertyChanged
{
    private const int PreviewActivityCount = 4;
    private bool _isExpanded;

    public TimelineSessionGroup(
        DateTimeOffset startedAt,
        DateTimeOffset endedAt,
        ActivityCategory category,
        IReadOnlyList<ActivityMomentViewModel> activities,
        ISessionSummarizer summarizer)
    {
        Activities = activities;
        Name = summarizer.GetSessionTitle(category, activities.Select(activity => activity.Activity).ToList());
        Summary = summarizer.GetSessionDescription(category, activities.Select(activity => activity.Activity).ToList());
        TimeRange = $"{startedAt.LocalDateTime:HH:mm} - {endedAt.LocalDateTime:HH:mm}";
        AccentBrush = category switch
        {
            ActivityCategory.Browsing => "#5CA8FF",
            ActivityCategory.Research => "#7C5CFF",
            ActivityCategory.Development => "#71C562",
            ActivityCategory.Communication => "#67D4B1",
            ActivityCategory.Documents => "#E7B84E",
            ActivityCategory.ScreenshotCapture => "#B89CFF",
            _ => "#8D9BB0"
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Name { get; }

    public string Summary { get; }

    public string TimeRange { get; }

    public string AccentBrush { get; }

    public IReadOnlyList<ActivityMomentViewModel> Activities { get; }

    public int ActivityCount => Activities.Count;

    public IReadOnlyList<ActivityMomentViewModel> VisibleActivities =>
        _isExpanded ? Activities : Activities.Take(PreviewActivityCount).ToList();

    public string ToggleText => _isExpanded ? "Show less" : "View all";

    public string ToggleGlyph => _isExpanded ? "⌃" : "⌄";

    public bool HasMoreThanPreviewCount => Activities.Count > PreviewActivityCount;

    public void ToggleExpanded()
    {
        _isExpanded = !_isExpanded;
        OnPropertyChanged(nameof(VisibleActivities));
        OnPropertyChanged(nameof(ToggleText));
        OnPropertyChanged(nameof(ToggleGlyph));
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
