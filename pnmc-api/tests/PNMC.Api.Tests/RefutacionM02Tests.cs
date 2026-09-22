using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PNMC.Contracts;
using PNMC.Infrastructure.Data;
using Xunit;
using Xunit.Abstractions;

namespace PNMC.Api.Tests;

public sealed class RefutacionM02Tests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly ITestOutputHelper _out;

    public RefutacionM02Tests(TestWebApplicationFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _out = output;
    }

    private async Task LoginAsWebmasterAsync()
    {
        var r = await _client.PostAsJsonAsync("/api/v1/admin/auth/login", new AdminLoginRequest
        {
            Email = "test@pnmc.local",
            Password = "pnmc-master"
        });
        r.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Registra, verifica y abre sesión externa. Es el canal por el que hoy entra una solicitud
    /// de vinculación de registros.
    /// </summary>
    private async Task RegistrarVerificarYEntrarComoExternoAsync(string correo)
    {
        var alta = await _client.PostAsJsonAsync("/api/v1/externo/auth/register", new ExternalRegisterRequest
        {
            OrganizationName = "Organizacion de prueba",
            HeadquartersDepartmentCode = "05",
            HeadquartersMunicipalityCode = "05001",
            FullName = "Persona Refutacion M02",
            FirstName = "Persona",
            FirstSurname = "Refutacion",
            DocumentType = "CC",
            DocumentNumber = "1020304051",
            NumeroDocumento = "1020304051",
            Phone = "3000000000",
            Email = correo,
            Password = "ClaveExterna123",
            PoliticasAceptadas = ["tratamiento", "terminos"],
        });
        alta.EnsureSuccessStatusCode();
        var registrada = await alta.Content.ReadFromJsonAsync<ExternalRegisterResponse>();


        var acceso = await _client.PostAsJsonAsync("/api/v1/externo/auth/login", new ExternalLoginRequest
        {
            Email = correo,
            Password = "ClaveExterna123"
        });
        acceso.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Refutacion_M02_Ataque_A_Los_Cinco_ALTA()
    {
        await LoginAsWebmasterAsync();

        // ============ H-005: forma real de la respuesta de listado ============
        var raw = await _client.GetStringAsync("/api/v1/admin/duplicates?limit=5");
        _out.WriteLine("[H-005] RAW GET /admin/duplicates = " + raw);
        _out.WriteLine("[H-005] empieza por corchete? " + raw.TrimStart().StartsWith('['));
        _out.WriteLine("[H-005] contiene la clave items? " + raw.Contains("\"items\""));

        // ============ H-006: EvidenceJson sin saneamiento ni cota ============
        var noJson = await _client.PostAsJsonAsync("/api/v1/admin/duplicates", new RecordDuplicateCandidateCreateRequest
        {
            ModuleId = "festivals",
            SourceRecordId = "1",
            CandidateRecordId = "2",
            SimilarityLevel = "alta",
            SimilarityScore = 92m,
            EvidenceJson = "no soy json <script>alert(1)</script>"
        });
        _out.WriteLine("[H-006 sintaxis] evidenceJson no-JSON -> " + (int)noJson.StatusCode);
        var noJsonDto = await noJson.Content.ReadFromJsonAsync<RecordDuplicateCandidateDto>();
        _out.WriteLine("[H-006 sintaxis] devuelto verbatim: [" + noJsonDto!.EvidenceJson + "]");

        var big = new string('A', 5000000);
        var huge = await _client.PostAsJsonAsync("/api/v1/admin/duplicates", new RecordDuplicateCandidateCreateRequest
        {
            ModuleId = "festivals",
            SourceRecordId = "3",
            CandidateRecordId = "4",
            SimilarityLevel = "media",
            SimilarityScore = 50m,
            EvidenceJson = big
        });
        _out.WriteLine("[H-006 longitud] evidenceJson de 5000000 chars -> " + (int)huge.StatusCode);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
            var maxLen = await db.RecordDuplicateCandidates.AsNoTracking().MaxAsync(x => x.EvidenceJson.Length);
            _out.WriteLine("[H-006 longitud] longitud maxima PERSISTIDA = " + maxLen);
        }

        // Control: los otros campos si se truncan
        var ctrl = await _client.PostAsJsonAsync("/api/v1/admin/data-quality/flags", new RecordQualityFlagCreateRequest
        {
            ModuleId = "festivals",
            RecordId = "1",
            FlagType = "prueba",
            Severity = "alta",
            Detail = new string('B', 5000)
        });
        var ctrlDto = await ctrl.Content.ReadFromJsonAsync<RecordQualityFlagDto>();
        _out.WriteLine("[H-006 control] Detail enviado 5000 chars -> persistido " + ctrlDto!.Detail.Length + " chars, HTTP " + (int)ctrl.StatusCode);

        // ============ H-001: fusionar no fusiona ============
        var dupCreate = await _client.PostAsJsonAsync("/api/v1/admin/duplicates", new RecordDuplicateCandidateCreateRequest
        {
            ModuleId = "festivals",
            SourceRecordId = "1",
            CandidateRecordId = "2",
            SimilarityLevel = "alta",
            SimilarityScore = 92m,
            EvidenceJson = "{}"
        });
        var dup = await dupCreate.Content.ReadFromJsonAsync<RecordDuplicateCandidateDto>();

        string antes;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
            var f1 = await db.FestivalRecords.AsNoTracking().FirstOrDefaultAsync(f => f.Id == 1);
            var f2 = await db.FestivalRecords.AsNoTracking().FirstOrDefaultAsync(f => f.Id == 2);
            antes = "count=" + await db.FestivalRecords.CountAsync()
                + " f1=" + (f1 is null ? "NULL" : f1.Name + "/" + f1.StatusCode)
                + " f2=" + (f2 is null ? "NULL" : f2.Name + "/" + f2.StatusCode);
            _out.WriteLine("[H-001] festivales ANTES : " + antes);
        }

        var fus = await _client.PostAsJsonAsync("/api/v1/admin/duplicates/" + dup!.Id + "/decision",
            new RecordDuplicateDecisionRequest { Decision = "fusionar", Comment = "refutacion" });
        _out.WriteLine("[H-001] POST decision=fusionar -> " + (int)fus.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
            var f1 = await db.FestivalRecords.AsNoTracking().FirstOrDefaultAsync(f => f.Id == 1);
            var f2 = await db.FestivalRecords.AsNoTracking().FirstOrDefaultAsync(f => f.Id == 2);
            var despues = "count=" + await db.FestivalRecords.CountAsync()
                + " f1=" + (f1 is null ? "NULL" : f1.Name + "/" + f1.StatusCode)
                + " f2=" + (f2 is null ? "NULL" : f2.Name + "/" + f2.StatusCode);
            _out.WriteLine("[H-001] festivales DESPUES: " + despues);
            _out.WriteLine("[H-001] IDENTICOS? " + (antes == despues));
            var row = await db.RecordDuplicateCandidates.AsNoTracking().FirstAsync(x => x.Id == long.Parse(dup.Id));
            _out.WriteLine("[H-001] fila: Decision=" + row.Decision + " Estado=" + row.Status);
        }

        var pend = await _client.GetStringAsync("/api/v1/admin/duplicates?status=pendiente&limit=200");
        _out.WriteLine("[H-001] el caso sigue en la cola pendiente? " + pend.Contains("\"id\":\"" + dup.Id + "\""));

        // ============ H-002: aprobar no concede alcance ============
        // La solicitud la crea el canal EXTERNO desde: quien pide
        // vincularse a un registro es alguien del ecosistema, no la consola. Antes esta llamada
        // iba con la sesión de webmaster de arriba y funcionaba solo porque la ruta estaba
        // cerrada contra el esquema institucional.
        await RegistrarVerificarYEntrarComoExternoAsync("refutacion.m02@example.com");

        var linkCreate = await _client.PostAsJsonAsync("/api/v1/solicitudes-de-vinculacion", new RecordLinkRequestCreateRequest
        {
            ModuleId = "festivals",
            RecordId = "1",
            RequestedScope = "responsable",
            Reason = "Refutacion M02 sobre concesion de alcance.",
            EvidenceText = "prueba"
        });
        var link = await linkCreate.Content.ReadFromJsonAsync<RecordLinkRequestDto>();
        _out.WriteLine("[H-002] Location del 201 = " + (linkCreate.Headers.Location?.ToString() ?? "(sin cabecera)"));

        // El contador era de UsuariosEntidadesAliadas, tabla retirada.
        // La pregunta de fondo no cambia —¿aprobar una solicitud CONCEDE algo, o solo cambia una
        // etiqueta?— y su tabla es ahora UsuariosEntidades, la relación persona-entidad del
        // modelo definitivo.
        int vinculosAntes;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
            vinculosAntes = await db.UserEntities.CountAsync();
        }
        var apr = await _client.PostAsJsonAsync("/api/v1/admin/solicitudes-de-vinculacion/" + link!.Id + "/status",
            new RecordLinkRequestStatusRequest { Status = "aprobada", Comment = "concedido" });
        _out.WriteLine("[H-002] POST status=aprobada -> " + (int)apr.StatusCode);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
            var vinculosDespues = await db.UserEntities.CountAsync();
            var row = await db.RecordLinkRequests.AsNoTracking().FirstAsync(x => x.Id == long.Parse(link.Id));
            _out.WriteLine("[H-002] UsuariosEntidades antes=" + vinculosAntes + " despues=" + vinculosDespues + " delta=" + (vinculosDespues - vinculosAntes));
            _out.WriteLine("[H-002] fila: Estado=" + row.Status + " Revisor=" + row.ReviewerUserId);

            // EL UNICO ASERTO DE ESTE FICHERO, y es deliberado que fije un hueco en vez de una
            // garantia. Este fichero nacio como instrumento —cero asertos en 300 lineas—, de modo
            // que podia pasar en verde diga lo que diga el sistema; de hecho paso en verde durante
            // meses mientras H-002 era cierto. Lo que H-002 midio es que aprobar una solicitud de
            // vinculacion NO concede nada: cambia el estado de la fila y no crea el vinculo
            // persona-entidad que la solicitud pide. El circuito es, hasta aqui, decorativo.
            //
            // Se deja escrito como aserto para que el dia que alguien implemente la concesion,
            // esta prueba se ponga en rojo y le obligue a venir aqui a decirlo. Cambiar el numero
            // sin implementar nada seria volver al instrumento mudo.
            Assert.Equal(vinculosAntes, vinculosDespues);
            Assert.Equal("aprobada", row.Status);
        }

        // H-011 colateral: transicion ilegal cancelada -> aprobada
        var canc = await _client.PostAsJsonAsync("/api/v1/admin/solicitudes-de-vinculacion/" + link.Id + "/status",
            new RecordLinkRequestStatusRequest { Status = "cancelada", Comment = "retirada" });
        _out.WriteLine("[H-011] aprobada -> cancelada = " + (int)canc.StatusCode);
        var reapr = await _client.PostAsJsonAsync("/api/v1/admin/solicitudes-de-vinculacion/" + link.Id + "/status",
            new RecordLinkRequestStatusRequest { Status = "aprobada", Comment = "revivida" });
        _out.WriteLine("[H-011] cancelada -> aprobada = " + (int)reapr.StatusCode);

        // ============ H-010 colateral: Clean() destruye la caja ============
        var caja = await _client.PostAsJsonAsync("/api/v1/admin/data-quality/flags", new RecordQualityFlagCreateRequest
        {
            ModuleId = "Participaciones",
            RecordId = "MAP-2026-483712",
            FlagType = "prueba",
            Severity = "alta",
            Detail = "caja"
        });
        var cajaDto = await caja.Content.ReadFromJsonAsync<RecordQualityFlagDto>();
        _out.WriteLine("[H-010] enviado MAP-2026-483712 -> devuelto " + cajaDto!.RecordId);

        // ============ H-014 colateral: auto-duplicado ============
        var self = await _client.PostAsJsonAsync("/api/v1/admin/duplicates", new RecordDuplicateCandidateCreateRequest
        {
            ModuleId = "festivals",
            SourceRecordId = "12",
            CandidateRecordId = "12",
            SimilarityLevel = "alta",
            SimilarityScore = 100m,
            EvidenceJson = "{}"
        });
        _out.WriteLine("[H-014] auto-duplicado 12 vs 12 -> " + (int)self.StatusCode);

        // ============ H-007 colateral: score fuera de rango ============
        var score = await _client.PostAsJsonAsync("/api/v1/admin/duplicates", new RecordDuplicateCandidateCreateRequest
        {
            ModuleId = "festivals",
            SourceRecordId = "5",
            CandidateRecordId = "6",
            SimilarityLevel = "alta",
            SimilarityScore = 1000m,
            EvidenceJson = "{}"
        });
        _out.WriteLine("[H-007] similarityScore=1000 sobre SQLite -> " + (int)score.StatusCode);

        // ============ H-003 colateral: Location apunta a 404 ============
        if (linkCreate.Headers.Location is not null)
        {
            var follow = await _client.GetAsync(linkCreate.Headers.Location);
            _out.WriteLine("[H-003] GET sobre la Location devuelta -> " + (int)follow.StatusCode);
        }
    }
}
