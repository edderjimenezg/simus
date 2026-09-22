using System.Globalization;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using PNMC.Contracts;
using PNMC.Infrastructure.Data;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// PNMC-054 - El contenido de una propuesta que NO acabo Publicada no puede llegar nunca a la
/// lectura publica anonima. El circuito de revision solo vale algo si rechazar produce un cambio
/// observable en lo que ve el ciudadano.
/// </summary>
public sealed class Pnmc054LecturaPublicaTests : IClassFixture<TestWebApplicationFactory>
{
    private const string CorreoAprobado = "contacto.institucional@festival.test";
    private const string CorreoRechazado = "secuestrado@atacante.test";
    // Las dos periodicidades ya estaban sembradas; ahora ademas se nombran, porque son el
    // centinela que distingue el contenido aprobado del rechazado en la lectura publica.
    private const string PeriodicidadAprobada = "anual";
    private const string PeriodicidadRechazada = "bienal";
    private const string DepartamentoAprobado = "Antioquia";
    // El nombre canonico DIVIPOLA del codigo 11 lleva coma (DatabaseBootstrapper.cs).
    // La prueba siembra el CODIGO y la lectura publica devuelve el NOMBRE resuelto.
    private const string DepartamentoRechazado = "Bogota, D.C.";

    private readonly TestWebApplicationFactory _factory;

    public Pnmc054LecturaPublicaTests(TestWebApplicationFactory factory) => _factory = factory;

    /// <summary>
    /// Reproduce el hallazgo por el camino real: se publica la propuesta por HTTP (eso crea la
    /// version nueva y la marca EsVigente) y despues se deja la propuesta en "Rechazada".
    /// Ese ultimo paso se escribe sobre la fila y no por HTTP porque el endpoint de decisiones
    /// devuelve 409 ante cualquier segunda decision (RevisionInstitucionalPropuestasFestivalEndpoints.cs):
    /// el unico camino que produce este par de filas en la aplicacion es la carrera entre dos
    /// revisores simultaneos, que no es reproducible de forma determinista en una prueba.
    /// El estado resultante es exactamente el que A06 midio extremo a extremo:
    /// estadoPropuesta=Rechazada, versionNuevaId apuntando a una version EsVigente, versionesVigentes=1.
    /// </summary>
    [Fact]
    public async Task Una_Propuesta_Rechazada_No_Deja_Su_Contenido_En_La_Lectura_Publica()
    {
        var (festivalId, propuestaId) = SembrarFestivalConPropuestaEnRevision();

        var institucional = await CrearClienteInstitucionalAsync();
        var decision = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/institucional/propuestas-cambio-festival/{propuestaId}/decisiones")
        {
            Content = JsonContent.Create(new { accion = "Publicar" })
        };
        decision.Headers.Add("X-CSRF-TOKEN", await ObtenerCsrfPropuestasAsync(institucional));
        (await institucional.SendAsync(decision)).EnsureSuccessStatusCode();

        // Linea base: publicar SI cambia la lectura publica. Sin esta comprobacion, la asercion
        // final podria pasar por accidente aunque el circuito entero estuviese muerto.
        var anonimo = _factory.CreateClient();
        var publicada = await anonimo.GetFromJsonAsync<FestivalPublicoDto>($"/api/v1/publico/festivales/{festivalId}");
        Assert.NotNull(publicada);
        Assert.Equal(DepartamentoRechazado, publicada!.TerritorioPrincipal.Departamento);
        // EL CENTINELA ERA EL CORREO Y AHORA ES LA PERIODICIDAD. El 30 de agosto de 2026 la
        // lectura publica dejo de servir correo, telefono y director; el contenido de la
        // propuesta se sigue distinguiendo con dos campos que si viajan y que las dos
        // versiones ya sembraban distintos: el departamento y la periodicidad.
        Assert.Equal(PeriodicidadRechazada, publicada.Periodicidad);

        int versionNuevaId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
            var propuesta = db.PropuestasDeCambio.Single(item => item.Id == propuestaId);
            Assert.Equal(EstadosDePropuesta.Aplicada, propuesta.Estado);
            Assert.NotNull(propuesta.SubregistroResultanteId);
            versionNuevaId = int.Parse(propuesta.SubregistroResultanteId!, CultureInfo.InvariantCulture);
            // SE REPUDIA EL EXPEDIENTE A MANO para comprobar el guardián: una versión que nació de
            // una propuesta que no acabó aplicada no puede seguir sirviéndose al público.
            propuesta.Estado = EstadosDePropuesta.Rechazada;
            propuesta.MotivoDeLaDecision = "Repudiada para la prueba.";
            db.SaveChanges();
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
            Assert.True(db.VersionesFestival.Single(item => item.Id == versionNuevaId).EsVigente);
            Assert.Equal(1, db.VersionesFestival.Count(item => item.FestivalOrigenId == festivalId && item.EsVigente));
        }

        // El nucleo del punto: la fila dice "Rechazada" y el publico anonimo ya no ve su contenido.
        var trasRechazo = await anonimo.GetFromJsonAsync<FestivalPublicoDto>($"/api/v1/publico/festivales/{festivalId}");
        Assert.NotNull(trasRechazo);
        Assert.Equal(DepartamentoAprobado, trasRechazo!.TerritorioPrincipal.Departamento);
        Assert.Equal(PeriodicidadAprobada, trasRechazo.Periodicidad);
        Assert.Equal("Festival PNMC-054", trasRechazo.Nombre);

        var listado = await anonimo.GetFromJsonAsync<PagedResponse<FestivalPublicoDto>>("/api/v1/publico/festivales?limit=500");
        Assert.NotNull(listado);
        var enListado = Assert.Single(listado!.Items, item => item.Id == festivalId.ToString());
        Assert.Equal(DepartamentoAprobado, enListado.TerritorioPrincipal.Departamento);
        Assert.Equal(PeriodicidadAprobada, enListado.Periodicidad);
        Assert.DoesNotContain(listado.Items, item => item.Periodicidad == PeriodicidadRechazada);
    }

    /// <summary>
    /// La misma invariante afirmada sobre el estado de la base, sin pasar por el circuito: una
    /// version marcada EsVigente cuya propuesta de origen no esta Publicada no es publica, y la
    /// lectura retrocede a la ultima version que si lo estuvo.
    /// </summary>
    [Fact]
    public async Task La_Lectura_Publica_Retrocede_A_La_Ultima_Version_Aprobada()
    {
        int festivalId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
            var organizacion = CrearOrganizacion(db);
            var festival = new FestivalRow
            {
                Name = "Festival PNMC-054 estado de base",
                CoverageLevel = "municipal",
                DepartmentCode = "05",
                MunicipalityCode = "05001",
                ContactEmail = CorreoAprobado,
                StatusCode = "Publicado",
                OrganizacionPrincipalId = organizacion.Id,
                CreatedAt = DateTime.UtcNow
            };
            db.FestivalRecords.Add(festival);
            db.SaveChanges();
            festivalId = festival.Id;

            var aprobada = new VersionFestivalRow
            {
                FestivalOrigenId = festival.Id,
                NumeroVersion = 1,
                EsVigente = false,
                Nombre = "Version aprobada por el revisor",
                NivelCobertura = "municipal",
                CodigoDepartamento = "05",
                CodigoMunicipio = "05001",
                Periodicidad = "anual",
                CorreoContacto = CorreoAprobado,
                FechaPublicacion = DateTime.UtcNow.AddDays(-1),
                FechaCreacion = DateTime.UtcNow.AddDays(-1)
            };
            var repudiada = new VersionFestivalRow
            {
                FestivalOrigenId = festival.Id,
                NumeroVersion = 2,
                EsVigente = true,
                Nombre = "Version de propuesta rechazada",
                NivelCobertura = "municipal",
                CodigoDepartamento = "11",
                CodigoMunicipio = "11001",
                Periodicidad = "bienal",
                CorreoContacto = CorreoRechazado,
                FechaPublicacion = DateTime.UtcNow,
                FechaCreacion = DateTime.UtcNow
            };
            db.VersionesFestival.AddRange(aprobada, repudiada);
            db.SaveChanges();

            // EL EXPEDIENTE QUE REPUDIA A LA VERSION. `SubregistroResultanteId` es la versión que
            // nació de aplicarlo, y su estado dice que NO acabó aplicado: eso es lo que la lectura
            // pública comprueba para no servirla.
            db.PropuestasDeCambio.Add(new PropuestaDeCambioRow
            {
                ModuloId = Modulos.Festivales,
                RegistroId = festival.Id.ToString(CultureInfo.InvariantCulture),
                SubregistroId = aprobada.Id.ToString(CultureInfo.InvariantCulture),
                SubregistroResultanteId = repudiada.Id.ToString(CultureInfo.InvariantCulture),
                IdOrganizacion = organizacion.Id,
                IdUsuarioProponente = 1,
                Estado = EstadosDePropuesta.Rechazada,
                MotivoDeLaDecision = "No corresponde.",
                FechaCreacion = DateTime.UtcNow.AddDays(-1),
                FechaEnvio = DateTime.UtcNow.AddHours(-2),
                FechaDecision = DateTime.UtcNow,
                FechaActualizacion = DateTime.UtcNow
            });
            db.SaveChanges();
        }

        var anonimo = _factory.CreateClient();
        var ficha = await anonimo.GetFromJsonAsync<FestivalPublicoDto>($"/api/v1/publico/festivales/{festivalId}");
        Assert.NotNull(ficha);
        Assert.Equal("Version aprobada por el revisor", ficha!.Nombre);
        Assert.Equal(DepartamentoAprobado, ficha.TerritorioPrincipal.Departamento);
        Assert.Equal(PeriodicidadAprobada, ficha.Periodicidad);
    }

    private (int FestivalId, int PropuestaId) SembrarFestivalConPropuestaEnRevision()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var organizacion = CrearOrganizacion(db);
        var festival = new FestivalRow
        {
            Name = "Festival PNMC-054",
            CoverageLevel = "municipal",
            DepartmentCode = "05",
            MunicipalityCode = "05001",
            ContactEmail = CorreoAprobado,
            StatusCode = "Publicado",
            OrganizacionPrincipalId = organizacion.Id,
            CreatedAt = DateTime.UtcNow
        };
        db.FestivalRecords.Add(festival);
        db.SaveChanges();

        var version = new VersionFestivalRow
        {
            FestivalOrigenId = festival.Id,
            NumeroVersion = 1,
            EsVigente = true,
            Nombre = "Festival PNMC-054",
            NivelCobertura = "municipal",
            CodigoDepartamento = "05",
            CodigoMunicipio = "05001",
            Periodicidad = "anual",
            CorreoContacto = CorreoAprobado,
            FechaPublicacion = DateTime.UtcNow.AddDays(-1),
            FechaCreacion = DateTime.UtcNow.AddDays(-1)
        };
        db.VersionesFestival.Add(version);
        db.SaveChanges();

        // EL EXPEDIENTE, EN LA FORMA GENERICA: cabecera más SOLO LOS CAMPOS QUE CAMBIAN. Es lo
        // que hace que la propuesta se distinga de la versión: aquí, el departamento y la
        // periodicidad, que son los dos centinelas que la lectura pública sí sirve.
        var propuesta = new PropuestaDeCambioRow
        {
            ModuloId = Modulos.Festivales,
            RegistroId = festival.Id.ToString(CultureInfo.InvariantCulture),
            SubregistroId = version.Id.ToString(CultureInfo.InvariantCulture),
            Estado = EstadosDePropuesta.EnRevision,
            IdOrganizacion = organizacion.Id,
            IdUsuarioProponente = 1,
            FechaCreacion = DateTime.UtcNow.AddHours(-3),
            FechaEnvio = DateTime.UtcNow.AddHours(-2),
            FechaActualizacion = DateTime.UtcNow.AddHours(-2),
        };
        db.PropuestasDeCambio.Add(propuesta);
        db.SaveChanges();
        db.PropuestasDeCambioCampos.AddRange(
            new PropuestaDeCambioCampoRow { IdPropuesta = propuesta.Id, SeccionId = "generales", CampoId = "descripcion", CampoEtiqueta = "Descripción", ValorAnterior = version.Descripcion, ValorPropuesto = "Contenido propuesto y despues rechazado", FechaCreacion = DateTime.UtcNow },
            new PropuestaDeCambioCampoRow { IdPropuesta = propuesta.Id, SeccionId = "territorio", CampoId = "codigoDepartamento", CampoEtiqueta = "Departamento", ValorAnterior = version.CodigoDepartamento, ValorPropuesto = "11", FechaCreacion = DateTime.UtcNow },
            new PropuestaDeCambioCampoRow { IdPropuesta = propuesta.Id, SeccionId = "territorio", CampoId = "codigoMunicipio", CampoEtiqueta = "Municipio", ValorAnterior = version.CodigoMunicipio, ValorPropuesto = "11001", FechaCreacion = DateTime.UtcNow },
            new PropuestaDeCambioCampoRow { IdPropuesta = propuesta.Id, SeccionId = "generales", CampoId = "periodicidad", CampoEtiqueta = "Periodicidad", ValorAnterior = version.Periodicidad, ValorPropuesto = "bienal", FechaCreacion = DateTime.UtcNow },
            new PropuestaDeCambioCampoRow { IdPropuesta = propuesta.Id, SeccionId = "contacto", CampoId = "correoContacto", CampoEtiqueta = "Correo de contacto", ValorAnterior = version.CorreoContacto, ValorPropuesto = CorreoRechazado, FechaCreacion = DateTime.UtcNow });
        db.SaveChanges();
        return (festival.Id, (int)propuesta.Id);
    }

    private static EntityProfileRow CrearOrganizacion(PnmcDbContext db)
    {
        var organizacion = new EntityProfileRow
        {
            EntityType = "organizacion",
            Name = "Organizacion proponente PNMC-054",
            CoverageLevel = "municipal",
            DepartmentCode = "05",
            MunicipalityCode = "05001",
            StatusCode = "activa",
            IsActive = true,
            CreatedByUserId = 1,
            CreatedAt = DateTime.UtcNow
        };
        db.EntityProfiles.Add(organizacion);
        db.SaveChanges();
        return organizacion;
    }

    private async Task<HttpClient> CrearClienteInstitucionalAsync()
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/v1/admin/auth/login", new AdminLoginRequest
        {
            Email = "test@pnmc.local",
            Password = "pnmc-master"
        });
        login.EnsureSuccessStatusCode();
        return client;
    }

    private static async Task<string> ObtenerCsrfPropuestasAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/institucional/propuestas-cambio-festival/csrf");
        response.EnsureSuccessStatusCode();
        var token = await response.Content.ReadFromJsonAsync<TokenAntiforgeryRespuesta>();
        Assert.NotNull(token);
        Assert.False(string.IsNullOrWhiteSpace(token!.RequestToken));
        return token.RequestToken;
    }
}
