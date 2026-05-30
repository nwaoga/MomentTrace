using Avalonia.Controls;
using MomentTrace.Core;
using MomentTrace.Presentation;
using MomentTrace.Storage;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace MomentTrace.Desktop;

public partial class MainWindow : Window
{
    private readonly TimelinePresentationBuilder _timelineBuilder = new(
        RuleBasedActivityClassifier.Default,
        RuleBasedSessionSummarizer.Default);
    private readonly OverviewPresentationBuilder _overviewBuilder;

    public MainWindow()
    {
        InitializeComponent();
        _overviewBuilder = new OverviewPresentationBuilder(_timelineBuilder);
        Opened += async (_, _) => await LoadTimelineAsync();
    }

    private async Task LoadTimelineAsync()
    {
        var store = new SqliteActivityStore(SqliteActivityStore.DefaultDatabasePath);
        await store.InitializeAsync();

        var activities = await store.QueryAsync(new TimelineQuery { Limit = 1000 });
        var visible = activities
            .Where(activity => !ActivityExclusionFilter.IsExcluded(activity))
            .ToList();
        var sessions = _timelineBuilder.BuildSessions(visible).Take(30).ToList();
        var overview = _overviewBuilder.Build(visible, DateTime.Today);

        StatusText.Text = $"{visible.Count} captured rows found in local storage";
        SummaryText.Text = $"{overview.Sessions.Count} moments today · {OverviewPresentationBuilder.FormatDuration(overview.TotalActive)} remembered span";
        MomentsList.ItemsSource = sessions.Select(session => new MomentRow(
            session.Title,
            $"{session.CardTime} · {session.CategoryLabel} · {session.Location}"));
    }

    private sealed record MomentRow(string Title, string Subtitle);
}
