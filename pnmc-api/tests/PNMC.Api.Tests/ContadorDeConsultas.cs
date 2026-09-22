using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace PNMC.Api.Tests;

/// <summary>
/// Cuenta las ordenes SQL que una peticion manda de verdad a la base.
/// </summary>
/// <remarks>
/// <para>
/// POR QUE HACIA FALTA ESTO ANTES DE TOCAR NADA. El backend tenia anotado desde el 23 de agosto
/// de 2026 que <c>GET /monitor</c> costaba 17 viajes y que el sondeo del panel los multiplicaba
/// por pestaña abierta. Ese numero se habia obtenido <b>leyendo el codigo</b>, y por tanto no
/// podia ponerse en rojo: cualquier «optimizacion» posterior seria una afirmacion sin medida, y
/// cualquier regresion —alguien que anada una consulta dentro de un bucle— entraria sin que
/// nada la delatase. Un objetivo de rendimiento sin instrumento no es un objetivo, es una
/// opinion.
/// </para>
/// <para>
/// QUE CUENTA. Las tres formas de ejecutar una orden en EF Core, en sus variantes sincrona y
/// asincrona. No cuenta aperturas de conexion ni transacciones: lo que interesa es el numero de
/// idas y vueltas con trabajo dentro, que es lo que se paga por vCore en Azure SQL.
/// </para>
/// <para>
/// Y QUE NO CUENTA, QUE ES IGUAL DE IMPORTANTE. <b>Solo ve lo que pasa por EF Core.</b> Una
/// consulta escrita a mano sobre la conexion —<c>GetDbConnection().CreateCommand()</c>— es
/// invisible para el, y en este proyecto hay al menos una: la de relaciones del ecosistema en
/// <c>CatalogModuleEndpoints.LoadRelationsAsync</c>. Por eso ninguna cifra de este contador debe
/// leerse como «el total de consultas de la ruta» sin comprobar antes que la ruta no abre
/// conexiones por su cuenta. Un instrumento con un punto ciego sin documentar es peor que no
/// tener instrumento: da una cifra con aspecto de completa.
/// </para>
/// <para>
/// ES DE UN SOLO USO POR MEDICION. <see cref="Reiniciar"/> antes de la peticion y leer despues.
/// Las clases que lo usan no comparten fabrica con nadie, porque la suite corre clases en
/// paralelo y un contador compartido mediria el trafico de las vecinas.
/// </para>
/// <para>
/// CALIENTA ANTES DE MEDIR. La primera peticion autenticada de un proceso paga dos cosas que no
/// son el coste de la ruta: la revalidacion de la sesion contra la base y la carga del cache de
/// DIVIPOLA. Medirlas seria describir el arranque en vez del estado estacionario, y ademas haria
/// que el numero dependiera del orden en que xunit ejecute los metodos — un numero exacto que
/// depende del orden no es una medida.
/// </para>
/// <para>
/// Y CUIDADO SI ALGUIEN LO ENCHUFA A SQL SERVER. Estos interceptores se disparan <b>una vez por
/// INTENTO</b>, no por consulta logica. El registro de produccion usa
/// <c>EnableRetryOnFailure</c>, asi que un solo tiempo de espera transitorio convertiria 13 en
/// 14 o 15 y la prueba caeria con un numero que parece una regresion y no lo es. Hoy no ocurre
/// porque el contador solo se engancha al proveedor SQLite de esta clase, que no reintenta; quien
/// lo lleve al carril de SQL Server tiene que contar con esto antes de fijar un numero exacto.
/// </para>
/// </remarks>
public sealed class ContadorDeConsultas : DbCommandInterceptor
{
    private int _ordenes;

    /// <summary>Ordenes contadas desde el ultimo <see cref="Reiniciar"/>.</summary>
    public int Ordenes => Volatile.Read(ref _ordenes);

    /// <summary>Textos SQL vistos, para poder decir QUE se repitio y no solo cuanto.</summary>
    public IReadOnlyList<string> Textos
    {
        get
        {
            lock (_cerrojo)
            {
                return [.. _textos];
            }
        }
    }

    private readonly List<string> _textos = [];
    private readonly Lock _cerrojo = new();

    public void Reiniciar()
    {
        Volatile.Write(ref _ordenes, 0);
        lock (_cerrojo)
        {
            _textos.Clear();
        }
    }

    /// <summary>Cuantas ordenes distintas comparten el mismo texto SQL. Delata el trabajo repetido.</summary>
    public int RepeticionesDe(string fragmento)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fragmento);
        lock (_cerrojo)
        {
            return _textos.Count(texto => texto.Contains(fragmento, StringComparison.OrdinalIgnoreCase));
        }
    }

    private void Anotar(DbCommand command)
    {
        Interlocked.Increment(ref _ordenes);
        lock (_cerrojo)
        {
            _textos.Add(command.CommandText);
        }
    }

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        Anotar(command);
        return base.ReaderExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Anotar(command);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
    {
        Anotar(command);
        return base.ScalarExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        Anotar(command);
        return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
    {
        Anotar(command);
        return base.NonQueryExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Anotar(command);
        return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }
}
