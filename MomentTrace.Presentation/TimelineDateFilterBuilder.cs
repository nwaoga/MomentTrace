using MomentTrace.Core;

namespace MomentTrace.Presentation;

public static class TimelineDateFilterBuilder
{
    public static IReadOnlyList<DateFilterOption> Build(IReadOnlyList<TimelineActivity> activities, DateTime today)
    {
        return activities
            .Select(activity => activity.StartedAt.LocalDateTime.Date)
            .Append(today)
            .Append(today.AddDays(-1))
            .Distinct()
            .OrderByDescending(date => date)
            .Select(date => new DateFilterOption(date, FormatDateLabel(date, today)))
            .ToList();
    }

    public static string FormatDateLabel(DateTime date, DateTime today)
    {
        if (date == today)
        {
            return "Today";
        }

        if (date == today.AddDays(-1))
        {
            return "Yesterday";
        }

        return date.ToString("ddd, d MMM");
    }
}

public sealed record DateFilterOption(DateTime Date, string Label)
{
    public override string ToString()
    {
        return Label;
    }
}
