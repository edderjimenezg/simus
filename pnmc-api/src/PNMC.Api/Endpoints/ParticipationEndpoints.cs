using PNMC.Api.Security;
using PNMC.Contracts;
using PNMC.Infrastructure.Integrations.Participation;
using PNMC.Infrastructure.Common;
using System.Security.Cryptography;

namespace PNMC.Api.Endpoints;

public static class ParticipationEndpoints
{
    private const int ShortTextMaxLength = 200;
    private const int LongTextMaxLength = 2500;

    private static readonly HashSet<string> AllowedActorTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "individual",
        "collective",
        "organization",
        "festival",
        "market",
        "space"
    };

    public static RouteGroupBuilder MapParticipationEndpoints(this RouteGroupBuilder group)
    {
        var api = group.MapGroup("/participaciones").WithTags("participaciones");

        api.MapGet(string.Empty, async (
            IParticipationSubmissionStore store,
            string? actorType,
            string? department,
            string? q,
            int? limit,
            int? offset,
            CancellationToken cancellationToken) =>
        {
            var all = await store.ListAsync(cancellationToken);

            var filtered = all.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(actorType))
            {
                filtered = filtered.Where(item =>
                    string.Equals(item.Payload.ActorType, actorType, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(department))
            {
                filtered = filtered.Where(item =>
                    item.Payload.Department.Contains(department, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                filtered = filtered.Where(item =>
                    item.Reference.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || (item.Payload.ActorName ?? string.Empty).Contains(q, StringComparison.OrdinalIgnoreCase)
                    || (item.Payload.Email ?? string.Empty).Contains(q, StringComparison.OrdinalIgnoreCase));
            }

            var ordered = filtered.OrderByDescending(item => item.SubmittedAt).ToList();
            var safeLimit = Math.Clamp(limit ?? 50, 1, 200);
            var safeOffset = Math.Max(offset ?? 0, 0);
            var page = ordered
                .Skip(safeOffset)
                .Take(safeLimit)
                .Select(item =>
                {
                    var payload = item.Payload ?? new ParticipationSubmissionRequest();
                    return new ParticipationSubmissionSummaryDto(
                        item.Reference,
                        item.SubmittedAt,
                        payload.ActorType ?? string.Empty,
                        payload.ActorName ?? string.Empty,
                        payload.Email ?? string.Empty,
                        payload.Department ?? string.Empty,
                        payload.Municipality ?? string.Empty,
                        item.ExternalSyncStatus ?? "backend_only");
                })
                .ToList();

            return Results.Ok(new PagedResponse<ParticipationSubmissionSummaryDto>(page, safeLimit, safeOffset, ordered.Count));
        }).RequireAuthorization(SimusAuthentication.InstitutionalPolicy);

        // ANONIMA A PROPOSITO (8 de 39). Es la ficha de participacion ciudadana: la
        // llena cualquier persona del ecosistema musical desde el geovisor publico, sin
        // cuenta y sin tramite previo. Exigir sesion aqui no protegeria un dato de
        // nadie — el dato lo aporta quien lo envia — y en cambio cerraria el unico canal
        // de entrada de la ciudadania al PNMC.
        //
        // El reparto ya estaba escrito en este mismo fichero y es el que se conserva:
        // ESCRIBIR es publico (esta ruta), LEER es institucional
        // (GET /participation/submissions, arriba, con InstitutionalPolicy). Por eso el
        // expediente con correos y telefonos de terceros no se expone aunque el alta lo
        // este. Las defensas del alta no son la sesion sino Validate() y el limitador
        // RequireRateLimiting("participation-submit").
        api.MapPost(string.Empty, async (
            ParticipationSubmissionRequest request,
            IParticipationSubmissionStore store,
            CancellationToken cancellationToken) =>
        {
            var result = await CreateSubmissionAsync(request, store, cancellationToken);
            if (result.ErrorResult is not null)
            {
                return result.ErrorResult;
            }

            if (result.ValidationErrors.Count > 0)
            {
                return Results.ValidationProblem(result.ValidationErrors, statusCode: StatusCodes.Status400BadRequest);
            }

            return Results.Created($"/api/v1/participaciones/{result.Response!.Reference}", result.Response);
        }).RequireRateLimiting("participation-submit").AllowAnonymous();

        // ANONIMA A PROPOSITO (9 de 39). Consulta por radicado: quien envio una ficha
        // recibe una referencia y con ella comprueba que quedo registrada. Es el
        // acuse de recibo de un tramite anonimo, de modo que solo puede consultarse
        // sin sesion. Devuelve unicamente referencia, estado, fecha y estado de
        // sincronizacion — ningun campo del formulario, ningun dato de la persona—,
        // asi que conocer un radicado no revela a quien lo presento ni que conto.
        api.MapGet("/{reference}", async (string reference, IParticipationSubmissionStore store, CancellationToken cancellationToken) =>
        {
            var result = await store.FindByReferenceAsync(reference, cancellationToken);
            if (result is null)
            {
                return Results.NotFound();
            }

            return Results.Ok(new ParticipationSubmissionResponse(
                result.Reference,
                "accepted",
                result.SubmittedAt,
                "Registro encontrado.",
                result.ExternalSyncStatus,
                result.ExternalSyncMessage
            ));
        }).AllowAnonymous();

        return group;
    }

    private static Dictionary<string, string[]> Validate(ParticipationSubmissionRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (ValidationHelpers.IsMissing(request.ActorType) || !AllowedActorTypes.Contains(request.ActorType))
        {
            errors["actorType"] = ["Actor type is required and must be a supported value."];
        }

        if (ValidationHelpers.IsMissing(request.ActorName))
        {
            errors["actorName"] = ["Actor name is required."];
        }

        if (!ValidationHelpers.IsValidEmail(request.Email))
        {
            errors["email"] = ["A valid email is required."];
        }

        if (ValidationHelpers.IsMissing(request.Phone))
        {
            errors["phone"] = ["Phone is required."];
        }

        if (ValidationHelpers.IsMissing(request.Department))
        {
            errors["department"] = ["Department is required."];
        }

        if (ValidationHelpers.IsMissing(request.Municipality))
        {
            errors["municipality"] = ["Municipality is required."];
        }

        if (ValidationHelpers.IsMissing(request.MusicalFields))
        {
            errors["musicalFields"] = ["Musical fields is required."];
        }

        if (ValidationHelpers.IsMissing(request.Description))
        {
            errors["description"] = ["Description is required."];
        }

        if (ValidationHelpers.IsMissing(request.Contribution))
        {
            errors["contribution"] = ["Contribution is required."];
        }

        if (!request.Consent)
        {
            errors["consent"] = ["Consent is required."];
        }

        if (!ValidationHelpers.IsValidHttpUrl(request.Website))
        {
            errors["website"] = ["Website must be a valid HTTP/HTTPS URL."];
        }

        if (!ValidationHelpers.IsValidHttpUrl(request.FacebookUrl))
        {
            errors["facebookUrl"] = ["Facebook URL must be a valid HTTP/HTTPS URL."];
        }

        if (!ValidationHelpers.IsValidHttpUrl(request.InstagramUrl))
        {
            errors["instagramUrl"] = ["Instagram URL must be a valid HTTP/HTTPS URL."];
        }

        if (!string.IsNullOrWhiteSpace(request.Phone)
            && request.Phone.Any(ch => !(char.IsDigit(ch) || ch is '+' or ' ' or '-' or '(' or ')')))
        {
            errors["phone"] = ["Phone contains invalid characters."];
        }

        ValidateLength(errors, "actorName", request.ActorName, ShortTextMaxLength);
        ValidateLength(errors, "department", request.Department, ShortTextMaxLength);
        ValidateLength(errors, "municipality", request.Municipality, ShortTextMaxLength);
        ValidateLength(errors, "musicalFields", request.MusicalFields, LongTextMaxLength);
        ValidateLength(errors, "description", request.Description, LongTextMaxLength);
        ValidateLength(errors, "contribution", request.Contribution, LongTextMaxLength);
        ValidateLength(errors, "needs", request.Needs, LongTextMaxLength);

        return errors;
    }

    private static string BuildReference()
    {
        var random = RandomNumberGenerator.GetInt32(100000, 1000000);
        return $"MAP-{DateTime.UtcNow:yyyy}-{random}";
    }

    private static async Task<SubmissionCommandResult> CreateSubmissionAsync(
        ParticipationSubmissionRequest request,
        IParticipationSubmissionStore store,
        CancellationToken cancellationToken)
    {
        var normalizedRequest = NormalizeRequest(request);
        var errors = Validate(normalizedRequest);
        if (errors.Count > 0)
        {
            return SubmissionCommandResult.WithValidationErrors(errors);
        }

        var reference = string.IsNullOrWhiteSpace(normalizedRequest.Reference)
            ? BuildReference()
            : ValidationHelpers.SanitizeText(normalizedRequest.Reference, 64);

        var submittedAt = DateTimeOffset.UtcNow;

        var submission = new ParticipationSubmissionEntity
        {
            Reference = reference,
            SubmittedAt = submittedAt,
            ExternalSyncStatus = "backend_only",
            ExternalSyncMessage = "Persistido en base de datos del backend.",
            Payload = normalizedRequest
        };

        await store.SaveAsync(submission, cancellationToken);

        return SubmissionCommandResult.WithResponse(new ParticipationSubmissionResponse(
            reference,
            "accepted",
            submission.SubmittedAt,
            "La ficha fue registrada por el backend de PNMC.",
            submission.ExternalSyncStatus,
            submission.ExternalSyncMessage
        ));
    }

    private sealed class SubmissionCommandResult
    {
        public Dictionary<string, string[]> ValidationErrors { get; init; } = [];
        public ParticipationSubmissionResponse? Response { get; init; }
        public IResult? ErrorResult { get; init; }

        public static SubmissionCommandResult WithValidationErrors(Dictionary<string, string[]> errors)
            => new() { ValidationErrors = errors };

        public static SubmissionCommandResult WithResponse(ParticipationSubmissionResponse response)
            => new() { Response = response };

        public static SubmissionCommandResult WithErrorResult(IResult result)
            => new() { ErrorResult = result };
    }

    private static void ValidateLength(Dictionary<string, string[]> errors, string fieldKey, string? value, int maxLength)
    {
        if (!string.IsNullOrEmpty(value) && value.Length > maxLength)
        {
            errors[fieldKey] = [$"Maximum length is {maxLength} characters."];
        }
    }

    private static ParticipationSubmissionRequest NormalizeRequest(ParticipationSubmissionRequest request)
    {
        var roles = request.Roles ?? [];
        var festivalHabitualMonths = request.FestivalHabitualMonths ?? [];
        var marketHabitualMonths = request.MarketHabitualMonths ?? [];
        var festivalAdditionalLocations = request.FestivalAdditionalLocations ?? [];

        return new ParticipationSubmissionRequest
        {
            Reference = ValidationHelpers.SanitizeText(request.Reference, 64),
            ActorType = ValidationHelpers.SanitizeText(request.ActorType, 40).ToLowerInvariant(),
            ActorTypeLabel = ValidationHelpers.SanitizeText(request.ActorTypeLabel, 120),
            ActorName = ValidationHelpers.SanitizeText(request.ActorName, ShortTextMaxLength),
            IndividualFirstName = ValidationHelpers.SanitizeText(request.IndividualFirstName, 80),
            IndividualLastName = ValidationHelpers.SanitizeText(request.IndividualLastName, 80),
            IdentificationType = ValidationHelpers.SanitizeText(request.IdentificationType, 40),
            IdentificationNumber = ValidationHelpers.SanitizeText(request.IdentificationNumber, 60),
            HasArtisticName = request.HasArtisticName,
            ArtisticName = ValidationHelpers.SanitizeText(request.ArtisticName, 120),
            ResponsibleEntity = ValidationHelpers.SanitizeText(request.ResponsibleEntity, 180),
            ContactName = ValidationHelpers.SanitizeText(request.ContactName, 120),
            ContactRole = ValidationHelpers.SanitizeText(request.ContactRole, 120),
            Email = ValidationHelpers.SanitizeText(request.Email, 160),
            Phone = ValidationHelpers.SanitizeText(request.Phone, 60),
            Department = ValidationHelpers.SanitizeText(request.Department, ShortTextMaxLength),
            Municipality = ValidationHelpers.SanitizeText(request.Municipality, ShortTextMaxLength),
            TerritoryScope = ValidationHelpers.SanitizeText(request.TerritoryScope, 120),
            Website = ValidationHelpers.SanitizeText(request.Website, 240),
            FacebookUrl = ValidationHelpers.SanitizeText(request.FacebookUrl, 240),
            InstagramUrl = ValidationHelpers.SanitizeText(request.InstagramUrl, 240),
            Roles = roles
                .Select(role => ValidationHelpers.SanitizeText(role, 60))
                .Where(role => !string.IsNullOrWhiteSpace(role))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            MusicalFields = ValidationHelpers.SanitizeText(request.MusicalFields, LongTextMaxLength),
            Description = ValidationHelpers.SanitizeText(request.Description, LongTextMaxLength),
            Contribution = ValidationHelpers.SanitizeText(request.Contribution, LongTextMaxLength),
            Needs = ValidationHelpers.SanitizeText(request.Needs, LongTextMaxLength),
            OrganizationSubtype = ValidationHelpers.SanitizeText(request.OrganizationSubtype, 120),
            YearFounded = ValidationHelpers.SanitizeText(request.YearFounded, 10),
            LegalStatus = ValidationHelpers.SanitizeText(request.LegalStatus, 120),
            MainPrograms = ValidationHelpers.SanitizeText(request.MainPrograms, LongTextMaxLength),
            FestivalDurationDays = ValidationHelpers.SanitizeText(request.FestivalDurationDays, 30),
            FestivalSetting = ValidationHelpers.SanitizeText(request.FestivalSetting, 120),
            FestivalVenueMode = ValidationHelpers.SanitizeText(request.FestivalVenueMode, 120),
            FestivalFrequency = ValidationHelpers.SanitizeText(request.FestivalFrequency, 80),
            FestivalVersions = ValidationHelpers.SanitizeText(request.FestivalVersions, 30),
            FestivalTicketing = ValidationHelpers.SanitizeText(request.FestivalTicketing, 80),
            OpenCall = ValidationHelpers.SanitizeText(request.OpenCall, 30),
            FestivalThisYearStatus = ValidationHelpers.SanitizeText(request.FestivalThisYearStatus, 80),
            FestivalCurrentOpenCall = ValidationHelpers.SanitizeText(request.FestivalCurrentOpenCall, 10),
            FestivalOpenCallDeadline = ValidationHelpers.SanitizeText(request.FestivalOpenCallDeadline, 20),
            FestivalThisYearDate = ValidationHelpers.SanitizeText(request.FestivalThisYearDate, 20),
            FestivalThisYearStartDate = ValidationHelpers.SanitizeText(request.FestivalThisYearStartDate, 20),
            FestivalThisYearEndDate = ValidationHelpers.SanitizeText(request.FestivalThisYearEndDate, 20),
            MarketFrequency = ValidationHelpers.SanitizeText(request.MarketFrequency, 80),
            MarketEditionsCount = ValidationHelpers.SanitizeText(request.MarketEditionsCount, 20),
            AverageBuyers = ValidationHelpers.SanitizeText(request.AverageBuyers, 20),
            LinkedFestival = ValidationHelpers.SanitizeText(request.LinkedFestival, 10),
            LinkedFestivalName = ValidationHelpers.SanitizeText(request.LinkedFestivalName, 180),
            MarketThisYearMonth = ValidationHelpers.SanitizeText(request.MarketThisYearMonth, 30),
            MarketThisYearStatus = ValidationHelpers.SanitizeText(request.MarketThisYearStatus, 80),
            MarketThisYearDate = ValidationHelpers.SanitizeText(request.MarketThisYearDate, 20),
            IndividualProfile = ValidationHelpers.SanitizeText(request.IndividualProfile, 120),
            TrajectoryYears = ValidationHelpers.SanitizeText(request.TrajectoryYears, 40),
            LinkedProcesses = ValidationHelpers.SanitizeText(request.LinkedProcesses, LongTextMaxLength),
            Members = ValidationHelpers.SanitizeText(request.Members, 20),
            MusicalPractice = ValidationHelpers.SanitizeText(request.MusicalPractice, LongTextMaxLength),
            CirculationScope = ValidationHelpers.SanitizeText(request.CirculationScope, 120),
            CollectiveTrajectory = ValidationHelpers.SanitizeText(request.CollectiveTrajectory, LongTextMaxLength),
            SpaceType = ValidationHelpers.SanitizeText(request.SpaceType, 120),
            SpaceCapacity = ValidationHelpers.SanitizeText(request.SpaceCapacity, 40),
            SpaceUses = ValidationHelpers.SanitizeText(request.SpaceUses, LongTextMaxLength),
            TechnicalEquipment = ValidationHelpers.SanitizeText(request.TechnicalEquipment, LongTextMaxLength),
            Consent = request.Consent,
            FestivalHabitualMonths = festivalHabitualMonths
                .Select(month => ValidationHelpers.SanitizeText(month, 30))
                .Where(month => !string.IsNullOrWhiteSpace(month))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            MarketHabitualMonths = marketHabitualMonths
                .Select(month => ValidationHelpers.SanitizeText(month, 30))
                .Where(month => !string.IsNullOrWhiteSpace(month))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            FestivalAdditionalLocations = festivalAdditionalLocations
                .Select(location => new ParticipationFestivalLocation
                {
                    Department = ValidationHelpers.SanitizeText(location.Department, ShortTextMaxLength),
                    Municipality = ValidationHelpers.SanitizeText(location.Municipality, ShortTextMaxLength)
                })
                .ToList()
        };
    }
}
