using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PNMC.Contracts;
using PNMC.Infrastructure.Data;

namespace PNMC.Infrastructure.Integrations.Participation;

public sealed class DbParticipationSubmissionStore : IParticipationSubmissionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly PnmcDbContext _dbContext;
    private readonly ILogger<DbParticipationSubmissionStore> _logger;

    public DbParticipationSubmissionStore(
        PnmcDbContext dbContext,
        ILogger<DbParticipationSubmissionStore> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task SaveAsync(ParticipationSubmissionEntity entity, CancellationToken cancellationToken = default)
    {
        var row = await _dbContext.Participations
            .FirstOrDefaultAsync(x => x.Reference == entity.Reference, cancellationToken);

        var payload = entity.Payload ?? new ParticipationSubmissionRequest();
        var actorName = string.IsNullOrWhiteSpace(payload.ActorName)
            ? $"{payload.IndividualFirstName} {payload.IndividualLastName}".Trim()
            : payload.ActorName;

        if (row is null)
        {
            row = new ParticipationSubmissionRow
            {
                Reference = entity.Reference,
                SubmittedAt = entity.SubmittedAt
            };
            _dbContext.Participations.Add(row);
        }

        row.ActorType = payload.ActorType;
        row.ActorName = actorName;
        row.Email = payload.Email;
        row.Department = payload.Department;
        row.Municipality = payload.Municipality;
        row.PayloadJson = JsonSerializer.Serialize(payload, JsonOptions);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<ParticipationSubmissionEntity?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return null;
        }

        var row = await _dbContext.Participations
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Reference == reference, cancellationToken);

        return row is null ? null : ToEntity(row);
    }

    public async Task<IReadOnlyList<ParticipationSubmissionEntity>> ListAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _dbContext.Participations
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return rows
            .OrderByDescending(x => x.SubmittedAt)
            .Select(ToEntity)
            .ToList();
    }

    private ParticipationSubmissionEntity ToEntity(ParticipationSubmissionRow row)
    {
        ParticipationSubmissionRequest payload;
        try
        {
            payload = JsonSerializer.Deserialize<ParticipationSubmissionRequest>(row.PayloadJson, JsonOptions) ?? new ParticipationSubmissionRequest();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Payload JSON inválido para referencia {Reference}", row.Reference);
            payload = new ParticipationSubmissionRequest
            {
                ActorType = row.ActorType,
                ActorName = row.ActorName,
                Email = row.Email,
                Department = row.Department,
                Municipality = row.Municipality
            };
        }

        return new ParticipationSubmissionEntity
        {
            Reference = row.Reference,
            SubmittedAt = row.SubmittedAt,
            ExternalSyncStatus = "backend_only",
            ExternalSyncMessage = "Persistido en base de datos del backend.",
            Payload = payload
        };
    }
}
