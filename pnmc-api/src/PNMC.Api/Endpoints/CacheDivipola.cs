using Microsoft.EntityFrameworkCore;
using PNMC.Api.ConsultaGuiada;
using PNMC.Infrastructure.Data;

namespace PNMC.Api.Endpoints;

/// <summary>
/// Los nombres de departamentos y municipios del DANE, cargados una sola vez, en dos vistas.
/// </summary>
/// <remarks>
/// <para>
/// El geovisor resuelve codigos a nombres en cada peticion, y para eso el mapa
/// se traia la tabla DIVIPOLA entera —unos mil cien municipios— dos veces por
/// llamada: una agrupada por departamento y otra completa. Es trabajo repetido
/// sobre datos que no cambian: la unica escritura de esa tabla esta en el
/// bootstrapper, al arrancar.
/// </para>
/// <para>
/// Por eso se cachea para toda la vida del proceso y sin caducidad. Poner un
/// vencimiento sugeriria que el dato puede cambiar solo, y no puede: la division
/// politico-administrativa se actualiza con un despliegue, que reinicia el
/// proceso y vacia esto de todos modos.
/// </para>
/// <para>
/// DOS VISTAS DE LAS MISMAS FILAS, y por eso una sola clase. La <b>cruda</b> devuelve el nombre
/// tal como esta en la base y la consumen el resumen del mapa, el geovisor y la analitica publica
/// de festivales. La <b>presentable</b> devuelve el nombre capitalizado —«ANTIOQUIA» sale
/// «Antioquia»— y la consume la consola administrativa, que hasta el 24 ago 2026 lo resolvia por
/// su cuenta barriendo la tabla DOS veces en cada peticion. Meter la capitalizacion dentro de la
/// vista cruda habria cambiado el JSON de las otras tres rutas sin que nadie lo pidiera; por eso
/// son dos y no una, y por eso se construyen juntas: la segunda no cuesta ni una consulta mas.
/// </para>
/// <para>
/// El cerrojo con doble comprobacion existe por el arranque: varias peticiones
/// simultaneas nada mas levantar el servicio harian la misma consulta a la vez.
/// Una vez cargado, el camino rapido no toca el semaforo.
/// </para>
/// </remarks>
public sealed class CacheDivipola : IDisposable
{
    /// <summary>Las cuatro tablas de traduccion, publicadas de una pieza.</summary>
    /// <remarks>
    /// UN SOLO CAMPO Y NO CUATRO, Y ESO ARREGLA UNA CARRERA. Con cuatro campos independientes, el
    /// camino rapido miraba dos de ellos y un hilo que entrase entre la escritura del segundo y la
    /// del tercero se llevaba dos nulos. Publicando un unico objeto inmutable, o esta todo o no
    /// hay nada: no existe el estado a medias que habia que documentar con un comentario sobre el
    /// orden de las asignaciones. Es mas barato hacer imposible el estado malo que explicarlo.
    /// </remarks>
    private sealed record Vistas(
        IReadOnlyDictionary<string, string> Departamentos,
        IReadOnlyDictionary<string, string> Municipios,
        IReadOnlyDictionary<string, string> DepartamentosPresentables,
        IReadOnlyDictionary<string, string> MunicipiosPresentables,
        VocabularioTerritorial Vocabulario);

    private readonly SemaphoreSlim _cerrojo = new(1, 1);
    private Vistas? _vistas;

    /// <summary>Los nombres tal como estan en la base. Es lo que sirven el mapa y la analitica.</summary>
    public async Task<(IReadOnlyDictionary<string, string> Departamentos, IReadOnlyDictionary<string, string> Municipios)>
        ObtenerAsync(PnmcDbContext dbContext, CancellationToken cancellationToken)
    {
        var vistas = await CargarAsync(dbContext, cancellationToken);
        return (vistas.Departamentos, vistas.Municipios);
    }

    /// <summary>Los mismos nombres, capitalizados para mostrarlos. Es lo que sirve la consola.</summary>
    public async Task<(IReadOnlyDictionary<string, string> Departamentos, IReadOnlyDictionary<string, string> Municipios)>
        ObtenerPresentablesAsync(PnmcDbContext dbContext, CancellationToken cancellationToken)
    {
        var vistas = await CargarAsync(dbContext, cancellationToken);
        return (vistas.DepartamentosPresentables, vistas.MunicipiosPresentables);
    }

    /// <summary>
    /// Los mismos nombres al revés: del nombre escrito al código, para reconocerlos dentro de una
    /// pregunta de la Consulta Guiada.
    /// </summary>
    /// <remarks>
    /// TERCERA VISTA DE LAS MISMAS FILAS, por la razón por la que ya había dos: la alternativa era
    /// que la Consulta Guiada se trajera DIVIPOLA entera en cada pregunta para construir su propio
    /// índice, o —peor— que llevara escritos dentro los treinta y tres departamentos. Aquí no cuesta
    /// ni una consulta más y no puede separarse del dato, que es la regla del proyecto: la división
    /// político-administrativa se lee de <c>dbo.Divipola</c> y de ningún otro sitio.
    /// </remarks>
    public async Task<VocabularioTerritorial> ObtenerVocabularioAsync(
        PnmcDbContext dbContext, CancellationToken cancellationToken)
    {
        var vistas = await CargarAsync(dbContext, cancellationToken);
        return vistas.Vocabulario;
    }

    private async Task<Vistas> CargarAsync(PnmcDbContext dbContext, CancellationToken cancellationToken)
    {
        var yaCargadas = Volatile.Read(ref _vistas);
        if (yaCargadas is not null)
        {
            return yaCargadas;
        }

        await _cerrojo.WaitAsync(cancellationToken);
        try
        {
            yaCargadas = Volatile.Read(ref _vistas);
            if (yaCargadas is not null)
            {
                return yaCargadas;
            }

            // Una sola lectura de la tabla para las dos vistas. Antes eran dos
            // consultas y la primera agrupaba en la base para quedarse con un
            // nombre por departamento; con las filas ya en memoria sale igual y
            // sin el segundo viaje.
            var filas = await dbContext.DivipolaLocations
                .AsNoTracking()
                .Select(fila => new { fila.DepartmentCode, fila.DepartmentName, fila.MunicipalityCode, fila.MunicipalityName })
                .ToListAsync(cancellationToken);

            var departamentos = filas
                .GroupBy(fila => fila.DepartmentCode)
                .ToDictionary(grupo => grupo.Key, grupo => grupo.First().DepartmentName);

            // `ToDictionary` a secas revienta si DIVIPOLA trae un codigo de
            // municipio repetido. Que reviente el geovisor publico por un dato
            // duplicado en una tabla de referencia seria una averia enorme para
            // una causa minuscula: se queda con la primera aparicion.
            var municipios = filas
                .GroupBy(fila => fila.MunicipalityCode)
                .ToDictionary(grupo => grupo.Key, grupo => grupo.First().MunicipalityName);

            var departamentosPresentables = departamentos.ToDictionary(
                par => par.Key, par => TextoEnEspanol.ATituloDeColombia(par.Value));
            var municipiosPresentables = municipios.ToDictionary(
                par => par.Key, par => TextoEnEspanol.ATituloDeColombia(par.Value));

            var vistas = new Vistas(
                departamentos,
                municipios,
                departamentosPresentables,
                municipiosPresentables,
                // EL VOCABULARIO SE CONSTRUYE CON LOS NOMBRES PRESENTABLES porque son los que acaban
                // escritos en la respuesta: quien pregunta por el Huila lee «Huila» y no «HUILA».
                // Para reconocerlos da igual, que los normaliza igualmente.
                VocabularioTerritorial.Construir(filas.Select(fila => new VocabularioTerritorial.FilaTerritorial(
                    fila.DepartmentCode,
                    departamentosPresentables.GetValueOrDefault(fila.DepartmentCode, fila.DepartmentName),
                    fila.MunicipalityCode,
                    municipiosPresentables.GetValueOrDefault(fila.MunicipalityCode, fila.MunicipalityName)))));

            // EL VACIO NO SE PUBLICA, Y ESTO NO ES UNA SUTILEZA. DIVIPOLA se siembra con un guion
            // aparte (`scripts/seed-local-db.sh`), y el orden habitual en local es levantar el API
            // ANTES de sembrarla. Si la primera peticion encuentra la tabla vacia, guardar ese
            // vacio para siempre significa que la consola muestra codigos crudos —«05», «05001»—
            // el resto de la vida del proceso, incluso despues de sembrar, y solo se arregla
            // reiniciando el servicio. Antes del 24 ago 2026 esa ruta releia la tabla en cada
            // peticion y se recuperaba sola; cachear sin esta guarda lo habria convertido en una
            // averia persistente. Con la tabla vacia se devuelve el vacio SIN publicarlo, y la
            // siguiente peticion vuelve a mirar.
            if (filas.Count > 0)
            {
                Volatile.Write(ref _vistas, vistas);
            }

            return vistas;
        }
        finally
        {
            _cerrojo.Release();
        }
    }

    public void Dispose() => _cerrojo.Dispose();
}
