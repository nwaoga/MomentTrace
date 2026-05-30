using MomentTrace.Core;

namespace MomentTrace.Presentation;

public sealed class TimelinePresentationBuilder(
    IActivityClassifier classifier,
    ISessionSummarizer summarizer)
{
    private static readonly TimeSpan GroupGap = TimeSpan.FromMinutes(25);
    private readonly IActivityClassifier _classifier = classifier;
    private readonly ISessionSummarizer _summarizer = summarizer;

    public IReadOnlyList<ActivityMomentViewModel> BuildSessions(IReadOnlyList<TimelineActivity> activities)
    {
        var sessions = new List<ActivityMomentViewModel>();
        TimelineSessionBuilder? session = null;

        foreach (var activity in activities.OrderByDescending(activity => activity.StartedAt))
        {
            if (session is not null && session.CanInclude(activity))
            {
                session.Include(activity);
                continue;
            }

            if (session is not null)
            {
                sessions.Add(session.ToViewModel());
            }

            session = new TimelineSessionBuilder(activity);
        }

        if (session is not null)
        {
            sessions.Add(session.ToViewModel());
        }

        return sessions;
    }

    public IReadOnlyList<TimelineSessionGroup> BuildGroups(IReadOnlyList<ActivityMomentViewModel> sessions)
    {
        var groups = new List<TimelineSessionGroup>();
        TimelineGroupBuilder? group = null;

        foreach (var session in sessions)
        {
            if (group is not null && group.CanInclude(session))
            {
                group.Include(session);
                continue;
            }

            if (group is not null)
            {
                groups.Add(group.ToViewModel());
            }

            group = new TimelineGroupBuilder(session, _classifier, _summarizer);
        }

        if (group is not null)
        {
            groups.Add(group.ToViewModel());
        }

        return groups;
    }

    private sealed class TimelineSessionBuilder
    {
        private readonly string _key;
        private readonly DateTime _date;
        private TimelineActivity _representative;
        private DateTimeOffset _startedAt;
        private DateTimeOffset _endedAt;
        private int _count;

        public TimelineSessionBuilder(TimelineActivity activity)
        {
            _representative = activity;
            _startedAt = activity.StartedAt;
            _endedAt = activity.EndedAt;
            _count = 1;
            _date = activity.StartedAt.LocalDateTime.Date;
            _key = GetSessionKey(activity);
        }

        public bool CanInclude(TimelineActivity activity)
        {
            return activity.StartedAt.LocalDateTime.Date == _date
                && string.Equals(GetSessionKey(activity), _key, StringComparison.OrdinalIgnoreCase);
        }

        public void Include(TimelineActivity activity)
        {
            _count++;

            if (activity.StartedAt < _startedAt)
            {
                _startedAt = activity.StartedAt;
            }

            if (activity.EndedAt > _endedAt)
            {
                _endedAt = activity.EndedAt;
            }

            if (activity.StartedAt > _representative.StartedAt)
            {
                _representative = activity;
            }
        }

        public ActivityMomentViewModel ToViewModel()
        {
            return new(_representative, _startedAt, _endedAt, _count);
        }

        private static string GetSessionKey(TimelineActivity activity)
        {
            return string.Join(
                "|",
                activity.Kind,
                activity.ProcessName.Trim(),
                NormalizeSessionTarget(activity));
        }

        private static string NormalizeSessionTarget(TimelineActivity activity)
        {
            if (activity.Kind == ActivityKind.Browser && !string.IsNullOrWhiteSpace(activity.Resource))
            {
                return NormalizeBrowserResource(activity.Resource);
            }

            if (!string.IsNullOrWhiteSpace(activity.ExecutablePath))
            {
                return activity.ExecutablePath.Trim();
            }

            return activity.ProcessName.Trim();
        }

        private static string NormalizeBrowserResource(string resource)
        {
            if (!Uri.TryCreate(resource.Trim(), UriKind.Absolute, out var uri))
            {
                return resource.Trim().TrimEnd('/');
            }

            var path = uri.AbsolutePath.TrimEnd('/');
            return string.IsNullOrWhiteSpace(path)
                ? uri.Host
                : $"{uri.Host}{path}";
        }
    }

    private sealed class TimelineGroupBuilder
    {
        private readonly IActivityClassifier _classifier;
        private readonly ISessionSummarizer _summarizer;
        private readonly List<ActivityMomentViewModel> _activities = [];
        private readonly DateTime _date;
        private readonly ActivityCategory _category;
        private DateTimeOffset _startedAt;
        private DateTimeOffset _endedAt;

        public TimelineGroupBuilder(ActivityMomentViewModel activity, IActivityClassifier classifier, ISessionSummarizer summarizer)
        {
            _classifier = classifier;
            _summarizer = summarizer;
            _date = activity.SessionStartedAt.LocalDateTime.Date;
            _category = classifier.Classify(activity.Activity).Category;
            _startedAt = activity.SessionStartedAt;
            _endedAt = activity.SessionEndedAt;
            _activities.Add(activity);
        }

        public bool CanInclude(ActivityMomentViewModel activity)
        {
            var category = _classifier.Classify(activity.Activity).Category;
            return activity.SessionStartedAt.LocalDateTime.Date == _date
                && category == _category
                && _startedAt - activity.SessionEndedAt <= GroupGap
                && _activities.Count < 8;
        }

        public void Include(ActivityMomentViewModel activity)
        {
            _activities.Add(activity);

            if (activity.SessionStartedAt < _startedAt)
            {
                _startedAt = activity.SessionStartedAt;
            }

            if (activity.SessionEndedAt > _endedAt)
            {
                _endedAt = activity.SessionEndedAt;
            }
        }

        public TimelineSessionGroup ToViewModel()
        {
            return new(_startedAt, _endedAt, _category, _activities.ToList(), _summarizer);
        }
    }
}
