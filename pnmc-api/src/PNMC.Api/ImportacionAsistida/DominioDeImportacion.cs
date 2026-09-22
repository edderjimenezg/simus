using PNMC.Contracts;
using PNMC.Domain.Entities;
using PNMC.Infrastructure.Data;

namespace PNMC.Api.ImportacionAsistida;

/// <summary>Una fila ya planeada por un dominio, todavía sin escribir en ninguna parte.</summary>
/// <param name="Resultado">
/// <c>crear</c>, <c>rechazar</c> o <c>coincidencia_existente</c>. Es lo único del plan que el núcleo
/// interpreta, y por eso es un vocabulario cerrado que la base también comprueba.
/// </param>
/// <param name="ContenidoNormalizadoJson">
/// El dato ya normalizado, con la forma que el dominio quiera. <b>El núcleo no lo interpreta</b>:
/// lo guarda y, cuando hay que enseñarlo, le pide al dominio que lo proyecte.
/// </param>
public sealed record FilaPlaneada(
    int NumeroFila,
    string Resultado,
    bool PuedeImportarse,
    long? RegistroCoincidenteId,
    string ContenidoNormalizadoJson,
    IReadOnlyList<HallazgoDeImportacionDto> Hallazgos);

/// <summary>El plan completo de un archivo, con su huella.</summary>
/// <param name="HuellaPlan">
/// Identifica exactamente lo que se previsualizó. Confirmar con otra huella se rechaza: es la
/// cuarta regla de la capacidad —una confirmación por plan—.
/// </param>
public sealed record PlanDeImportacion(string HuellaPlan, IReadOnlyList<FilaPlaneada> Filas)
{
    public int Importables => Filas.Count(fila => fila.PuedeImportarse);

    public int Rechazadas => Filas.Count - Importables;
}

/// <summary>Lo que el dominio necesita saber al escribir, y que no sabe por sí mismo.</summary>
/// <param name="OrganizacionInstitucionalId">
/// Quién responde por lo que entra sin dueño. Un registro importado no tiene organización
/// responsable declarada, y dejarlo sin custodia lo volvería inalcanzable.
/// </param>
/// <param name="UsuarioId">Quién confirmó. Va a la bitácora y a la procedencia.</param>
/// <param name="Ahora">Una sola marca de tiempo para todo el lote.</param>
public sealed record ContextoDeAplicacion(int OrganizacionInstitucionalId, int UsuarioId, DateTime Ahora);

/// <summary>
/// Lo que un dominio tiene que saber hacer para poder importarse.
/// </summary>
/// <remarks>
/// <para>
/// <b>ES LA COSTURA DE LA CAPACIDAD.</b> El núcleo se ocupa de todo lo que es igual en cualquier
/// importación —validar el sobre, guardar el lote y sus filas, la reserva contra confirmaciones
/// simultáneas, la idempotencia, la bitácora, la retención y la forma de la respuesta— y el dominio
/// solo pone lo suyo: qué campos lee, cómo normaliza, cuándo una fila coincide con algo que ya
/// existe, y cómo se escribe el registro.
/// </para>
/// <para>
/// <b>EL DOMINIO NO DECIDE SI PUBLICA.</b> Declara en qué estado nace lo suyo dentro de
/// <see cref="CapacidadDeImportacion"/>, y una prueba comprueba que ese estado no sea uno público.
/// Es la segunda regla, y un dominio nuevo no puede saltársela por descuido.
/// </para>
/// <para>
/// <b>PLANEAR NO ESCRIBE.</b> Recibe el <see cref="PnmcDbContext"/> porque necesita leer —los
/// territorios, lo que ya existe— pero no debe añadir ni modificar nada: quien guarda el plan es el
/// núcleo, dentro de su propia transacción.
/// </para>
/// </remarks>
public interface IDominioDeImportacion
{
    /// <summary>Qué es este dominio y qué promete. Ver <see cref="ReglasDeImportacion"/>.</summary>
    CapacidadDeImportacion Capacidad { get; }

    /// <summary>Convierte las filas del archivo en un plan. No escribe.</summary>
    Task<PlanDeImportacion> PlanearAsync(
        IReadOnlyList<FilaDeArchivoDto> filas,
        string huellaArchivo,
        PnmcDbContext db,
        CancellationToken ct);

    /// <summary>
    /// Escribe el registro de una fila y devuelve su identificador.
    /// </summary>
    /// <remarks>
    /// SE LLAMA DENTRO DE LA TRANSACCION DEL NUCLEO. El dominio añade a <c>db</c> y guarda lo que
    /// necesite para obtener su identificador; el núcleo cierra la transacción, anota la decisión,
    /// la procedencia y la bitácora, y marca el lote.
    /// </remarks>
    Task<long> AplicarAsync(
        FilaImportacionRow fila,
        ContextoDeAplicacion contexto,
        PnmcDbContext db,
        CancellationToken ct);

    /// <summary>
    /// Proyecta el contenido normalizado a campo → valor, para poder enseñarlo.
    /// </summary>
    /// <remarks>
    /// EL NUCLEO NO INTERPRETA EL JSON. Así un dominio puede guardar lo que necesite —códigos
    /// territoriales resueltos, identificadores de catálogo— sin que el contrato de la capacidad
    /// tenga que crecer por cada uno.
    /// </remarks>
    IReadOnlyDictionary<string, string?> ParaMostrar(string contenidoNormalizadoJson);

    /// <summary>
    /// Si algo cambió entre la previsualización y la confirmación que invalide el plan.
    /// </summary>
    /// <remarks>
    /// EL CASO REAL: entre previsualizar y confirmar, alguien crea a mano un registro que el archivo
    /// también traía. Escribirlo igual duplicaría. El dominio lo sabe comprobar; el núcleo solo sabe
    /// que si devuelve un motivo, no se escribe nada y se pide volver a previsualizar.
    /// </remarks>
    Task<string?> QueCambioDesdeLaPrevisualizacionAsync(
        IReadOnlyList<FilaImportacionRow> filas,
        PnmcDbContext db,
        CancellationToken ct);
}
