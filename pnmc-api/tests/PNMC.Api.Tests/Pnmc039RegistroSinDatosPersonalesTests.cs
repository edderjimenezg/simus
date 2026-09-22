using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PNMC.Api.Observability;
using PNMC.Contracts;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// PNMC-039. El registro del API no puede llevar datos personales en claro.
/// <para>
/// LA REGLA, VERIFICADO ANTES DE TOCAR NADA: un barrido de los 30 ficheros de
/// <c>Endpoints/</c> no encontro ni una sentencia de registro de negocio, ni una metrica, ni una
/// traza. Hoy eso significa que no se puede depurar. El dia del lanzamiento significa que no se
/// puede responder a nadie: ya paso esta misma semana, cuando para archivar un expediente hubo
/// que deducir la exposicion de datos leyendo <c>git log</c> porque no habia un solo registro que
/// consultar.
/// </para>
/// <para>
/// POR QUE ESTE FICHERO Y NO UNA BATERIA DE "SE REGISTRA X". El riesgo de anadir registro a este
/// proyecto no es olvidarse de registrar algo: es registrar de mas. Hay siete rutas anonimas
/// sirviendo datos de contacto y una octava con la nomina del equipo. La primera persona que
/// escriba <c>LogInformation("login de {Correo}", request.Email)</c> para depurar un martes crea
/// una base de datos de correos de ciudadanos en el fichero de texto del contenedor — sin control
/// de acceso, sin retencion, sin nadie respondiendo por ella— y nadie se entera, porque nada se
/// pone rojo.
/// </para>
/// <para>
/// LO QUE SE FIJA AQUI ES LA PUERTA, NO LA BUENA EDUCACION DE LOS MANEJADORES DE HOY. Una prueba
/// que solo comprobara que los tres endpoints instrumentados se portan bien seguiria pasando
/// cuando el cuarto se portara mal. Por eso las dos pruebas centrales
/// —<see cref="La_puerta_rechaza_una_linea_que_hoy_se_escribiria_entera"/> y
/// <see cref="Un_correo_registrado_en_crudo_bajo_una_clave_inocente_no_llega_a_la_salida"/>—
/// registran un correo POR EL CAMINO QUE UN DESCUIDADO USARIA, sin llamar a ningun enmascarador
/// y bajo un nombre de propiedad deliberadamente inocente, y comprueban que aun asi no sale.
/// </para>
/// <para>
/// Y NO SON VACIAS. "El correo no aparece en el registro" lo cumple tambien un sistema que no
/// registra nada, que es exactamente el sistema que teniamos ayer. Por eso cada prueba de fuga
/// afirma ademas que la linea EXISTE, que lleva el identificador de correlacion de la peticion y
/// que lleva la marca <c>[correo:#...]</c> — o sea que el dato paso por ahi y fue transformado,
/// no que nadie lo miro.
/// </para>
/// </summary>
public sealed class Pnmc039RegistroSinDatosPersonalesTests
{
    private const string CorreoDeCiudadanoFicticio = "sara.quintero.paez@correo-inventado.example";

    // ---------------------------------------------------------------------------------------
    // 1. La puerta, aislada: se demuestra que rechaza algo que hoy se escribiria entero.
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// La demostracion mas corta de que esto es una puerta y no un adorno: se construye la linea
    /// TAL Y COMO la escribiria el proveedor de consola de serie —el que estaba puesto hasta este
    /// parche—, se comprueba que efectivamente lleva el correo dentro, y se comprueba que despues
    /// del saneado ya no. Sin el parche, la primera afirmacion pasa y la segunda falla.
    /// </summary>
    [Fact]
    public void La_puerta_rechaza_una_linea_que_hoy_se_escribiria_entera()
    {
        var lineaSinSanear =
            "{\"nivel\":\"Information\",\"mensaje\":\"login de " + CorreoDeCiudadanoFicticio + "\"}";

        // Antes: esto es literalmente lo que habria acabado en el fichero del contenedor.
        Assert.Contains(CorreoDeCiudadanoFicticio, lineaSinSanear, StringComparison.Ordinal);

        var saneada = RedaccionDatosPersonales.Sanear(lineaSinSanear);

        // Despues: el dato ya no esta, pero la linea sigue siendo util.
        Assert.DoesNotContain(CorreoDeCiudadanoFicticio, saneada, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sara.quintero.paez", saneada, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(RedaccionDatosPersonales.MarcaCorreo, saneada, StringComparison.Ordinal);
        Assert.Contains("login de", saneada, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------
    // 2. La puerta enchufada: una peticion real, contra el API real, con un correo dentro.
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// EL ENTREGABLE PRINCIPAL. Una peticion HTTP de verdad que lleva un correo en el cuerpo,
    /// contra el API montado entero, leyendo lo que el API ESCRIBIO — no lo que un enmascarador
    /// devuelve cuando se le llama a mano en una prueba unitaria que pasaria en verde aunque
    /// nadie lo hubiera enchufado.
    /// </summary>
    [Fact]
    public async Task Una_peticion_con_un_correo_en_el_cuerpo_no_deja_el_correo_en_el_registro()
    {
        var capturador = new DestinoDeCaptura();
        using var baseFactory = new TestWebApplicationFactory();
        using var factory = ConSalidaCapturada(baseFactory, capturador);
        var cliente = factory.CreateClient();

        var respuesta = await cliente.PostAsJsonAsync("/api/v1/admin/auth/login", new AdminLoginRequest
        {
            Email = CorreoDeCiudadanoFicticio,
            Password = "una-contrasena-que-no-corresponde-a-nadie"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);

        var lineasDeAutenticacion = capturador.LineasDe("PNMC.Autenticacion");

        // (a) NO ES VACIA: el intento dejo rastro. Sin esto, la prueba la aprobaria tambien un
        //     API que no registra absolutamente nada, que es el de ayer.
        Assert.NotEmpty(lineasDeAutenticacion);

        // (b) EL RASTRO SIRVE: lleva el identificador de correlacion que se le devolvio a quien
        //     llamo, asi que desde la queja de una persona se puede llegar a su linea.
        var correlacion = Assert.Single(respuesta.Headers.GetValues("X-Correlation-ID"));
        Assert.Contains(lineasDeAutenticacion, linea => linea.Contains(correlacion, StringComparison.Ordinal));

        // (c) EL DATO PASO POR AHI Y FUE TRANSFORMADO, no simplemente omitido: la linea lleva el
        //     seudonimo del correo, que es lo que permite agrupar catorce intentos de la misma
        //     cuenta sin saber de quien es.
        Assert.Contains(
            lineasDeAutenticacion,
            linea => linea.Contains(RedaccionDatosPersonales.MarcaCorreo, StringComparison.Ordinal));

        // (d) Y AUN ASI EL CORREO NO ESTA. En ninguna linea, ni siquiera su parte local.
        Assert.DoesNotContain(CorreoDeCiudadanoFicticio, capturador.Todo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sara.quintero.paez", capturador.Todo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("correo-inventado.example", capturador.Todo, StringComparison.OrdinalIgnoreCase);

        // (e) Y LA CONTRASENA TAMPOCO, que es el otro dato que viajaba en ese cuerpo.
        Assert.DoesNotContain("una-contrasena-que-no-corresponde-a-nadie", capturador.Todo, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// LA PRUEBA QUE DEMUESTRA QUE LA PUERTA NO DEPENDE DE LA DISCIPLINA DE NADIE. Aqui no se
    /// llama a ningun enmascarador y la propiedad se llama <c>{Cualquiera}</c> a proposito: es
    /// una clave que la capa de nombres NO reconoce como sensible. Si esto pasa, es unicamente
    /// porque el saneado por forma corre en el ultimo punto por el que pasa toda linea. Es el
    /// caso del programador con prisa, que es el unico que importa.
    /// </summary>
    [Fact]
    public void Un_correo_registrado_en_crudo_bajo_una_clave_inocente_no_llega_a_la_salida()
    {
        const string correo = "descuidado.a.las.once@ejemplo-inventado.test";

        var capturador = new DestinoDeCaptura();
        using var baseFactory = new TestWebApplicationFactory();
        using var factory = ConSalidaCapturada(baseFactory, capturador);
        _ = factory.CreateClient();

        var registro = factory.Services.GetRequiredService<ILoggerFactory>().CreateLogger("PNMC.PruebaDeDescuido");
        if (registro.IsEnabled(LogLevel.Information))
        {
            registro.LogInformation("El ciudadano {Cualquiera} pidio informacion", correo);
        }

        var lineas = capturador.LineasDe("PNMC.PruebaDeDescuido");

        Assert.NotEmpty(lineas);
        Assert.Contains(lineas, linea => linea.Contains(RedaccionDatosPersonales.MarcaCorreo, StringComparison.Ordinal));
        Assert.DoesNotContain(correo, capturador.Todo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("descuidado.a.las.once", capturador.Todo, StringComparison.OrdinalIgnoreCase);

        // El resto de la linea sobrevive: enmascarar no es borrar el registro.
        Assert.Contains(lineas, linea => linea.Contains("pidio informacion", StringComparison.Ordinal));
    }

    /// <summary>
    /// La segunda capa, la de nombre de campo. Cubre lo que el saneado por forma no puede cubrir
    /// sin destrozar el registro: una cedula y un telefono escritos de corrido son, como texto,
    /// indistinguibles de un identificador de fila cualquiera. Aqui se atrapan porque la
    /// propiedad se llama como se llama.
    /// </summary>
    [Fact]
    public void Los_campos_que_se_llaman_como_un_dato_personal_salen_enmascarados()
    {
        const string cedula = "1090234567";
        const string telefono = "3014567890";
        const string contrasena = "esta-es-una-contrasena-de-verdad";
        const string nombre = "Sara Quintero Paez";

        var capturador = new DestinoDeCaptura();
        using var baseFactory = new TestWebApplicationFactory();
        using var factory = ConSalidaCapturada(baseFactory, capturador);
        _ = factory.CreateClient();

        var registro = factory.Services.GetRequiredService<ILoggerFactory>().CreateLogger("PNMC.PruebaDeClaves");
        if (registro.IsEnabled(LogLevel.Information))
        {
            registro.LogInformation(
                "Solicitud recibida {Documento} {Telefono} {Password} {NombreCompleto} {Expediente}",
                cedula,
                telefono,
                contrasena,
                nombre,
                42);
        }

        var lineas = capturador.LineasDe("PNMC.PruebaDeClaves");
        Assert.NotEmpty(lineas);
        var linea = lineas[0];

        Assert.DoesNotContain(cedula, linea, StringComparison.Ordinal);
        Assert.DoesNotContain(telefono, linea, StringComparison.Ordinal);
        Assert.DoesNotContain(contrasena, linea, StringComparison.Ordinal);
        Assert.DoesNotContain(nombre, linea, StringComparison.Ordinal);

        Assert.Contains(RedaccionDatosPersonales.MarcaDocumento, linea, StringComparison.Ordinal);
        Assert.Contains(RedaccionDatosPersonales.MarcaTelefono, linea, StringComparison.Ordinal);
        Assert.Contains(RedaccionDatosPersonales.MarcaSecreto, linea, StringComparison.Ordinal);
        Assert.Contains(RedaccionDatosPersonales.MarcaPersona, linea, StringComparison.Ordinal);

        // Lo que NO es dato personal sigue ahi. Una puerta que enmascara todo deja el registro
        // inservible, que es otra forma de no tener registro.
        Assert.Contains("\"Expediente\":\"42\"", linea, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------
    // 3. Los tres endpoints instrumentados como patron.
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// LECTURA PUBLICA. La regla mas dura de la guia: de una ruta anonima se registra CUANTO se
    /// sirvio, nunca QUE se sirvio. Esta ruta devuelve <c>CorreoContacto</c>; si alguien anadiera
    /// el cuerpo de la respuesta al registro "solo para depurar", esta prueba se pondria roja.
    /// </summary>
    [Fact]
    public async Task La_lectura_publica_registra_cuantas_fichas_sirvio_y_ningun_dato_de_ellas()
    {
        var capturador = new DestinoDeCaptura();
        using var baseFactory = new TestWebApplicationFactory();
        using var factory = ConSalidaCapturada(baseFactory, capturador);
        var cliente = factory.CreateClient();

        var respuesta = await cliente.GetAsync("/api/v1/publico/festivales");
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var lineas = capturador.LineasDe("PNMC.LecturaPublica");

        Assert.NotEmpty(lineas);
        Assert.Contains(lineas, linea => linea.Contains("\"Devueltas\"", StringComparison.Ordinal));
        // EL CONTADOR CAMBIO DE NOMBRE EL 30 DE AGOSTO DE 2026 y sigue midiendo lo mismo: cuantas
        // de las fichas servidas traen algo mas que un nombre. Antes se contaban las que llevaban
        // correo; desde que la lectura publica no sirve datos personales, se cuentan las que
        // publican algun enlace propio. La regla que esta prueba defiende no se toco: se registra
        // CUANTO se sirvio, jamas QUE se sirvio, y por eso la asercion de la arroba sigue abajo.
        Assert.Contains(lineas, linea => linea.Contains("\"FichasConEnlacePublico\"", StringComparison.Ordinal));
        Assert.Contains(lineas, linea => linea.Contains("\"DuracionMs\"", StringComparison.Ordinal));

        // Ninguna linea de lectura publica puede contener una arroba: si la contiene, alguien
        // esta volcando datos de contacto de gente que no pidio salir en un registro.
        Assert.All(lineas, linea => Assert.DoesNotContain("@", linea, StringComparison.Ordinal));
    }

    /// <summary>
    /// ESCRITURA ADMINISTRATIVA. El nombre de quien edita esta en el ClaimsPrincipal y el
    /// manejador lo registra: sale seudonimizado. Se ejercita por el camino del rechazo
    /// antiforgery, que ademas cubre el otro medio hallazgo — que hasta hoy un 400 de seguridad
    /// no dejaba ni una linea, asi que nadie podia saber que a alguien le estaba rebotando.
    /// </summary>
    [Fact]
    public async Task La_escritura_administrativa_registra_quien_edito_sin_escribir_su_nombre()
    {
        var capturador = new DestinoDeCaptura();
        using var baseFactory = new TestWebApplicationFactory();
        using var factory = ConSalidaCapturada(baseFactory, capturador);
        var cliente = factory.CreateClient();

        var acceso = await cliente.PostAsJsonAsync("/api/v1/admin/auth/login", new AdminLoginRequest
        {
            Email = CmsTestClient.WebmasterEmail,
            Password = CmsTestClient.WebmasterPassword
        });
        acceso.EnsureSuccessStatusCode();

        // A proposito SIN la cabecera antiforgery: el manejador corre, la guarda lo rechaza y
        // ese rechazo tiene que quedar registrado.
        var guardado = await cliente.PostAsJsonAsync(
            "/api/v1/admin/equipo-web",
            new { members = (object?)null, publish = false, version = 1 });

        Assert.Equal(HttpStatusCode.BadRequest, guardado.StatusCode);

        var lineas = capturador.LineasDe("PNMC.EscrituraAdministrativa");

        Assert.NotEmpty(lineas);
        Assert.Contains(lineas, linea => linea.Contains("antiforgery_invalido", StringComparison.Ordinal));
        Assert.Contains(
            lineas,
            linea => linea.Contains(RedaccionDatosPersonales.MarcaPersona, StringComparison.Ordinal));

        // El nombre real del webmaster que sembro TestWebApplicationFactory no aparece en ningun
        // sitio de la salida, ni siquiera en la linea que habla de el.
        Assert.DoesNotContain("Usuario Prueba", capturador.Todo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(CmsTestClient.WebmasterEmail, capturador.Todo, StringComparison.OrdinalIgnoreCase);
    }

    // ---------------------------------------------------------------------------------------
    // 4. Que la puerta no muerda lo que no debe. Una puerta que rompe el registro se acaba
    //    desactivando, y entonces deja de ser una puerta.
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// FRONTERA DELIBERADA. El patron de telefono exige separador o prefijo +57 justamente para
    /// no morder un identificador de correlacion: un GUID en hexadecimal de 32 caracteres puede
    /// empezar o acabar por diez digitos con un 3 delante, y un patron ingenuo lo habria
    /// destrozado en aproximadamente una peticion de cada quinientas — de forma intermitente, que
    /// es la peor manera de romper algo. Estas mil iteraciones fijan esa frontera: si alguien
    /// ensancha el patron, esto se pone rojo antes de que se ponga rojo el turno de guardia.
    /// </summary>
    [Fact]
    public void El_saneado_no_toca_un_identificador_de_correlacion()
    {
        for (var intento = 0; intento < 1000; intento++)
        {
            var identificador = Guid.NewGuid().ToString("N");
            Assert.Equal(identificador, RedaccionDatosPersonales.Sanear(identificador));
        }
    }

    /// <summary>
    /// Un contador no es un dato personal aunque se llame como uno. Ver la nota de
    /// <c>RedaccionDatosPersonales.Enmascarar</c>: los numeros solo se enmascaran donde el numero
    /// puede ser de verdad el dato.
    /// </summary>
    [Fact]
    public void Un_contador_no_se_convierte_en_seudonimo_por_llamarse_como_un_dato_personal()
    {
        Assert.Equal("7", RedaccionDatosPersonales.Enmascarar("CorreosServidos", 7));
        Assert.Equal("0", RedaccionDatosPersonales.Enmascarar("FichasConContacto", 0));
        Assert.Equal("Festival de la Guabina", RedaccionDatosPersonales.Enmascarar("NombreFestival", "Festival de la Guabina"));
        Assert.Equal("/api/v1/publico/festivales", RedaccionDatosPersonales.Enmascarar("Path", "/api/v1/publico/festivales"));
    }

    /// <summary>
    /// El seudonimo tiene que servir para agrupar, que es lo unico que se le pide: dos apariciones
    /// del mismo correo en la misma ejecucion coinciden, dos correos distintos no. Sin esta
    /// propiedad, investigar "los catorce intentos de las 03:00" seria imposible y mas valdria
    /// borrar el campo.
    /// </summary>
    [Fact]
    public void El_seudonimo_agrupa_a_la_misma_cuenta_y_separa_a_dos_distintas()
    {
        var unaVez = RedaccionDatosPersonales.Correo("Ana.Ruiz@ejemplo-inventado.test");
        var otraVez = RedaccionDatosPersonales.Correo("  ana.ruiz@EJEMPLO-INVENTADO.TEST  ");
        var otraPersona = RedaccionDatosPersonales.Correo("beto.ruiz@ejemplo-inventado.test");

        Assert.Equal(unaVez, otraVez);
        Assert.NotEqual(unaVez, otraPersona);
        Assert.StartsWith(RedaccionDatosPersonales.MarcaCorreo, unaVez);
        Assert.DoesNotContain("ana.ruiz", unaVez!, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Un valor que el manejador ya seudonimizo no se vuelve a seudonimizar al pasar por la capa
    /// de nombres. Sin esto, la huella cambiaria segun por donde entrara el dato y dos lineas
    /// sobre la misma persona dejarian de coincidir.
    /// </summary>
    [Fact]
    public void Un_valor_ya_enmascarado_no_se_enmascara_dos_veces()
    {
        var seudonimo = RedaccionDatosPersonales.Correo("ana.ruiz@ejemplo-inventado.test");
        Assert.Equal(seudonimo, RedaccionDatosPersonales.Enmascarar("Correo", seudonimo));
    }

    // ---------------------------------------------------------------------------------------
    // Utilidades
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Sustituye el destino del registro por uno que se puede leer. Se hace con
    /// <c>WithWebHostBuilder</c> para no tocar <c>TestWebApplicationFactory</c>, que comparten
    /// las demas pruebas de la suite.
    /// </summary>
    private static WebApplicationFactory<Program> ConSalidaCapturada(
        TestWebApplicationFactory baseFactory,
        DestinoDeCaptura capturador)
    {
        return baseFactory.WithWebHostBuilder(constructor =>
            constructor.ConfigureServices(servicios =>
                servicios.AddSingleton<IDestinoRegistro>(capturador)));
    }

    /// <summary>
    /// Recoge lo que el API escribio de verdad. Es el punto entero de que
    /// <see cref="IDestinoRegistro"/> exista: sin el, estas pruebas comprobarian el enmascarador
    /// aislado y pasarian en verde aunque nadie lo hubiera conectado al registrador.
    /// </summary>
    private sealed class DestinoDeCaptura : IDestinoRegistro
    {
        private readonly List<string> _lineas = [];

        public void Escribir(string linea)
        {
            lock (_lineas)
            {
                _lineas.Add(linea);
            }
        }

        public IReadOnlyList<string> Lineas
        {
            get
            {
                lock (_lineas)
                {
                    return _lineas.ToArray();
                }
            }
        }

        public string Todo => string.Join("\n", Lineas);

        /// <summary>Las lineas de una categoria concreta, para que las afirmaciones de no
        /// vacuidad no las cumpla por accidente una linea de arranque de otro componente.</summary>
        public List<string> LineasDe(string categoria)
        {
            var marca = "\"categoria\":\"" + categoria + "\"";
            var seleccion = new List<string>();
            foreach (var linea in Lineas)
            {
                if (linea.Contains(marca, StringComparison.Ordinal))
                {
                    seleccion.Add(linea);
                }
            }

            return seleccion;
        }
    }
}
