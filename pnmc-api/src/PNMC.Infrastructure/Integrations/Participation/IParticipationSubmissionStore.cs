namespace PNMC.Infrastructure.Integrations.Participation;

public interface IParticipationSubmissionStore
{
    Task SaveAsync(ParticipationSubmissionEntity entity, CancellationToken cancellationToken = default);
    Task<ParticipationSubmissionEntity?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ParticipationSubmissionEntity>> ListAsync(CancellationToken cancellationToken = default);
}
