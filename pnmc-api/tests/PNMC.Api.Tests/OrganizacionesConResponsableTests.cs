using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PNMC.Api.Endpoints;
using PNMC.Contracts;
using PNMC.Infrastructure.Data;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// El listado de organizaciones de la consola, con quien responde por cada una.
/// </summary>
/// <remarks>
/// <para>
/// La persona responsable —nombre, correo, telefono, documento y autorizacion de datos— vive en
/// <c>EntidadesResponsable</c>, en una tabla aparte y por privacidad. No se deduce de la cuenta
/// institucional asignada al registro, porque esa cuenta puede revisarlo sin representarla.
/// </para>
/// <para>
/// LAS DOS PROCEDENCIAS SE PRUEBAN POR SEPARADO, y esa es la mitad que importa. La tabla nacio el
/// 25 de agosto de 2026 y solo la escribe el alta externa: leyendo solo esa tabla, seis de las
/// siete organizaciones de la base local saldrian «sin responsable». El respaldo es la cuenta
/// vinculada en <c>UsuariosEntidades</c>, y la respuesta tiene que DECIR cual de las dos fue: una
/// deduccion y un dato firmado no pueden pintarse igual.
/// </para>
/// </remarks>
public sealed class OrganizacionesConResponsableTests : IClassFixture<TestWebApplicationFactory>
{
    private static readonly int[] TotalesAscendentes = [0, 1, 2];
    private static readonly int[] TotalesDescendentes = [2, 1, 0];
    private static readonly string[] DepartamentosEnOrden = ["Córdoba", "Cundinamarca"];

    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public OrganizacionesConResponsableTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task EntrarComoWebmasterAsync()
    {
        var respuesta = await _client.PostAsJsonAsync("/api/v1/admin/auth/login", new AdminLoginRequest
        {
            Email = "test@pnmc.local",
            Password = "pnmc-master"
        });
        respuesta.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Una organizacion con su responsable DECLARADO en <c>EntidadesResponsable</c>, un festival a
    /// su cargo, y ademas una cuenta vinculada con otro nombre.
    /// </summary>
    /// <remarks>
    /// LA CUENTA VINCULADA SE SIEMBRA CON UN NOMBRE DISTINTO A PROPOSITO. Si las dos dijeran lo
    /// mismo, la prueba no distinguiria «leyo la tabla buena» de «leyo el respaldo»: pasaria igual
    /// con la fuente equivocada.
    /// </remarks>
    private async Task<int> SembrarConResponsableDeclaradoAsync(string sufijo)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var ahora = DateTime.UtcNow;

        var cuenta = new UserRow
        {
            FullName = "Cuenta Que No Debe Salir " + sufijo,
            Email = $"cuenta.declarada.{sufijo}@example.com",
            PasswordHash = "x",
            AccessChannel = "externo",
            IsActive = true,
            CreatedAt = ahora,
            UpdatedAt = ahora,
        };
        db.Users.Add(cuenta);

        var organizacion = new EntityProfileRow
        {
            EntityType = "organizacion",
            Name = "Organizacion Declarada " + sufijo,
            ContactEmail = $"org.declarada.{sufijo}@example.com",
            CoverageLevel = "sin_definir",
            StatusCode = "activa",
            IsActive = true,
            CreatedByUserId = 1,
            CreatedAt = ahora,
            UpdatedAt = ahora,
        };
        db.EntityProfiles.Add(organizacion);
        await db.SaveChangesAsync();

        db.UserEntities.Add(new UserEntityRow
        {
            UserId = cuenta.Id,
            EntityId = organizacion.Id,
            EntityRole = "administrador",
            IsActive = true,
            CreatedAt = ahora,
        });

        db.EntidadesResponsable.Add(new EntidadResponsableRow
        {
            IdEntidad = organizacion.Id,
            ResponsableNombre = "Persona Declarada " + sufijo,
            ResponsableTipoDocumento = "CC",
            ResponsableNumeroDocumento = "1098765432",
            ResponsableCorreo = $"persona.declarada.{sufijo}@example.com",
            ResponsableTelefono = "3001112233",
            ResponsableDesde = ahora,
            ResponsableAutorizacionDatos = true,
            FechaCreacion = ahora,
        });

        db.FestivalRecords.Add(new FestivalRow
        {
            Name = "Festival De La Declarada " + sufijo,
            CoverageLevel = "nacional",
            StatusCode = "borrador",
            OrganizacionPrincipalId = organizacion.Id,
            CreatedAt = ahora,
            UpdatedAt = ahora,
        });

        await db.SaveChangesAsync();
        return organizacion.Id;
    }

    /// <summary>Una organizacion SIN fila declarada: solo la cuenta vinculada que la administra.</summary>
    private async Task<int> SembrarSoloConCuentaAsync(string sufijo)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var ahora = DateTime.UtcNow;

        var cuenta = new UserRow
        {
            FullName = "Cuenta Duena " + sufijo,
            Email = $"cuenta.duena.{sufijo}@example.com",
            PasswordHash = "x",
            AccessChannel = "externo",
            IsActive = true,
            CreatedAt = ahora,
            UpdatedAt = ahora,
        };
        db.Users.Add(cuenta);

        var organizacion = new EntityProfileRow
        {
            EntityType = "organizacion",
            Name = "Organizacion Sin Declarar " + sufijo,
            CoverageLevel = "sin_definir",
            StatusCode = "activa",
            IsActive = true,
            CreatedByUserId = 1,
            CreatedAt = ahora,
            UpdatedAt = ahora,
        };
        db.EntityProfiles.Add(organizacion);
        await db.SaveChangesAsync();

        db.UserEntities.Add(new UserEntityRow
        {
            UserId = cuenta.Id,
            EntityId = organizacion.Id,
            EntityRole = "propietario",
            IsActive = true,
            CreatedAt = ahora,
        });
        await db.SaveChangesAsync();

        return organizacion.Id;
    }

    private async Task<OrganizacionAdminDto> BuscarAsync(string termino)
    {
        var respuesta = await _client.GetAsync("/api/v1/admin/organizaciones/?q=" + Uri.EscapeDataString(termino));
        respuesta.EnsureSuccessStatusCode();
        var carga = await respuesta.Content.ReadFromJsonAsync<OrganizacionesRespuestaDto>();
        Assert.NotNull(carga);
        return Assert.Single(carga!.Items);
    }

    [Fact]
    public async Task El_Responsable_Declarado_Manda_Sobre_La_Cuenta_Vinculada()
    {
        var id = await SembrarConResponsableDeclaradoAsync("manda");
        await EntrarComoWebmasterAsync();

        var organizacion = await BuscarAsync("Organizacion Declarada manda");

        Assert.Equal(id.ToString(CultureInfo.InvariantCulture), organizacion.Id);
        Assert.NotNull(organizacion.Responsable);

        // LA TABLA BUENA GANA. La cuenta vinculada se llama distinto justamente para que este
        // Assert falle si alguien invierte el orden de los dos intentos.
        Assert.Equal("Persona Declarada manda", organizacion.Responsable!.Nombre);
        Assert.Equal(AdminOrganizacionesEndpoints.OrigenDeclarado, organizacion.Responsable.Origen);
        Assert.Equal("CC", organizacion.Responsable.TipoDocumento);
        Assert.True(organizacion.Responsable.TieneDocumento);
        Assert.True(organizacion.Responsable.AutorizacionDatos);
    }

    [Fact]
    public async Task Sin_Fila_Declarada_Responde_La_Cuenta_Vinculada_Y_La_Respuesta_Lo_Dice()
    {
        await SembrarSoloConCuentaAsync("respaldo");
        await EntrarComoWebmasterAsync();

        var organizacion = await BuscarAsync("Organizacion Sin Declarar respaldo");

        Assert.NotNull(organizacion.Responsable);
        Assert.Equal("Cuenta Duena respaldo", organizacion.Responsable!.Nombre);

        // SIN ESTE CAMPO, UNA DEDUCCION SE PINTARIA COMO UN DATO FIRMADO. La cuenta administra la
        // organizacion; eso no es lo mismo que haber dado nombre y documento al darla de alta.
        Assert.Equal(AdminOrganizacionesEndpoints.OrigenCuenta, organizacion.Responsable.Origen);
        Assert.Equal("propietario", organizacion.Responsable.RolEntidad);
        Assert.False(organizacion.Responsable.TieneDocumento);
    }

    [Fact]
    public async Task El_Numero_De_Documento_No_Sale_Nunca_En_La_Respuesta()
    {
        await SembrarConResponsableDeclaradoAsync("cedula");
        await EntrarComoWebmasterAsync();

        // Se mira el JSON EN CRUDO y no el DTO: un campo nuevo con la cedula dentro no cambiaria
        // el DTO que esta prueba deserializa, y pasaria desapercibido.
        var respuesta = await _client.GetAsync("/api/v1/admin/organizaciones/?q=Organizacion+Declarada+cedula");
        respuesta.EnsureSuccessStatusCode();
        var crudo = await respuesta.Content.ReadAsStringAsync();

        Assert.Contains("Persona Declarada cedula", crudo, StringComparison.Ordinal);
        Assert.DoesNotContain("1098765432", crudo, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cuenta_Los_Procesos_Que_Responde_Cada_Organizacion()
    {
        await SembrarConResponsableDeclaradoAsync("procesos");
        await EntrarComoWebmasterAsync();

        var organizacion = await BuscarAsync("Organizacion Declarada procesos");

        // Un festival sembrado a su nombre. Esta etapa sólo expone el proceso habilitado.
        Assert.Equal(1, organizacion.Procesos.Festivales);
        Assert.Equal(1, organizacion.Procesos.Total);
    }

    [Fact]
    public async Task Los_Totales_Por_Estado_Se_Cuentan_Sobre_Todas_Y_No_Sobre_El_Filtro()
    {
        await SembrarConResponsableDeclaradoAsync("estados");   // nace «activa»
        await SembrarParaOrdenAsync(
            "Organizacion Inactiva estados", new DateTime(2026, 8, 1, 8, 0, 0, DateTimeKind.Utc), estado: "inactiva");
        await EntrarComoWebmasterAsync();

        var respuesta = await _client.GetAsync("/api/v1/admin/organizaciones/?estado=activa");
        respuesta.EnsureSuccessStatusCode();
        var carga = await respuesta.Content.ReadFromJsonAsync<OrganizacionesRespuestaDto>();
        Assert.NotNull(carga);

        // Lo devuelto SI esta filtrado...
        Assert.NotEmpty(carga!.Items);
        Assert.All(carga.Items, item => Assert.Equal("activa", item.Estado));

        // ...y las cifras de las pestañas NO: con el filtro puesto, «inactiva» tiene que seguir
        // diciendo cuantas hay. Si se recalcularan filtradas, todas las demas dirian cero y
        // pareceria que no hay nada en ellas.
        Assert.Contains(carga.Estados, e => e.Id == "inactiva" && e.Total > 0);
        Assert.Contains(carga.Estados, e => e.Id == "activa" && e.Total > 0);
    }

    [Fact]
    public async Task El_Estado_De_La_Organizacion_Tiene_Etiqueta_Propia()
    {
        // El desplegable de la consola debe nombrar el estado para no mostrar el código en crudo:
        // «pendiente_de_confirmacion» en pantalla es un defecto, no un dato.
        await SembrarConResponsableDeclaradoAsync("etiqueta");
        await EntrarComoWebmasterAsync();

        var organizacion = await BuscarAsync("Organizacion Declarada etiqueta");

        Assert.Equal("activa", organizacion.Estado);
        Assert.Equal("Activa", organizacion.EstadoEtiqueta);
    }

    [Fact]
    public async Task El_Tamano_De_Pagina_Lo_Decide_El_Servidor()
    {
        await SembrarSoloConCuentaAsync("tope");
        await EntrarComoWebmasterAsync();

        // Cada pagina dispara ocho consultas de apoyo: el cliente no decide cuanto trabajo pedir.
        var respuesta = await _client.GetAsync("/api/v1/admin/organizaciones/?tamano=100000");
        respuesta.EnsureSuccessStatusCode();
        var carga = await respuesta.Content.ReadFromJsonAsync<OrganizacionesRespuestaDto>();

        Assert.NotNull(carga);
        Assert.Equal(100, carga!.TamanoPagina);
        Assert.True(carga.Items.Count <= 100);
    }

    // ================================================================================
    // ORDEN Y FILTROS · pedidos
    // ================================================================================

    /// <summary>
    /// Una organizacion con TODO lo que hace falta para probar un orden: cuando se creo, donde
    /// esta, en que estado, quien responde y cuantos procesos sostiene.
    /// </summary>
    /// <remarks>
    /// LOS AYUDANTES DE ARRIBA NO SIRVEN PARA ESTO porque fijan <c>CreatedAt</c> en
    /// <c>DateTime.UtcNow</c>: dos organizaciones sembradas seguidas pueden caer en la misma marca
    /// y el orden por fecha quedaria decidido por el desempate, no por la fecha.
    /// </remarks>
    private async Task<int> SembrarParaOrdenAsync(
        string nombre,
        DateTime creada,
        string estado = "activa",
        string? departamento = null,
        string? municipio = null,
        string? responsableDeclarado = null,
        string? cuentaVinculada = null,
        int festivales = 0)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();

        var organizacion = new EntityProfileRow
        {
            EntityType = "organizacion",
            Name = nombre,
            StatusCode = estado,
            HeadquartersDepartmentCode = departamento,
            HeadquartersMunicipalityCode = municipio,
            // SE DERIVA DEL ESTADO, que es el único eje. Ponerlo fijo en `true` sembraría una
            // organización «inactiva» que sigue vigente, y CK_Entidades_VigenciaCoherente la
            // rechazaría contra SQL Server —donde además esta prueba no corre, así que el fallo
            // aparecería en otra—.
            IsActive = estado is "activa" or "pendiente_de_confirmacion",
            CreatedByUserId = 1,
            CreatedAt = creada,
            UpdatedAt = creada,
        };
        db.EntityProfiles.Add(organizacion);
        await db.SaveChangesAsync();

        if (responsableDeclarado is not null)
        {
            db.EntidadesResponsable.Add(new EntidadResponsableRow
            {
                IdEntidad = organizacion.Id,
                ResponsableNombre = responsableDeclarado,
                ResponsableDesde = creada,
                ResponsableAutorizacionDatos = true,
                FechaCreacion = creada,
            });
        }

        if (cuentaVinculada is not null)
        {
            var cuenta = new UserRow
            {
                FullName = cuentaVinculada,
                Email = $"orden.{organizacion.Id}@example.com",
                PasswordHash = "x",
                AccessChannel = "externo",
                IsActive = true,
                CreatedAt = creada,
                UpdatedAt = creada,
            };
            db.Users.Add(cuenta);
            await db.SaveChangesAsync();

            db.UserEntities.Add(new UserEntityRow
            {
                UserId = cuenta.Id,
                EntityId = organizacion.Id,
                EntityRole = "administrador",
                IsActive = true,
                CreatedAt = creada,
            });
        }

        for (var i = 0; i < festivales; i++)
        {
            db.FestivalRecords.Add(new FestivalRow
            {
                Name = $"Festival {i} de {nombre}",
                CoverageLevel = "nacional",
                StatusCode = "borrador",
                OrganizacionPrincipalId = organizacion.Id,
                CreatedAt = creada,
                UpdatedAt = creada,
            });
        }

        await db.SaveChangesAsync();
        return organizacion.Id;
    }

    /// <summary>
    /// Pide el listado con una cadena de consulta cruda.
    /// </summary>
    /// <remarks>
    /// LAS PRUEBAS COMPARTEN BASE, asi que toda llamada de orden lleva su propio <c>q</c> con una
    /// marca unica: sin el, lo sembrado por las demas pruebas entraria en la comparacion y el orden
    /// esperado dependeria de cuales corrieron antes.
    /// </remarks>
    private async Task<OrganizacionesRespuestaDto> ListarAsync(string consulta)
    {
        var respuesta = await _client.GetAsync("/api/v1/admin/organizaciones/?" + consulta);
        respuesta.EnsureSuccessStatusCode();
        var carga = await respuesta.Content.ReadFromJsonAsync<OrganizacionesRespuestaDto>();
        Assert.NotNull(carga);
        return carga!;
    }

    [Fact]
    public async Task El_Orden_Por_Fecha_De_Creacion_Pone_Primero_La_Mas_Antigua_Y_Al_Reves_Al_Reves()
    {
        // LOS NOMBRES VAN AL REVES DE LAS FECHAS A PROPOSITO. Si la mas antigua se llamara «Alfa»,
        // el orden por nombre —el de por omision— daria el mismo resultado y la prueba pasaria sin
        // que nadie hubiera ordenado por fecha.
        const string marca = "ordencreacion";
        await SembrarParaOrdenAsync("Zeta " + marca, new DateTime(2026, 1, 5, 8, 0, 0, DateTimeKind.Utc));
        await SembrarParaOrdenAsync("Alfa " + marca, new DateTime(2026, 3, 9, 8, 0, 0, DateTimeKind.Utc));
        await EntrarComoWebmasterAsync();

        var ascendente = await ListarAsync($"q={marca}&orden=creacion&direccion=asc");
        Assert.Equal(
            new[] { "Zeta " + marca, "Alfa " + marca },
            ascendente.Items.Select(item => item.Nombre).ToArray());
        Assert.Equal(AdminOrganizacionesEndpoints.OrdenCreacion, ascendente.Orden);
        Assert.Equal("asc", ascendente.Direccion);

        var descendente = await ListarAsync($"q={marca}&orden=creacion&direccion=desc");
        Assert.Equal(
            new[] { "Alfa " + marca, "Zeta " + marca },
            descendente.Items.Select(item => item.Nombre).ToArray());
        Assert.Equal("desc", descendente.Direccion);
    }

    [Fact]
    public async Task El_Orden_Por_Responsable_Coincide_Con_El_Nombre_Que_Se_Pinta()
    {
        // ESTA ES LA PRUEBA QUE ATA LAS DOS ESCRITURAS DE LA MISMA REGLA. El nombre que se pinta lo
        // resuelve ResolverResponsablesAsync en C#; el que ordena lo resuelve ClaveDeResponsable en
        // SQL. Son dos codigos distintos y el compilador no obliga a que digan lo mismo: si se
        // separan, la columna sale ordenada por un nombre que no es el que se lee.
        const string marca = "ordenresponsable";

        // La declarada lleva ADEMAS una cuenta vinculada cuyo nombre ordenaria al final. Si la
        // clave leyera la cuenta antes que la fila declarada, esta fila se iria al fondo.
        await SembrarParaOrdenAsync(
            "Uno " + marca,
            new DateTime(2026, 2, 1, 8, 0, 0, DateTimeKind.Utc),
            responsableDeclarado: "Aaa Persona Declarada",
            cuentaVinculada: "Zzz Cuenta Que No Ordena");
        await SembrarParaOrdenAsync(
            "Dos " + marca,
            new DateTime(2026, 2, 2, 8, 0, 0, DateTimeKind.Utc),
            cuentaVinculada: "Mmm Cuenta Vinculada");
        await SembrarParaOrdenAsync(
            "Tres " + marca,
            new DateTime(2026, 2, 3, 8, 0, 0, DateTimeKind.Utc));
        await EntrarComoWebmasterAsync();

        var carga = await ListarAsync($"q={marca}&orden=responsable&direccion=asc");
        var nombres = carga.Items.Select(item => item.Responsable?.Nombre ?? string.Empty).ToArray();

        Assert.Equal(3, nombres.Length);
        Assert.Equal(new[] { string.Empty, "Aaa Persona Declarada", "Mmm Cuenta Vinculada" }, nombres);

        var descendente = await ListarAsync($"q={marca}&orden=responsable&direccion=desc");
        Assert.Equal(
            new[] { "Mmm Cuenta Vinculada", "Aaa Persona Declarada", string.Empty },
            descendente.Items.Select(item => item.Responsable?.Nombre ?? string.Empty).ToArray());
    }

    [Fact]
    public async Task El_Orden_Por_Procesos_Coincide_Con_El_Total_Que_Se_Pinta()
    {
        // La otra mitad del mismo problema: ContarProcesosAsync cuenta seis tablas en C# y
        // ClaveDeProcesos las suma en SQL. Si una de las dos se deja una tabla, esto se pone rojo.
        const string marca = "ordenprocesos";
        await SembrarParaOrdenAsync("Alfa " + marca, new DateTime(2026, 4, 1, 8, 0, 0, DateTimeKind.Utc), festivales: 2);
        await SembrarParaOrdenAsync("Beta " + marca, new DateTime(2026, 4, 2, 8, 0, 0, DateTimeKind.Utc), festivales: 0);
        await SembrarParaOrdenAsync("Gama " + marca, new DateTime(2026, 4, 3, 8, 0, 0, DateTimeKind.Utc), festivales: 1);
        await EntrarComoWebmasterAsync();

        var ascendente = await ListarAsync($"q={marca}&orden=procesos&direccion=asc");
        Assert.Equal(TotalesAscendentes, ascendente.Items.Select(item => item.Procesos.Total).ToArray());
        Assert.Equal(
            new[] { "Beta " + marca, "Gama " + marca, "Alfa " + marca },
            ascendente.Items.Select(item => item.Nombre).ToArray());

        var descendente = await ListarAsync($"q={marca}&orden=procesos&direccion=desc");
        Assert.Equal(TotalesDescendentes, descendente.Items.Select(item => item.Procesos.Total).ToArray());
    }

    [Fact]
    public async Task El_Orden_Por_Territorio_Sigue_Lo_Que_Dice_La_Celda()
    {
        // La celda dice «Medellin, Antioquia»: se ordena por lo que se lee primero, el municipio.
        // Las que no tienen territorio quedan juntas al principio, no repartidas.
        const string marca = "ordenterritorio";
        await SembrarParaOrdenAsync("Uno " + marca, new DateTime(2026, 5, 1, 8, 0, 0, DateTimeKind.Utc), departamento: "05", municipio: "05001");
        await SembrarParaOrdenAsync("Dos " + marca, new DateTime(2026, 5, 2, 8, 0, 0, DateTimeKind.Utc), departamento: "11", municipio: "11001");
        await SembrarParaOrdenAsync("Tres " + marca, new DateTime(2026, 5, 3, 8, 0, 0, DateTimeKind.Utc));
        await EntrarComoWebmasterAsync();

        var carga = await ListarAsync($"q={marca}&orden=territorio&direccion=asc");

        Assert.Equal(
            new[] { string.Empty, "Bogota, D.C., Bogota, D.C.", "Medellin, Antioquia" },
            carga.Items.Select(item => item.Territorio).ToArray());
    }

    [Fact]
    public async Task Una_Columna_Que_No_Existe_Cae_Al_Orden_Por_Nombre_Y_La_Respuesta_Lo_Dice()
    {
        // No es un 400: un enlace guardado o un parametro mal escrito no puede dejar la consola sin
        // registro. Lo que no puede pasar es que la respuesta calle cual orden quedo puesto, porque
        // entonces la pantalla pinta la flechita sobre una cabecera que no ordeno nada.
        const string marca = "ordeninventado";
        await SembrarParaOrdenAsync("Zeta " + marca, new DateTime(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc));
        await SembrarParaOrdenAsync("Alfa " + marca, new DateTime(2026, 6, 2, 8, 0, 0, DateTimeKind.Utc));
        await EntrarComoWebmasterAsync();

        var carga = await ListarAsync($"q={marca}&orden=fecha_de_cumpleanos&direccion=asc");

        Assert.Equal(AdminOrganizacionesEndpoints.OrdenNombre, carga.Orden);
        Assert.Equal(
            new[] { "Alfa " + marca, "Zeta " + marca },
            carga.Items.Select(item => item.Nombre).ToArray());
    }

    [Fact]
    public async Task El_Filtro_De_Territorio_Separa_El_Departamento_Y_Tambien_Lo_Que_No_Tiene_Ninguno()
    {
        const string marca = "filtroterritorio";
        await SembrarParaOrdenAsync("Con " + marca, new DateTime(2026, 7, 1, 8, 0, 0, DateTimeKind.Utc), departamento: "05", municipio: "05001");
        await SembrarParaOrdenAsync("Sin " + marca, new DateTime(2026, 7, 2, 8, 0, 0, DateTimeKind.Utc));
        await EntrarComoWebmasterAsync();

        var enAntioquia = await ListarAsync($"q={marca}&departamento=05");
        Assert.Equal("Con " + marca, Assert.Single(enAntioquia.Items).Nombre);

        // EL CAJON DE LAS QUE NO TIENEN TERRITORIO ES LA MITAD QUE IMPORTA: sin el, esas filas solo
        // se alcanzan quitando el filtro, y no hay manera de pedir «enseñame las que faltan».
        var sinTerritorio = await ListarAsync($"q={marca}&departamento={OrganizacionTerritorioDto.SinTerritorio}");
        Assert.Equal("Sin " + marca, Assert.Single(sinTerritorio.Items).Nombre);
    }

    [Fact]
    public async Task Las_Cifras_Del_Desplegable_De_Territorio_No_Se_Cuentan_Sobre_Si_Mismas()
    {
        // Misma regla que las pestañas de estado: con Antioquia elegida, «Sin territorio» tiene que
        // seguir diciendo cuantas hay, o la opcion pareceria vacia y nadie volveria a pulsarla.
        const string marca = "facetaterritorio";
        await SembrarParaOrdenAsync("Con " + marca, new DateTime(2026, 8, 1, 8, 0, 0, DateTimeKind.Utc), departamento: "05", municipio: "05001");
        await SembrarParaOrdenAsync("Sin " + marca, new DateTime(2026, 8, 2, 8, 0, 0, DateTimeKind.Utc));
        await EntrarComoWebmasterAsync();

        var carga = await ListarAsync($"q={marca}&departamento=05");

        Assert.Single(carga.Items);
        Assert.Contains(carga.Territorios, t => t.Codigo == "05" && t.Etiqueta == "Antioquia" && t.Total == 1);
        Assert.Contains(carga.Territorios, t => t.Codigo == OrganizacionTerritorioDto.SinTerritorio && t.Total == 1);
    }

    [Fact]
    public async Task Las_Cifras_Por_Estado_Si_Se_Reducen_Al_Departamento_Elegido()
    {
        // La otra cara de la faceta, y va en direccion CONTRARIA a la prueba de mas arriba: el
        // estado no se cuenta sobre si mismo, pero SI sobre el territorio. Con Antioquia elegida,
        // una pestaña que prometiera «verificada 1» estaria prometiendo una fila que el filtro de
        // departamento ya descarto, y al pulsarla no saldria nada.
        const string marca = "facetaestado";
        await SembrarParaOrdenAsync("Con " + marca, new DateTime(2026, 8, 3, 8, 0, 0, DateTimeKind.Utc), estado: "activa", departamento: "05", municipio: "05001");
        await SembrarParaOrdenAsync("Sin " + marca, new DateTime(2026, 8, 4, 8, 0, 0, DateTimeKind.Utc), estado: "inactiva");
        await EntrarComoWebmasterAsync();

        var carga = await ListarAsync($"q={marca}&departamento=05");

        Assert.Contains(carga.Estados, e => e.Id == "activa" && e.Total == 1);
        Assert.DoesNotContain(carga.Estados, e => e.Id == "inactiva");
    }

    [Fact]
    public async Task Los_Departamentos_Del_Desplegable_Van_En_Orden_Alfabetico_Español()
    {
        // CORDOBA ANTES QUE CUNDINAMARCA. Comparando numeros de caracter, la «Ó» vale 0xD3 y la «U»
        // vale 0x55, asi que Cordoba caeria despues. Ocho de los treinta y tres departamentos del
        // pais llevan tilde: el desplegable saldria barajado justo donde nadie lo esperaria.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
            if (!await db.DivipolaLocations.AnyAsync(fila => fila.DepartmentCode == "23"))
            {
                db.DivipolaLocations.Add(new DivipolaLocationRow
                {
                    DepartmentCode = "23",
                    DepartmentName = "Córdoba",
                    MunicipalityCode = "23001",
                    MunicipalityName = "Montería",
                    LocationType = "MUNICIPALITY",
                });
            }

            if (!await db.DivipolaLocations.AnyAsync(fila => fila.DepartmentCode == "25"))
            {
                db.DivipolaLocations.Add(new DivipolaLocationRow
                {
                    DepartmentCode = "25",
                    DepartmentName = "Cundinamarca",
                    MunicipalityCode = "25754",
                    MunicipalityName = "Soacha",
                    LocationType = "MUNICIPALITY",
                });
            }

            await db.SaveChangesAsync();
        }

        const string marca = "ordendepartamentos";
        await SembrarParaOrdenAsync("Uno " + marca, new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc), departamento: "25", municipio: "25754");
        await SembrarParaOrdenAsync("Dos " + marca, new DateTime(2026, 9, 2, 8, 0, 0, DateTimeKind.Utc), departamento: "23", municipio: "23001");
        await EntrarComoWebmasterAsync();

        var carga = await ListarAsync($"q={marca}");

        Assert.Equal(
            DepartamentosEnOrden,
            carga.Territorios.Select(territorio => territorio.Etiqueta).ToArray());
    }

    /// <summary>
    /// La ficha de una organización trae CUÁLES procesos administra -no solo cuántos-, y las
    /// solicitudes y reclamaciones que la involucran, con el nombre real del Festival y no solo su
    /// identificador. El criterio es este: «consultar procesos
    /// administrados; revisar solicitudes; revisar vinculaciones».
    /// </summary>
    [Fact]
    public async Task La_Ficha_De_Una_Organizacion_Trae_Sus_Procesos_Solicitudes_Y_Reclamaciones_Con_Nombre()
    {
        var organizacionId = await SembrarConResponsableDeclaradoAsync("fichacompleta");

        int festivalId;
        int otraOrganizacionId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
            var ahora = DateTime.UtcNow;

            festivalId = await db.FestivalRecords.AsNoTracking()
                .Where(item => item.OrganizacionPrincipalId == organizacionId)
                .Select(item => item.Id)
                .SingleAsync();

            db.RecordLinkRequests.Add(new RecordLinkRequestRow
            {
                ModuloId = "festivals",
                RecordId = festivalId.ToString(CultureInfo.InvariantCulture),
                RequestingUserId = 1,
                EntidadId = organizacionId,
                RequestedScope = "responsable",
                Reason = "Prueba de la ficha completa de la organización.",
                Status = "pendiente",
                CreatedAt = ahora,
                UpdatedAt = ahora,
            });

            var otraOrganizacion = new EntityProfileRow
            {
                EntityType = "organizacion",
                Name = "Organizacion Reclamante Ficha Completa",
                CoverageLevel = "sin_definir",
                StatusCode = "activa",
                IsActive = true,
                CreatedByUserId = 1,
                CreatedAt = ahora,
                UpdatedAt = ahora,
            };
            db.EntityProfiles.Add(otraOrganizacion);
            await db.SaveChangesAsync();
            otraOrganizacionId = otraOrganizacion.Id;

            db.AdministrationClaims.Add(new AdministrationClaimRow
            {
                ModuloId = "festival",
                CanonicalRecordId = festivalId.ToString(CultureInfo.InvariantCulture),
                RequestingOrganizationId = otraOrganizacionId,
                RequestingPersonId = 1,
                Status = "enviada",
                Justification = "Prueba de la ficha completa de la organización.",
                CreatedAt = ahora,
                SubmittedAt = ahora,
                UpdatedAt = ahora,
                Version = 1,
            });

            await db.SaveChangesAsync();
        }

        await EntrarComoWebmasterAsync();
        var respuesta = await _client.GetAsync($"/api/v1/admin/organizaciones/{organizacionId}");
        respuesta.EnsureSuccessStatusCode();
        var ficha = await respuesta.Content.ReadFromJsonAsync<OrganizacionFichaDto>();
        Assert.NotNull(ficha);

        // MUTANTE QUE MATA: devolver solo el CONTEO de Festivales -que ya trae
        // `ficha.Organizacion.Procesos.Festivales`- en vez de la lista con nombre. Sin el nombre,
        // «consultar procesos administrados» sigue sin poder decir CUÁL Festival es.
        Assert.Contains(ficha!.Festivales, item => item.Id == festivalId.ToString(CultureInfo.InvariantCulture) && item.Nombre == "Festival De La Declarada fichacompleta");

        var solicitud = Assert.Single(ficha.Solicitudes);
        Assert.Equal("Festival De La Declarada fichacompleta", solicitud.RecordName);

        // LA RECLAMACIÓN SALE EN LA FICHA DE QUIEN LA PIDIÓ, no en la del Festival reclamado: es
        // «otraOrganizacion» quien la presentó -`RequestingOrganizationId`-, así que es su propia
        // ficha la que tiene que traerla.
        var fichaReclamante = await _client.GetFromJsonAsync<OrganizacionFichaDto>($"/api/v1/admin/organizaciones/{otraOrganizacionId}");
        var reclamacion = Assert.Single(fichaReclamante!.Reclamaciones);
        Assert.Equal("Festival De La Declarada fichacompleta", reclamacion.RegistroNombre);
        Assert.Equal(otraOrganizacionId.ToString(CultureInfo.InvariantCulture), reclamacion.OrganizacionSolicitanteId);
    }

    [Fact]
    public async Task Ficha_De_Organizacion_Inexistente_Responde_404()
    {
        await EntrarComoWebmasterAsync();
        var respuesta = await _client.GetAsync("/api/v1/admin/organizaciones/999999999");
        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task Sin_Sesion_Institucional_El_Listado_No_Se_Lee()
    {
        // Trae correos, telefonos y el nombre de personas naturales: es exactamente el tipo de dato
        // que no puede quedar detras de una ruta anonima por descuido.
        var anonimo = _factory.CreateClient();
        var respuesta = await anonimo.GetAsync("/api/v1/admin/organizaciones/");

        Assert.True(
            respuesta.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
            $"Se esperaba 401 o 403 y llego {(int)respuesta.StatusCode}.");
    }
}
