using PNMC.Contracts;

namespace PNMC.Infrastructure.Integrations.Participation;

public sealed class ParticipationSubmissionEntity
{
    public string Reference { get; init; } = string.Empty;
    public DateTimeOffset SubmittedAt { get; init; }
    public string ExternalSyncStatus { get; init; } = "not_configured";
    public string ExternalSyncMessage { get; init; } = string.Empty;
    public ParticipationSubmissionRequest Payload { get; init; } = new();
}
