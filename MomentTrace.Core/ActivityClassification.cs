namespace MomentTrace.Core;

public sealed record ActivityClassification(
    ActivityCategory Category,
    string Title,
    string Description,
    string AccentColor,
    string Icon);
