using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PNMC.Contracts;
using PNMC.Domain.Entities;
using PNMC.Infrastructure.Data;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// Publicar, despublicar y archivar una Edición contra el motor real.
/// </summary>
/// <remarks>
/// <para>
/// <b>POR QUE VIVE EN LA VIA DE SQL SERVER.</b> Porque el defecto que fija <b>no existe en SQLite</b>.
/// Medido contra la base local: <c>POST /externo/ediciones/{id}/publicar</c>
/// escribía <c>EstadoNuevo = "publicada"</c> en <c>dbo.RegistrosRevisionHistorial</c>, cuya
/// restricción <c>CK_RegistrosRevisionHistorial_EstadoNuevo</c> solo admite los siete códigos del
/// circuito de contenidos —<c>publicado</c>, en masculino—. La ruta devolvía <b>500 siempre</b>, y
/// las veinte pruebas de <c>EdicionesDelFestivalTests</c> pasaban en verde porque el arnés construye
/// SQLite desde el modelo de EF y ahí esa restricción no está.
/// </para>
/// <para>
/// <b>ASI SE ENCONTRO:</b> recorriendo el flujo en el navegador contra la base real, que es donde el
/// usuario lo vivía. Su frase era «tengo una edición en estado borrador y no existe una ruta clara
/// para publicarla»; la ruta existía y estaba rota.
/// </para>
/// <para>
/// Es la misma lección que <c>EstadosFestival</c> ya dejó escrita sobre <c>Publicada</c> en las
/// propuestas de cambio: lo que impone el motor no se deduce leyendo el ORM. Los dos vocabularios se
/// conservan —la Edición nombra su visibilidad en femenino, el historial es institucional y común—;
/// lo que faltaba era la traducción.
/// </para>
/// <para>
/// Omitida salvo que se encienda <c>PNMC_PRUEBAS_SQLSERVER=1</c>. Ver <c>ArnesSqlServer</c>.
/// </para>
/// </remarks>
[Collection(ColeccionSqlServer.Nombre)]
public sealed class PublicarUnaEdicionSqlServerTests
{
    private const string Clave = "ClaveExterna123";

    private readonly SqlServerFixture _base;

    public PublicarUnaEdicionSqlServerTests(SqlServerFixture baseDesechable) => _base = baseDesechable;

    [HechoSqlServer]
    public async Task El_Ciclo_De_Una_Edicion_No_Choca_Con_La_Restriccion_Del_Historial()
    {
        var (cliente, festivalId) = await FestivalPublicadoAsync("ciclo");
        var edicionId = await CrearEdicionAsync(cliente, festivalId, 2028);

        // PUBLICAR. Aquí es donde estaba el 500.
        var publicada = await EnviarAsync(cliente, $"/api/v1/externo/ediciones/{edicionId}/publicar");
        Assert.Equal(HttpStatusCode.OK, publicada.StatusCode);
        Assert.Equal("publicada", (await publicada.Content.ReadFromJsonAsync<EdicionFestivalDto>())!.EstadoVisibilidad);

        // DESPUBLICAR Y ARCHIVAR pasan por el mismo manejador y escriben en la misma tabla, así que
        // heredarían el mismo defecto si la traducción no estuviera en un solo sitio.
        Assert.Equal(HttpStatusCode.OK, (await EnviarAsync(cliente, $"/api/v1/externo/ediciones/{edicionId}/despublicar")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await EnviarAsync(cliente, $"/api/v1/externo/ediciones/{edicionId}/archivar")).StatusCode);

        using var alcance = _base.Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var historial = await db.HistorialesRevisionRegistros.AsNoTracking()
            .Where(x => x.ModuloId == Modulos.EdicionesDeFestival && x.RegistroId == edicionId.ToString(CultureInfo.InvariantCulture))
            .ToListAsync();

        // LAS TRES TRANSICIONES DEJARON RASTRO, que es la mitad que importa: una traducción que
        // funcionara dejando de escribir el historial habría hecho pasar la prueba y perdido la traza.
        Assert.Equal(3, historial.Count);

        // Y TODAS EN EL VOCABULARIO DEL HISTORIAL: si alguna trajera «publicada» o «archivada», el
        // INSERT no habría llegado hasta aquí, pero se comprueba igualmente para que el motivo del
        // fallo se lea en la aserción y no en un 500.
        string[] admitidos = ["borrador", "en_revision", "ajustes_solicitados", "aprobado", "publicado", "rechazado", "archivado"];
        Assert.All(historial, fila => Assert.Contains(fila.EstadoNuevo, admitidos));
        Assert.All(historial, fila => Assert.Contains(fila.EstadoAnterior!, admitidos));
    }


    /// <summary>
    /// Una edición con tablas hijas se puede borrar.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>EL BORRADO MORIA CON UN 500 EN CUANTO LA EDICION TENIA ALGO DENTRO.</b>
    /// <c>DELETE /externo/ediciones/{id}</c> quitaba la fila de <c>dbo.EdicionesFestival</c> y no
    /// tocaba ninguna de sus OCHO tablas hijas, así que la clave foránea paraba el borrado. Medido
    /// contra <c>PNMC_LOCAL</c> registrando una edición con un
    /// municipio, una entidad aliada y un material: <c>The DELETE statement conflicted with the
    /// REFERENCE constraint "FK_EdicionesFestivalLocalizaciones_Edicion"</c>, y en pantalla un
    /// «no fue posible» sin motivo.
    /// </para>
    /// <para>
    /// <b>POR QUE AQUI Y NO EN LAS VEINTE PRUEBAS DE SQLITE.</b> Por lo mismo que la de publicar,
    /// una línea más arriba: el arnés de SQLite construye el esquema desde el modelo de EF, y ahí la
    /// restricción no frena nada. Las claves foráneas de SQL Server son lo que el usuario tiene
    /// delante.
    /// </para>
    /// <para>
    /// LA EDICION SE CREA CON LAS TRES TABLAS QUE LA ROMPIAN, y no solo con la localización: un
    /// borrado que limpiara una sola de las ocho pasaría esta prueba si midiera una sola.
    /// </para>
    /// </remarks>
    [HechoSqlServer]
    public async Task Una_Edicion_Con_Tablas_Hijas_Se_Borra_Sin_Chocar_Con_Las_Foraneas()
    {
        var (cliente, festivalId) = await FestivalPublicadoAsync("borrar");
        var (departamento, municipio) = await TerritorioRealAsync();

        var creada = await EnviarAsync(cliente, $"/api/v1/externo/festivales/{festivalId}/ediciones", new
        {
            anio = 2029,
            nombre = "Edición con hijas",
            localizaciones = new[] { new { codigoDepartamento = departamento, codigoMunicipio = municipio, zonaUrbanoRuralId = (int?)null, titulacionColectivaId = (int?)null } },
            entidadesAliadas = new[] { new { nombre = "Aliada", correo = (string?)null, naturalezaEntidadId = (int?)null, entidadId = (int?)null } },
            materiales = new[] { new { url = "https://ejemplo.org/afiche.jpg", descripcionArchivo = "Afiche" } },
        });
        creada.EnsureSuccessStatusCode();
        var edicionId = int.Parse(
            (await creada.Content.ReadFromJsonAsync<EdicionFestivalDto>())!.Id, CultureInfo.InvariantCulture);

        // LAS TRES ESTAN DE VERDAD ANTES DE BORRAR. Sin esta comprobación, un alta que no guardara
        // las hijas dejaría la prueba en verde sin haber ejercido el caso.
        using (var alcance = _base.Services.CreateScope())
        {
            var db = alcance.ServiceProvider.GetRequiredService<PnmcDbContext>();
            Assert.True(await db.Relacion<LocalizacionDeRegistroRow>(Modulos.EdicionesDeFestival, edicionId).AnyAsync());
            Assert.True(await db.Relacion<EntidadAliadaDeRegistroRow>(Modulos.EdicionesDeFestival, edicionId).AnyAsync());
            Assert.True(await db.Relacion<ArchivoDeRegistroRow>(Modulos.EdicionesDeFestival, edicionId).AnyAsync());
        }

        var testigo = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/externo/organizaciones/csrf");
        var peticion = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/externo/ediciones/{edicionId}");
        peticion.Headers.Add("X-CSRF-TOKEN", testigo.GetProperty("requestToken").GetString());
        var borrada = await cliente.SendAsync(peticion);

        Assert.Equal(HttpStatusCode.NoContent, borrada.StatusCode);

        using (var alcance = _base.Services.CreateScope())
        {
            var db = alcance.ServiceProvider.GetRequiredService<PnmcDbContext>();
            Assert.False(await db.EdicionesFestival.AnyAsync(x => x.Id == edicionId));
            // Y NO QUEDA HUERFANA NINGUNA FILA: borrar la edición dejando sus hijas cambiaría el 500
            // por basura silenciosa, que es peor.
            Assert.False(await db.Relacion<LocalizacionDeRegistroRow>(Modulos.EdicionesDeFestival, edicionId).AnyAsync());
            Assert.False(await db.Relacion<EntidadAliadaDeRegistroRow>(Modulos.EdicionesDeFestival, edicionId).AnyAsync());
            Assert.False(await db.Relacion<ArchivoDeRegistroRow>(Modulos.EdicionesDeFestival, edicionId).AnyAsync());
        }
    }

    /// <summary>
    /// Una edición con revisión registrada no se borra: se explica por qué.
    /// </summary>
    /// <remarks>
    /// <b>ES LA OTRA MITAD DE LA CORRECCION, y es una decisión, no un detalle.</b> Para que el
    /// borrado funcionara hacía falta quitar lo que cuelga de la edición; sus OBSERVACIONES DE
    /// REVISION también cuelgan, y son el expediente de lo que el PNMC pidió y de lo que la
    /// organización atendió. Arrastrarlas al borrado habría hecho pasar la prueba de arriba
    /// destruyendo historial en silencio. La regla es que ahí el botón no llega, y lo dice.
    /// </remarks>
    [HechoSqlServer]
    public async Task Una_Edicion_Con_Revision_No_Se_Borra_Y_Dice_Por_Que()
    {
        var (cliente, festivalId) = await FestivalPublicadoAsync("revisada");
        var edicionId = await CrearEdicionAsync(cliente, festivalId, 2030);

        using (var alcance = _base.Services.CreateScope())
        {
            var db = alcance.ServiceProvider.GetRequiredService<PnmcDbContext>();
            var revisor = await db.Users.AsNoTracking().OrderBy(x => x.Id).FirstAsync();
            db.RevisionesDeRegistro.Add(new PNMC.Domain.Entities.RevisionDeRegistroRow
            {
                ModuloId = Modulos.EdicionesDeFestival,
                RegistroId = edicionId.ToString(CultureInfo.InvariantCulture),
                Estado = "borrador",
                IdUsuarioRevisor = revisor.Id,
                FechaCreacion = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var testigo = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/externo/organizaciones/csrf");
        var peticion = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/externo/ediciones/{edicionId}");
        peticion.Headers.Add("X-CSRF-TOKEN", testigo.GetProperty("requestToken").GetString());
        var respuesta = await cliente.SendAsync(peticion);

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Contains("revisión", await respuesta.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        using (var alcance = _base.Services.CreateScope())
        {
            var db = alcance.ServiceProvider.GetRequiredService<PnmcDbContext>();
            Assert.True(await db.EdicionesFestival.AnyAsync(x => x.Id == edicionId));
            var clave = edicionId.ToString(CultureInfo.InvariantCulture);
            Assert.True(await db.RevisionesDeRegistro.AnyAsync(x => x.ModuloId == Modulos.EdicionesDeFestival && x.RegistroId == clave));
        }
    }


    /// <summary>
    /// Lo que estuvo publicado no se borra: se archiva.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>EL BORRADO MIRABA UN SOLO EJE DE LOS DOS.</b> Su guarda comprobaba <c>Estado</c> —el eje
    /// de la REALIZACION: en preparacion, programada, realizada, cancelada— y no
    /// <c>EstadoVisibilidad</c>, que es el eje EDITORIAL. Una edicion publicada, luego despublicada
    /// y archivada conserva <c>Estado = "en_preparacion"</c>, asi que el borrado la aceptaba.
    /// </para>
    /// <para>
    /// <b>QUE SE PERDIA.</b> Medido contra PNMC_LOCAL recorriendo el
    /// circuito entero: el 204 se llevo un registro que el portal habia mostrado y dejo TRES filas
    /// de <c>dbo.RegistrosRevisionHistorial</c> apuntando a un <c>RegistroId</c> inexistente. El
    /// rastro de las transiciones sobrevivia; el registro del que hablaban, no.
    /// </para>
    /// <para>
    /// LA PRUEBA DESPUBLICA ANTES DE BORRAR, y ese es el caso que importa: despues de despublicar,
    /// <c>EstadoVisibilidad</c> vuelve a <c>borrador</c>, asi que mirar el estado de AHORA no basta
    /// —diria que nunca se publico—. La pregunta es si llego a publicarse alguna vez, y eso solo lo
    /// contesta el historial.
    /// </para>
    /// </remarks>
    [HechoSqlServer]
    public async Task Una_Edicion_Que_Estuvo_Publicada_No_Se_Borra_Aunque_Se_Despublique()
    {
        var (cliente, festivalId) = await FestivalPublicadoAsync("publicada");
        var edicionId = await CrearEdicionAsync(cliente, festivalId, 2031);

        Assert.Equal(HttpStatusCode.OK, (await EnviarAsync(cliente, $"/api/v1/externo/ediciones/{edicionId}/publicar")).StatusCode);
        // DESPUBLICAR LA DEVUELVE A `borrador`: aqui es donde la guarda ingenua se dejaria engañar.
        Assert.Equal(HttpStatusCode.OK, (await EnviarAsync(cliente, $"/api/v1/externo/ediciones/{edicionId}/despublicar")).StatusCode);

        var testigo = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/externo/organizaciones/csrf");
        var peticion = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/externo/ediciones/{edicionId}");
        peticion.Headers.Add("X-CSRF-TOKEN", testigo.GetProperty("requestToken").GetString());
        var respuesta = await cliente.SendAsync(peticion);

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadAsStringAsync();
        // Y DICE QUE HACER EN VEZ DE SOLO NEGARSE: archivar es el camino, y existe.
        Assert.Contains("Archívala", cuerpo, StringComparison.OrdinalIgnoreCase);

        // Y UNA VEZ ARCHIVADA, EL AVISO CAMBIA: mandar a archivar algo ya archivado hace que un
        // aviso correcto se lea como un fallo.
        Assert.Equal(HttpStatusCode.OK, (await EnviarAsync(cliente, $"/api/v1/externo/ediciones/{edicionId}/archivar")).StatusCode);
        var testigoDos = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/externo/organizaciones/csrf");
        var segunda = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/externo/ediciones/{edicionId}");
        segunda.Headers.Add("X-CSRF-TOKEN", testigoDos.GetProperty("requestToken").GetString());
        var respuestaDos = await cliente.SendAsync(segunda);
        Assert.Equal(HttpStatusCode.Conflict, respuestaDos.StatusCode);
        var cuerpoDos = await respuestaDos.Content.ReadAsStringAsync();
        Assert.Contains("Ya está archivada", cuerpoDos, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Archívala", cuerpoDos, StringComparison.OrdinalIgnoreCase);

        using var alcance = _base.Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<PnmcDbContext>();
        Assert.True(await db.EdicionesFestival.AnyAsync(x => x.Id == edicionId));

        // Y EL HISTORIAL NO SE QUEDA HUERFANO, que es el daño concreto que esto impide.
        var clave = edicionId.ToString(CultureInfo.InvariantCulture);
        var historial = await db.HistorialesRevisionRegistros.AsNoTracking()
            .Where(x => x.ModuloId == Modulos.EdicionesDeFestival && x.RegistroId == clave).CountAsync();
        Assert.Equal(3, historial);
    }


    /// <summary>
    /// La lista dice cuáles se pueden eliminar, y solo esas.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>ES LO QUE DECIDE SI EL BOTON EXISTE.</b> «Se elimina un borrador registrado por error; lo
    /// que se publicó en algún momento sí debe quedar archivado», de la dirección de producto el 13 de
    /// septiembre de 2026. Un «Eliminar» que contesta 409 obliga a descubrir por ensayo y error qué
    /// se puede hacer con cada fila, que es justo lo que la lista existe para evitar.
    /// </para>
    /// <para>
    /// <b>LA PRUEBA DESPUBLICA ANTES DE MIRAR, y ahí está el caso que importa:</b> al despublicar,
    /// `EstadoVisibilidad` vuelve a «borrador», así que un cliente que mirara el estado de ahora
    /// ofrecería eliminar algo que el público ya vio. La respuesta está en el historial.
    /// </para>
    /// </remarks>
    [HechoSqlServer]
    public async Task La_Lista_Solo_Marca_Como_Eliminables_Los_Borradores_Que_Nunca_Se_Publicaron()
    {
        var (cliente, festivalId) = await FestivalPublicadoAsync("eliminables");

        var virgen = await CrearEdicionAsync(cliente, festivalId, 2032);
        var publicadaYRetirada = await CrearEdicionAsync(cliente, festivalId, 2033);
        Assert.Equal(HttpStatusCode.OK, (await EnviarAsync(cliente, $"/api/v1/externo/ediciones/{publicadaYRetirada}/publicar")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await EnviarAsync(cliente, $"/api/v1/externo/ediciones/{publicadaYRetirada}/despublicar")).StatusCode);

        var lista = await cliente.GetFromJsonAsync<List<EdicionFestivalDto>>(
            $"/api/v1/externo/festivales/{festivalId}/ediciones");

        var sinPublicar = Assert.Single(lista!, x => x.Id == virgen.ToString(CultureInfo.InvariantCulture));
        var yaVista = Assert.Single(lista!, x => x.Id == publicadaYRetirada.ToString(CultureInfo.InvariantCulture));

        Assert.True(sinPublicar.SePuedeEliminar, "un borrador que nunca se publicó sí se elimina");
        Assert.False(yaVista.SePuedeEliminar, "la que estuvo publicada se archiva, no se elimina");
        // Y LAS DOS DICEN «borrador»: si la regla mirara el estado de ahora, las dos darían true.
        Assert.Equal("borrador", yaVista.EstadoVisibilidad);

        // LO QUE LA LISTA PROMETE, EL BORRADO LO CUMPLE. Sin esto, la marca podría decir que sí
        // sobre algo que después contesta 409, que es el defecto que esta prueba existe para evitar.
        var testigo = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/externo/organizaciones/csrf");
        var peticion = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/externo/ediciones/{virgen}");
        peticion.Headers.Add("X-CSRF-TOKEN", testigo.GetProperty("requestToken").GetString());
        Assert.Equal(HttpStatusCode.NoContent, (await cliente.SendAsync(peticion)).StatusCode);
    }

    // ---------- Andamio -----------------------------------------------------------------------

    private async Task<(HttpClient Cliente, int FestivalId)> FestivalPublicadoAsync(string marca)
    {
        var cliente = _base.CrearCliente();
        // EL SUFIJO SE RECORTA ANTES DE PEGAR EL DOMINIO. Recortar la dirección entera partía
        // «@organizacion.test» por la mitad y el alta respondía 400 por un correo inválido, que no
        // es lo que esta prueba mide.
        var correo = $"edicion.{marca}.{Guid.NewGuid():N}"[..24] + "@organizacion.test";

        // EL TERRITORIO SALE DE LA BASE Y NO SE ESCRIBE A MANO. La base desechable siembra DIVIPOLA
        // de referencia; un «05001» fijo puede no estar ahí, y el alta responde 400 por un motivo
        // que no tiene nada que ver con lo que esta prueba mide.
        var (departamento, municipio) = await TerritorioRealAsync();

        (await cliente.PostAsJsonAsync("/api/v1/externo/auth/register", new ExternalRegisterRequest
        {
            OrganizationName = "Organización " + marca,
            FullName = "Responsable " + marca,
            NumeroDocumento = System.Security.Cryptography.RandomNumberGenerator.GetInt32(100000000, 999999999).ToString(CultureInfo.InvariantCulture),
            HeadquartersDepartmentCode = departamento,
            HeadquartersMunicipalityCode = municipio,
            Phone = "3000000000",
            Email = correo,
            Password = Clave,
            PoliticasAceptadas = ["tratamiento", "terminos"],
        })).EnsureSuccessStatusCode();

        (await cliente.PostAsJsonAsync("/api/v1/externo/auth/login",
            new ExternalLoginRequest { Email = correo, Password = Clave })).EnsureSuccessStatusCode();

        var mias = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/externo/organizaciones/mis");
        var organizacionId = int.Parse(mias[0].GetProperty("id").GetString()!, CultureInfo.InvariantCulture);

        var creado = await EnviarAsync(cliente, $"/api/v1/externo/organizaciones/{organizacionId}/festivales", new
        {
            nombre = "Festival " + marca,
            nivelCobertura = "nacional",
            practicasMusicalesIds = Array.Empty<int>(),
            territoriosSonorosIds = Array.Empty<int>(),
        });
        creado.EnsureSuccessStatusCode();
        var festivalId = int.Parse(
            (await creado.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!,
            CultureInfo.InvariantCulture);

        // SE PUBLICA POR LA BASE: lo que mide esta prueba es el ciclo de la Edición, no el del
        // Festival, que tiene su propio circuito y sus propias pruebas.
        using (var alcance = _base.Services.CreateScope())
        {
            var db = alcance.ServiceProvider.GetRequiredService<PnmcDbContext>();
            var festival = await db.FestivalRecords.SingleAsync(x => x.Id == festivalId);
            festival.StatusCode = "publicado";
            await db.SaveChangesAsync();
        }

        return (cliente, festivalId);
    }

    private async Task<(string Departamento, string Municipio)> TerritorioRealAsync()
    {
        var par = Convert.ToString(await _base.EscalarAsync(
            "SELECT TOP (1) CodigoDepartamento + N'|' + CodigoMunicipio FROM dbo.Divipola "
            + "ORDER BY CodigoDepartamento, CodigoMunicipio;"), CultureInfo.InvariantCulture);
        Assert.False(string.IsNullOrWhiteSpace(par),
            "dbo.Divipola esta vacia: sin territorio no se puede crear una organizacion ni un Festival.");
        var partes = par!.Split('|');
        return (partes[0], partes[1]);
    }

    private static async Task<int> CrearEdicionAsync(HttpClient cliente, int festivalId, int anio)
    {
        var creada = await EnviarAsync(cliente, $"/api/v1/externo/festivales/{festivalId}/ediciones", new
        {
            anio,
            nombre = $"Edición {anio}",
        });
        creada.EnsureSuccessStatusCode();
        var cuerpo = await creada.Content.ReadFromJsonAsync<EdicionFestivalDto>();
        return int.Parse(cuerpo!.Id, CultureInfo.InvariantCulture);
    }

    private static async Task<HttpResponseMessage> EnviarAsync(HttpClient cliente, string ruta, object? cuerpo = null)
    {
        var testigo = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/externo/organizaciones/csrf");
        var mensaje = new HttpRequestMessage(HttpMethod.Post, ruta);
        if (cuerpo is not null) mensaje.Content = JsonContent.Create(cuerpo);
        mensaje.Headers.Add("X-CSRF-TOKEN", testigo.GetProperty("requestToken").GetString());
        return await cliente.SendAsync(mensaje);
    }
}
