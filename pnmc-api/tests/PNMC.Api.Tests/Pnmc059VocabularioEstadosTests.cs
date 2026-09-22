using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using PNMC.Api.Endpoints;
using PNMC.Contracts;
using PNMC.Infrastructure.Data;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// PNMC-059 — Todo estado y todo verbo que el circuito Festival escribe tiene que ser uno
/// que la base real admita.
/// </summary>
/// <remarks>
/// <para>
/// LA REGLA. El circuito escribia los estados en PascalCase y la base tiene una clave
/// foranea contra <c>EstadosContenido</c>, cuyos siete codigos son minuscula con guion
/// bajo. Medido con un <c>UPDATE</c> por cada nombre contra <c>PNMC_LOCAL</c>:
/// </para>
/// <code>
///           Borrador  ->  ACEPTADO
///         EnRevision  ->  RECHAZADO POR LA FK
/// AjustesSolicitados  ->  RECHAZADO POR LA FK
///          Rechazado  ->  ACEPTADO
///          Publicado  ->  ACEPTADO
/// </code>
/// <para>
/// Los dos imposibles son exactamente los dos que meten un Festival en la bandeja del
/// funcionario. La cola de revision institucional no estaba vacia por falta de uso:
/// <b>nada podia entrar en ella</b>. Y los tres que pasan lo hacen por accidente —la base
/// esta en collation insensible a mayusculas—, no por diseño.
/// </para>
/// <para>
/// Y no era solo el estado. <c>CK_BitacoraAuditoria_Accion</c> cierra la columna a trece
/// verbos tecnicos, y el circuito escribia nombres de evento (<c>FestivalCreado</c>,
/// <c>FestivalEnviadoARevision</c>...), ninguno de los cuales esta en la lista: toda
/// escritura externa de Festival fallaba, no solo el envio a revision.
/// </para>
/// <para>
/// POR QUE ESTA PRUEBA NO MIRA EL MOTOR. La tabla <c>Festivales</c> de la suite la crea EF
/// desde el modelo, y el mapeo de <c>FestivalRow</c> no declara relacion con
/// <c>EstadosContenido</c>: en SQLite no hay ni clave foranea ni <c>CHECK</c> que violar, de
/// modo que una prueba que solo ejecute el endpoint pasa igual con el defecto dentro. Eso es
/// lo que dejo verde durante meses a
/// <c>External_Administrator_Can_Send_Valid_Festival_Draft_To_Review_Without_Publication</c>,
/// que llego a AFIRMAR los dos valores prohibidos.
/// </para>
/// <para>
/// Asi que aqui se comprueba contra el <b>vocabulario declarado</b>, no contra el motor: se
/// recorre el circuito por HTTP y se exige que cada valor persistido pertenezca al conjunto
/// que la base admite. La prueba falla igual en SQLite y en SQL Server, que es la unica forma
/// de que proteja algo. La comprobacion contra el motor de verdad vive aparte, en
/// <c>ArnesSqlServer</c>.
/// </para>
/// </remarks>
public sealed class Pnmc059VocabularioEstadosTests : IClassFixture<TestWebApplicationFactory>
{
    /// <summary>Los siete codigos sembrados en <c>EstadosContenido</c>
    /// (<c>V20260519_03__administracion_control_seed.sql</c>), transcritos aqui a proposito:
    /// si alguien cambia el seed sin mirar el codigo, esta lista deja de coincidir y la
    /// prueba lo dice.</summary>
    private static readonly string[] CodigosDeLaBase =
        ["borrador", "en_revision", "ajustes_solicitados", "aprobado", "publicado", "archivado", "rechazado", "registrada"];

    /// <summary>Los trece verbos de <c>CK_BitacoraAuditoria_Accion</c>
    /// (<c>V20260519_02__administracion_control.sql:167</c>), igualmente transcritos.</summary>
    private static readonly string[] VerbosDeLaBase =
    [
        "crear", "actualizar", "eliminar", "publicar", "archivar",
        "aprobar", "rechazar", "iniciar_sesion", "cerrar_sesion",
        "iniciar_sesion_externa", "cerrar_sesion_externa",
        "crear_organizacion", "asignar_administrador_inicial",
    ];

    private readonly TestWebApplicationFactory _factory;

    public Pnmc059VocabularioEstadosTests(TestWebApplicationFactory factory) => _factory = factory;

    /// <summary>
    /// Las constantes del codigo y los codigos de la base tienen que ser el mismo conjunto.
    /// </summary>
    /// <remarks>
    /// Es la comprobacion mas barata y la que mas lejos llega: cierra el defecto para
    /// cualquier estado, no solo para los dos que resultaron imposibles. Si mañana alguien
    /// añade <c>en_revision_institucional</c> al seed sin declararlo, o declara una constante
    /// que la base no tiene, se entera aqui y no en produccion.
    /// </remarks>
    [Fact]
    public void El_Vocabulario_Declarado_Coincide_Con_El_De_La_Base()
    {
        var declarados = EstadosFestival.DelCatalogo;

        Assert.Equal(CodigosDeLaBase.OrderBy(item => item, StringComparer.Ordinal), declarados.OrderBy(item => item, StringComparer.Ordinal));
        Assert.Equal(VerbosDeLaBase.OrderBy(item => item, StringComparer.Ordinal), AccionesAuditoria.Admitidos.OrderBy(item => item, StringComparer.Ordinal));

        // CK_EstadosContenido_CodigoEstado_Formato exige LOWER(CodigoEstado) y prohibe espacios.
        // Es la restriccion que hace IMPOSIBLE añadir "EnRevision" al catalogo: no es una
        // cuestion de gusto, la base no lo admite ni aunque se quisiera.
        foreach (var codigo in declarados)
        {
            Assert.Equal(codigo.ToLowerInvariant(), codigo);
            Assert.DoesNotContain(' ', codigo);
        }

        // Los cinco del circuito son un subconjunto de los siete. "aprobado" queda fuera a
        // proposito: no hay aprobacion intermedia y "archivado" tampoco pertenece al recorrido.
        Assert.All(EstadosFestival.DelCircuito, estado => Assert.Contains(estado, CodigosDeLaBase));
        Assert.DoesNotContain(EstadosFestival.Aprobado, EstadosFestival.DelCircuito);
        // "registrada" es estado de Entidad, no de contenido: pertenece al catalogo pero NO al
        // recorrido de un Festival. Si algun dia aparece aqui, alguien confundio los dos ciclos.
        Assert.DoesNotContain(EstadosFestival.Registrada, EstadosFestival.DelCircuito);
    }

    /// <summary>
    /// Los tipos de entidad declarados coinciden con la lista de <c>CK_Entidades_Tipo</c>.
    /// </summary>
    /// <remarks>
    /// Misma comprobacion barata que la de los estados, para la otra lista cerrada que el
    /// codigo escribe a ciegas. La restriccion real vive solo en SQL Server —el modelo de EF
    /// no la declara—, asi que aqui se compara contra la lista transcrita del guion de
    /// esquema y la comprobacion contra el motor queda en <c>Pnmc060AgrupacionesSqlServerTests</c>.
    /// </remarks>
    [Fact]
    public void Los_Tipos_De_Entidad_Declarados_Coinciden_Con_El_Esquema()
    {
        // Transcritos de V20260912_04__ciclo_de_vida_de_la_organizacion.sql, que dejo
        // CK_Entidades_Tipo en un solo valor: «no va a existir agrupacion».
        string[] deLaBase = ["organizacion"];

        Assert.Equal(
            deLaBase.OrderBy(item => item, StringComparer.Ordinal),
            TiposDeEntidad.Admitidos.OrderBy(item => item, StringComparer.Ordinal));

        // LOS DOS QUE QUEDAN SON ACTORES. Un tipo que nombra un proceso —festival, mercado,
        // escuela— o un lugar —espacio— en la tabla de actores es la puerta por la que vuelve el
        // modelo generico de septiembre, donde todo cabia en dbo.Entidades. En esta base cada
        // proceso tiene su tabla y su circuito.
        Assert.DoesNotContain("festival", TiposDeEntidad.Admitidos);
        Assert.DoesNotContain("escuela_musica", TiposDeEntidad.Admitidos);
        Assert.DoesNotContain("mercado_musical", TiposDeEntidad.Admitidos);
        Assert.DoesNotContain("espacio", TiposDeEntidad.Admitidos);
        Assert.DoesNotContain("agrupacion", TiposDeEntidad.Admitidos);
    }

    /// <summary>
    /// Enviar un Festival a revision no puede escribir ni un solo valor que la base rechace.
    /// </summary>
    /// <remarks>
    /// Recorre el camino real por HTTP —registro, organizacion, borrador, envio— y despues
    /// mira las TRES filas que ese envio produce: el Festival, el historial de revision y la
    /// bitacora. Las tres fallaban contra SQL Server, cada una por su cuenta y por un motivo
    /// distinto. Comprobar solo el estado del Festival habria dejado dos de los tres fallos
    /// dentro.
    /// </remarks>
    [Fact]
    public async Task Enviar_A_Revision_Solo_Escribe_Valores_Que_La_Base_Admite()
    {
        const string correo = "vocabulario.estados@example.com";
        var cliente = _factory.CreateClient();
        var registro = await cliente.PostAsJsonAsync("/api/v1/externo/auth/register", new ExternalRegisterRequest
        {
            OrganizationName = "Organizacion de prueba",
            HeadquartersDepartmentCode = "05",
            HeadquartersMunicipalityCode = "05001",
            FullName = "Responsable de agrupación",
            FirstName = "Responsable",
            FirstSurname = "Agrupación",
            DocumentType = "CC",
            DocumentNumber = "1020304051",
            NumeroDocumento = "1020304051",
            Phone = "3000000000",
            Email = correo,
            Password = "ClaveExterna123",
            PoliticasAceptadas = ["tratamiento", "terminos"],
        });
        registro.EnsureSuccessStatusCode();
        var cuenta = await registro.Content.ReadFromJsonAsync<ExternalRegisterResponse>();
        Assert.NotNull(cuenta);
        (await cliente.PostAsJsonAsync("/api/v1/externo/auth/login",
            new ExternalLoginRequest { Email = correo, Password = "ClaveExterna123" })).EnsureSuccessStatusCode();

        var organizacion = await OrganizacionDeLaCuenta.PrepararAsync(
            cliente, nombre: "Agrupación Vocabulario", correoContacto: "contacto@agrupacion-vocabulario.test", departamento: "05", municipio: "05001");

        var festival = await CrearAsync<FestivalBorradorDto>(cliente, $"/api/v1/externo/organizaciones/{organizacion.Id}/festivales", new
        {
            nombre = "Festival del Vocabulario",
            nivelCobertura = "municipal",
            codigoDepartamento = "05",
            codigoMunicipio = "05001",
        });

        // Linea base: el borrador recien creado ya tiene que estar en un codigo valido. Antes
        // se escribia "Borrador", que pasaba solo porque la base local ignora mayusculas.
        var festivalId = int.Parse(festival.Id);
        using (var alta = _factory.Services.CreateScope())
        {
            var db = alta.ServiceProvider.GetRequiredService<PnmcDbContext>();
            var fila = db.FestivalRecords.Single(item => item.Id == festivalId);
            Assert.Contains(fila.StatusCode, CodigosDeLaBase);
            Assert.Equal(EstadosFestival.Borrador, fila.StatusCode);
        }

        var enviar = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/externo/festivales/{festivalId}/enviar-a-revision")
        {
            Content = JsonContent.Create(new { }),
        };
        enviar.Headers.Add("X-CSRF-TOKEN", await ObtenerCsrfAsync(cliente));
        var respuesta = await cliente.SendAsync(enviar);
        Assert.Equal(System.Net.HttpStatusCode.OK, respuesta.StatusCode);

        // El CONTRATO no cambia: el front-end pinta el aviso de «en revision» comparando
        // contra 'EnRevision', y si esta linea dejase de cumplirse ese aviso desapareceria en
        // silencio, sin error y sin 400.
        var devuelto = await respuesta.Content.ReadFromJsonAsync<FestivalBorradorDto>();
        Assert.Equal("EnRevision", devuelto!.Estado);

        using var scope = _factory.Services.CreateScope();
        var contexto = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();

        // 1. El Festival. Violaba FK_Festivales_EstadosContenido.
        var guardado = contexto.FestivalRecords.Single(item => item.Id == festivalId);
        Assert.Equal(EstadosFestival.EnRevision, guardado.StatusCode);
        Assert.Contains(guardado.StatusCode, CodigosDeLaBase);

        // 2. El historial de revision. Violaba CK_RegistrosRevisionHistorial_EstadoNuevo.
        var historial = contexto.HistorialesRevisionRegistros
            .Single(item => item.ModuloId == Modulos.Festivales && item.RegistroId == festival.Id);
        Assert.Contains(historial.EstadoNuevo, CodigosDeLaBase);
        Assert.Equal(EstadosFestival.EnRevision, historial.EstadoNuevo);
        Assert.Contains(historial.EstadoAnterior!, CodigosDeLaBase);
        // El evento funcional SI vive aqui: esta columna no tiene CHECK y es su sitio, segun
        // la separacion de §39 entre historial de revision y auditoria.
        Assert.Equal("FestivalEnviadoARevision", historial.Accion);

        // 3. La bitacora. Violaba CK_BitacoraAuditoria_Accion.
        //
        // EL FILTRO LLEVA LA TABLA, Y NO ES ADORNO. Hasta el 25 ago 2026 filtraba solo por
        // RecordId, y eso solo funcionaba mientras el identificador del festival no coincidiera
        // con el de OTRO registro auditado. Coincidio en cuanto la siembra gano una entidad mas
        // —la organizacion institucional tomo IdEntidad 1 y corrio las demas—: la
        // organizacion de esta prueba paso a tener el mismo numero que el festival, y sus dos
        // filas de `Entidades`, que llevan ValoresNuevos nulo, entraron en la lista y reventaron
        // el `Single` con una NullReferenceException.
        //
        // La bitacora es de todo el sistema y su clave es (tabla, registro), nunca el registro
        // solo. Que esto aguantara hasta hoy fue casualidad aritmetica, no correccion.
        var bitacora = contexto.AuditLogs
            .Where(item => item.TableName == "Festivales" && item.RecordId == festival.Id)
            .ToList();
        Assert.NotEmpty(bitacora);
        Assert.All(bitacora, fila => Assert.Contains(fila.Action, VerbosDeLaBase));
        var envio = bitacora.Single(item => item.NewValuesJson?.Contains("FestivalEnviadoARevision", StringComparison.Ordinal) == true);
        Assert.Equal(AccionesAuditoria.Actualizar, envio.Action);
        Assert.Contains($"\"Estado\":\"{EstadosFestival.EnRevision}\"", envio.NewValuesJson!);
    }

    /// <summary>
    /// La traduccion entre el contrato y el almacenamiento no puede perder nada por el camino.
    /// </summary>
    /// <remarks>
    /// El lector es tolerante a proposito —acepta <c>EnRevision</c> y <c>en_revision</c>—
    /// porque durante la transicion conviven peticiones de las dos epocas. Lo que NO puede
    /// hacer es adivinar: un valor desconocido devuelve <c>null</c> para que quien llama lo
    /// trate como peticion invalida, en vez de caer a un estado por defecto que nadie pidio.
    /// </remarks>
    [Theory]
    [InlineData("EnRevision", "en_revision")]
    [InlineData("en_revision", "en_revision")]
    [InlineData("ENREVISION", "en_revision")]
    [InlineData("AjustesSolicitados", "ajustes_solicitados")]
    [InlineData("  Borrador  ", "borrador")]
    [InlineData("Publicado", "publicado")]
    public void El_Puente_Traduce_Las_Dos_Grafias(string entrada, string esperado) =>
        Assert.Equal(esperado, EstadosFestival.DesdeContrato(entrada));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("EnRevisionInstitucional")]
    [InlineData("cualquier_cosa")]
    public void El_Puente_No_Adivina(string? entrada) =>
        Assert.Null(EstadosFestival.DesdeContrato(entrada));

    [Fact]
    public void La_Ida_Y_La_Vuelta_Son_Simetricas()
    {
        foreach (var codigo in CodigosDeLaBase)
        {
            var contrato = EstadosFestival.HaciaContrato(codigo);
            Assert.NotNull(contrato);
            Assert.Equal(codigo, EstadosFestival.DesdeContrato(contrato));
        }
    }

    /// <summary>
    /// Ningun evento del circuito puede traducirse a un verbo que la base rechace.
    /// </summary>
    /// <remarks>
    /// Incluye un evento inventado a proposito: el caso por defecto debe dar
    /// <c>actualizar</c>, no una excepcion. Un evento nuevo sin traducir tiene que quedar
    /// registrado como modificacion generica antes que tumbar la operacion del usuario o
    /// dejar la accion sin rastro.
    /// </remarks>
    [Theory]
    [InlineData("FestivalCreado", "crear")]
    [InlineData("FestivalBorradorActualizado", "actualizar")]
    [InlineData("FestivalEnviadoARevision", "actualizar")]
    [InlineData("FestivalCambioPropuesto", "crear")]
    [InlineData("FestivalCambioPropuestoActualizado", "actualizar")]
    [InlineData("FestivalPropuestaEnviadaARevision", "actualizar")]
    [InlineData("FestivalPropuestaPublicada", "publicar")]
    [InlineData("FestivalHistoricoNormalizado", "actualizar")]
    [InlineData("EventoQueTodaviaNoExiste", "actualizar")]
    public void Todo_Evento_Se_Traduce_A_Un_Verbo_Admitido(string evento, string esperado)
    {
        var verbo = AccionesAuditoria.DeEvento(evento);
        Assert.Equal(esperado, verbo);
        Assert.Contains(verbo, VerbosDeLaBase);
        Assert.True(AccionesAuditoria.EsAdmitido(verbo));
    }

    private static async Task<string> ObtenerCsrfAsync(HttpClient cliente)
    {
        var respuesta = await cliente.GetAsync("/api/v1/externo/organizaciones/csrf");
        respuesta.EnsureSuccessStatusCode();
        var token = await respuesta.Content.ReadFromJsonAsync<ExternalCsrfTokenResponse>();
        return token!.RequestToken;
    }

    private static async Task<T> CrearAsync<T>(HttpClient cliente, string ruta, object cuerpo)
    {
        var mensaje = new HttpRequestMessage(HttpMethod.Post, ruta) { Content = JsonContent.Create(cuerpo) };
        mensaje.Headers.Add("X-CSRF-TOKEN", await ObtenerCsrfAsync(cliente));
        var respuesta = await cliente.SendAsync(mensaje);
        respuesta.EnsureSuccessStatusCode();
        var creado = await respuesta.Content.ReadFromJsonAsync<T>();
        Assert.NotNull(creado);
        return creado!;
    }
}
