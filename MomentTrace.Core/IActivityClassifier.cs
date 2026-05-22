namespace MomentTrace.Core;

public interface IActivityClassifier
{
    ActivityClassification Classify(TimelineActivity activity);
}
