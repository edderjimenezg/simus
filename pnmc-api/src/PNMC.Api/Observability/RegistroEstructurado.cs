using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace PNMC.Api.Observability;

/// <summary>
/// A donde va escrita cada linea de registro. Existe como interfaz por una sola razon: para que
/// una prueba pueda leer lo que el API escribio de verdad, en vez de comprobar que el
/// enmascarador funciona aislado en una prueba unitaria que no demuestra que este enchufado.
/// </summary>
public interface IDestinoRegistro
{
    void Escribir(string linea);
}

/// <summary>Destino de produccion: una linea JSON por evento en la salida estandar.</summary>
public sealed class DestinoConsola : IDestinoRegistro
{
    public void Escribir(string linea) => Console.Out.WriteLine(linea);
}

/// <summary>
/// PNMC-039. Proveedor de registro estructurado del API.
/// <para>
/// POR QUE SUSTITUYE A LOS PROVEEDORES POR DEFECTO Y NO SE SUMA A ELLOS. En
/// <c>Program.cs</c> se llama a <c>ClearProviders()</c> antes de registrar este. Es deliberado:
/// si el proveedor de consola de serie siguiera activo, escribiria la misma linea SIN sanear al
/// mismo fichero, y el enmascarado seria decorativo — el correo estaria en el registro igual,
/// una linea mas abajo. Una puerta con una segunda puerta abierta al lado no es una puerta.
/// </para>
/// <para>
/// EFECTO SECUNDARIO QUE HAY QUE CONOCER: la salida del API deja de ser texto legible con
/// colores y pasa a ser una linea JSON por evento. Los mensajes de arranque de Kestrel siguen
/// estando, dentro del campo <c>mensaje</c>.
/// </para>
/// </summary>
[ProviderAlias("PnmcEstructurado")]
public sealed class ProveedorRegistroEstructurado : ILoggerProvider, ISupportExternalScope
{
    private readonly ConcurrentDictionary<string, RegistradorEstructurado> _registradores =
        new(StringComparer.Ordinal);

    private readonly IDestinoRegistro _destino;
    private IExternalScopeProvider? _ambitos;

    public ProveedorRegistroEstructurado(IDestinoRegistro destino)
    {
        _destino = destino;
    }

    internal IDestinoRegistro Destino => _destino;

    internal IExternalScopeProvider? Ambitos => _ambitos;

    public ILogger CreateLogger(string categoryName) =>
        _registradores.GetOrAdd(categoryName, nombre => new RegistradorEstructurado(nombre, this));

    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _ambitos = scopeProvider;

    public void Dispose() => _registradores.Clear();
}

/// <summary>
/// Serializa un evento a una linea JSON y la pasa por
/// <see cref="RedaccionDatosPersonales.Sanear(string)"/> antes de escribirla.
/// <para>
/// EL ORDEN ES LO IMPORTANTE. Primero se enmascara campo a campo por nombre; despues se
/// reescribe el mensaje ya formateado sustituyendo cada valor bruto por su version enmascarada
/// —sin esto, <c>"guardado por {NombreCompleto}"</c> dejaria el nombre dentro del texto del
/// mensaje aunque el campo saliera seudonimizado—; y al final se sanea la linea entera por
/// forma, que es lo que atrapa lo que nadie clasifico. La linea que se escribe es la de despues
/// de las tres pasadas: no hay ningun camino que llegue al destino sin pasar por ellas.
/// </para>
/// <para>
/// UN REGISTRADOR NO PUEDE TUMBAR EL API. El cuerpo va envuelto en try/catch: si serializar o
/// sanear fallara, se escribe una linea de diagnostico sin ningun valor de estado en vez de
/// propagar la excepcion al manejador que estaba atendiendo a alguien.
/// </para>
/// </summary>
internal sealed class RegistradorEstructurado : ILogger
{
    private static readonly JsonSerializerOptions Formato = new()
    {
        // Sin esto, cada tilde del castellano sale como \u00XX y el registro deja de ser legible
        // para la persona que lo esta leyendo a las tres de la manana. No abre ningun agujero de
        // inyeccion: JSON escapa los saltos de linea pase lo que pase, asi que un valor con
        // "\n" no puede fabricar una segunda linea de registro falsa.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false
    };

    private readonly string _categoria;
    private readonly ProveedorRegistroEstructurado _proveedor;

    public RegistradorEstructurado(string categoria, ProveedorRegistroEstructurado proveedor)
    {
        _categoria = categoria;
        _proveedor = proveedor;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull =>
        _proveedor.Ambitos?.Push(state) ?? AlcanceVacio.Instancia;

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        try
        {
            var contexto = new ContextoLinea();

            // El orden de insercion es el orden de las claves en la linea. Se ponen primero las
            // cuatro que se leen de un vistazo cuando se esta buscando algo.
            contexto.Campos["marca"] = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            contexto.Campos["nivel"] = logLevel.ToString();
            contexto.Campos["categoria"] = _categoria;
            contexto.Campos["correlacion"] = null;
            contexto.Campos["mensaje"] = null;

            if (eventId.Id != 0 || !string.IsNullOrEmpty(eventId.Name))
            {
                contexto.Campos["evento"] = eventId.Name ?? eventId.Id.ToString(CultureInfo.InvariantCulture);
            }

            // Los ambitos primero: de ahi salen correlacion, ruta y metodo, que los pone
            // RequestContextMiddleware. Si el manejador declara una propiedad con el mismo
            // nombre, la suya gana, que es lo esperable.
            var ambitos = _proveedor.Ambitos;
            if (ambitos is not null)
            {
                ambitos.ForEachScope<ContextoLinea>(
                    (alcance, acumulado) => VolcarPares(alcance, acumulado),
                    contexto);
            }

            VolcarPares(state, contexto);

            contexto.Campos["mensaje"] = ReescribirMensaje(formatter(state, exception), contexto);

            if (exception is not null)
            {
                contexto.Campos["excepcion"] = exception.GetType().FullName;
                contexto.Campos["excepcionMensaje"] = exception.Message;
                // El detalle completo —traza y excepciones internas— es lo que hace
                // diagnosticable un 500 en produccion, donde no hay depurador. Hasta el
                // 23 ago 2026 este proveedor lo descartaba: era una regresion respecto al
                // de serie, introducida al enmascarar datos personales. El enmascarado
                // se aplica igual, porque la linea entera pasa por Sanear mas abajo.
                contexto.Campos["excepcionDetalle"] = exception.ToString();
            }

            var linea = RedaccionDatosPersonales.Sanear(JsonSerializer.Serialize(contexto.Campos, Formato));
            _proveedor.Destino.Escribir(linea);
        }
        catch (Exception fallo)
        {
            // Sin un solo valor de estado: si el fallo fue al serializarlos, volcarlos aqui seria
            // exactamente la fuga que este fichero existe para evitar.
            _proveedor.Destino.Escribir(
                "{\"nivel\":\"Error\",\"categoria\":\"PNMC.Observabilidad\",\"mensaje\":\"No se pudo escribir una linea de registro\",\"excepcion\":\""
                + fallo.GetType().Name
                + "\"}");
        }
    }

    /// <summary>
    /// Vuelca las propiedades de un estado o de un ambito, ya enmascaradas, y anota que hay que
    /// sustituir en el mensaje.
    /// </summary>
    private static void VolcarPares(object? estado, ContextoLinea contexto)
    {
        if (estado is not IEnumerable<KeyValuePair<string, object?>> pares)
        {
            return;
        }

        foreach (var par in pares)
        {
            if (string.Equals(par.Key, "{OriginalFormat}", StringComparison.Ordinal))
            {
                continue;
            }

            var bruto = par.Value?.ToString();
            var limpio = RedaccionDatosPersonales.Enmascarar(par.Key, par.Value);
            contexto.Campos[NombreCampo(par.Key)] = limpio;

            // El umbral de tres caracteres evita que enmascarar un valor cortisimo dispare
            // sustituciones por todo el mensaje.
            if (bruto is not null
                && limpio is not null
                && bruto.Length >= 3
                && !string.Equals(bruto, limpio, StringComparison.Ordinal))
            {
                contexto.Reemplazos[bruto] = limpio;
            }
        }
    }

    private static string ReescribirMensaje(string mensaje, ContextoLinea contexto)
    {
        if (string.IsNullOrEmpty(mensaje) || contexto.Reemplazos.Count == 0)
        {
            return mensaje;
        }

        var salida = mensaje;
        foreach (var reemplazo in contexto.Reemplazos)
        {
            salida = salida.Replace(reemplazo.Key, reemplazo.Value, StringComparison.Ordinal);
        }

        return salida;
    }

    /// <summary>
    /// Traduce al castellano las tres claves que pone <c>RequestContextMiddleware</c>, para que
    /// toda la linea se lea en el mismo idioma.
    /// </summary>
    private static string NombreCampo(string clave) => clave switch
    {
        "CorrelationId" => "correlacion",
        "Path" => "ruta",
        "Method" => "metodo",
        _ => clave
    };

    private sealed class ContextoLinea
    {
        public Dictionary<string, string?> Campos { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, string> Reemplazos { get; } = new(StringComparer.Ordinal);
    }

    private sealed class AlcanceVacio : IDisposable
    {
        public static readonly AlcanceVacio Instancia = new();

        private AlcanceVacio()
        {
        }

        public void Dispose()
        {
        }
    }
}
