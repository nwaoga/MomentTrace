using MomentTrace.Core;

namespace MomentTrace.Presentation;

public sealed class TimelinePresentationBuilder(
    IActivityClassifier classifier,
    ISessionSummarizer summarizer)
{
    private static readonly TimeSpan GroupGap = TimeSpan.FromMinutes(25);
    private static readonly TimeSpan WorkBlockGap = TimeSpan.FromMinutes(20);
    private static readonly TimeSpan LooseWorkBlockGap = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan MaxWorkBlockSpan = TimeSpan.FromHours(3);
    private readonly IActivityClassifier _classifier = classifier;
    private readonly ISessionSummarizer _summarizer = summarizer;

    public IReadOnlyList<ActivityMomentViewModel> BuildSessions(IReadOnlyList<TimelineActivity> activities, bool isSortedDescending = false)
    {
        var sessions = new List<ActivityMomentViewModel>();
        TimelineSessionBuilder? session = null;
        IEnumerable<TimelineActivity> orderedActivities = isSortedDescending
            ? activities
            : activities.OrderByDescending(activity => activity.StartedAt);

        foreach (var activity in orderedActivities)
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

    public IReadOnlyList<WorkBlockViewModel> BuildWorkBlocks(IReadOnlyList<ActivityMomentViewModel> sessions)
    {
        var blocks = new List<WorkBlockViewModel>();
        WorkBlockBuilder? block = null;

        foreach (var session in sessions.OrderBy(session => session.SessionStartedAt))
        {
            if (block is not null && block.CanInclude(session))
            {
                block.Include(session);
                continue;
            }

            if (block is not null)
            {
                blocks.Add(block.ToViewModel());
            }

            block = new WorkBlockBuilder(session, _classifier);
        }

        if (block is not null)
        {
            blocks.Add(block.ToViewModel());
        }

        return blocks;
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

    private sealed class WorkBlockBuilder
    {
        private readonly IActivityClassifier _classifier;
        private readonly List<ActivityMomentViewModel> _moments = [];
        private readonly DateTime _date;
        private DateTimeOffset _startedAt;
        private DateTimeOffset _endedAt;

        public WorkBlockBuilder(ActivityMomentViewModel moment, IActivityClassifier classifier)
        {
            _classifier = classifier;
            _date = moment.SessionStartedAt.LocalDateTime.Date;
            _startedAt = moment.SessionStartedAt;
            _endedAt = moment.SessionEndedAt;
            _moments.Add(moment);
        }

        public bool CanInclude(ActivityMomentViewModel moment)
        {
            if (moment.SessionStartedAt.LocalDateTime.Date != _date)
            {
                return false;
            }

            var gap = moment.SessionStartedAt - _endedAt;
            if (gap < TimeSpan.Zero)
            {
                gap = TimeSpan.Zero;
            }

            if (gap > WorkBlockGap || moment.SessionEndedAt - _startedAt > MaxWorkBlockSpan)
            {
                return false;
            }

            if (gap <= LooseWorkBlockGap && !IsSystemOnly(moment))
            {
                return true;
            }

            return SharesApp(moment)
                || SharesHost(moment)
                || HasCompatibleCategory(moment);
        }

        public void Include(ActivityMomentViewModel moment)
        {
            _moments.Add(moment);
            if (moment.SessionStartedAt < _startedAt)
            {
                _startedAt = moment.SessionStartedAt;
            }

            if (moment.SessionEndedAt > _endedAt)
            {
                _endedAt = moment.SessionEndedAt;
            }
        }

        public WorkBlockViewModel ToViewModel()
        {
            return new WorkBlockViewModel(_startedAt, _endedAt, GetDominantCategory(), _moments.ToList());
        }

        private bool SharesApp(ActivityMomentViewModel moment)
        {
            return _moments.Any(existing =>
                !string.IsNullOrWhiteSpace(existing.AppName)
                && string.Equals(existing.AppName, moment.AppName, StringComparison.OrdinalIgnoreCase));
        }

        private bool SharesHost(ActivityMomentViewModel moment)
        {
            if (!TryGetHost(moment.Resource, out var host))
            {
                return false;
            }

            return _moments.Any(existing =>
                TryGetHost(existing.Resource, out var existingHost)
                && string.Equals(existingHost, host, StringComparison.OrdinalIgnoreCase));
        }

        private bool HasCompatibleCategory(ActivityMomentViewModel moment)
        {
            var category = _classifier.Classify(moment.Activity).Category;
            return _moments
                .Select(existing => _classifier.Classify(existing.Activity).Category)
                .Any(existingCategory => CategoriesCanShareWorkBlock(existingCategory, category));
        }

        private ActivityCategory GetDominantCategory()
        {
            return _moments
                .Select(moment => _classifier.Classify(moment.Activity).Category)
                .GroupBy(category => category)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => GetCategoryPriority(group.Key))
                .Select(group => group.Key)
                .FirstOrDefault();
        }

        private bool IsSystemOnly(ActivityMomentViewModel moment)
        {
            return _classifier.Classify(moment.Activity).Category == ActivityCategory.System
                && _moments.All(existing => _classifier.Classify(existing.Activity).Category == ActivityCategory.System);
        }

        private static bool CategoriesCanShareWorkBlock(ActivityCategory left, ActivityCategory right)
        {
            if (left == right)
            {
                return true;
            }

            return IsResearchLike(left) && IsResearchLike(right)
                || IsMakingContext(left) && IsMakingContext(right);
        }

        private static bool IsResearchLike(ActivityCategory category)
        {
            return category is ActivityCategory.Browsing or ActivityCategory.Research or ActivityCategory.Documents;
        }

        private static bool IsMakingContext(ActivityCategory category)
        {
            return category is ActivityCategory.Development or ActivityCategory.Research or ActivityCategory.Documents or ActivityCategory.ScreenshotCapture;
        }

        private static int GetCategoryPriority(ActivityCategory category)
        {
            return category switch
            {
                ActivityCategory.Development => 0,
                ActivityCategory.Research => 1,
                ActivityCategory.Documents => 2,
                ActivityCategory.Browsing => 3,
                ActivityCategory.Communication => 4,
                ActivityCategory.ScreenshotCapture => 5,
                ActivityCategory.Other => 6,
                _ => 7
            };
        }

        private static bool TryGetHost(string? resource, out string host)
        {
            host = string.Empty;
            if (string.IsNullOrWhiteSpace(resource))
            {
                return false;
            }

            var candidate = resource.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? resource : $"https://{resource}";
            if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri) || string.IsNullOrWhiteSpace(uri.Host))
            {
                return false;
            }

            host = uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? uri.Host[4..] : uri.Host;
            return true;
        }
    }
}
