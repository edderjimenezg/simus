using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PNMC.Contracts;
using PNMC.Domain.Entities;
using PNMC.Infrastructure.Common;
using PNMC.Infrastructure.Data;

using PNMC.Api.Security;

namespace PNMC.Api.Endpoints;

/// <summary>
/// Nace un Festival: sus datos, su territorio, su clasificación y de dónde vino.
/// </summary>
/// <remarks>
/// <para>
/// <b>POR QUÉ ESTO VIVE EN UN SITIO Y NO EN DOS.</b> Hay dos rutas que crean Festivales y hacen
/// falta las dos: el asistente externo, para la organización que registra el suyo, y la consola
/// institucional, para el funcionario que incorpora uno que no tiene quién lo registre. Antes de
/// extraer esto, la segunda no existía; escribirla aparte habría producido una segunda copia de
/// las reglas de territorio, periodicidad y catálogos, y una copia de una regla es una regla que
/// va a divergir.
/// </para>
/// <para>
/// <b>LO ÚNICO QUE CAMBIA ENTRE LAS DOS ES DE DÓNDE VIENE Y QUIÉN RESPONDE.</b> El canal externo
/// crea el Festival de la organización que tiene la sesión: procedencia y responsable coinciden.
/// La consola puede crear el Festival de una organización que no tiene cuenta: la procedencia es
/// el Programa y la responsable es esa otra organización —o ninguna todavía—. Son tres datos
/// distintos y aquí viajan por separado.
/// </para>
/// <para>
/// <b>NO CREA UNA VARIANTE DEL MODELO.</b> Las dos rutas escriben la misma fila de
/// <c>dbo.Festivales</c>, con el mismo estado inicial y las mismas tablas puente.
/// </para>
/// </remarks>
internal static class AltaDeFestival
{
    /// <summary>Estado con el que nace un Festival, venga por donde venga.</summary>
    internal const string EstadoInicial = EstadosFestival.Borrador;

    // ---------- Normalizadores ------------------------------------------------------------

    internal static string NormalizarNivel(string? valor) => (valor ?? string.Empty).Trim().ToLowerInvariant();

    internal static string? LimpiarTexto(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : ValidationHelpers.SanitizeText(valor, 500);

    internal static string? NormalizarCorreo(string? valor) => CorreoElectronico.Normalizar(valor);

    /// <summary>
    /// Departamento que corresponde al nivel declarado, o <c>null</c> si el nivel es nacional.
    /// </summary>
    /// <remarks>
    /// EL TERRITORIO SE LIMPIA SEGÚN EL NIVEL, aunque venga puesto. Un nacional con departamento
    /// es un residuo de haber elegido municipal antes, y <c>CK_Festivales_NivelCobertura</c> lo
    /// rechazaría con un 500 que no explica nada.
    /// </remarks>
    internal static string? DepartamentoSegunNivel(CrearFestivalBorradorSolicitud solicitud) =>
        NormalizarNivel(solicitud.NivelCobertura) == "nacional" ? null : LimpiarTexto(solicitud.CodigoDepartamento);

    /// <summary>Municipio que corresponde al nivel: solo el nivel municipal lo lleva.</summary>
    internal static string? MunicipioSegunNivel(CrearFestivalBorradorSolicitud solicitud) =>
        NormalizarNivel(solicitud.NivelCobertura) == "municipal" ? LimpiarTexto(solicitud.CodigoMunicipio) : null;

    // ---------- Validación ----------------------------------------------------------------

    /// <summary>
    /// Comprueba el Festival contra las reglas del API, DIVIPOLA y los catálogos.
    /// </summary>
    /// <remarks>
    /// DEVUELVE TODO LO QUE ESTÁ MAL DE UNA VEZ, salvo cuando un error invalida la comprobación
    /// siguiente: sin nivel de cobertura válido no tiene sentido decir nada del territorio.
    /// </remarks>
    internal static async Task<Dictionary<string, string[]>> ValidarAsync(
        CrearFestivalBorradorSolicitud solicitud,
        PnmcDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var errores = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        if (ValidationHelpers.IsMissing(solicitud.Nombre)) errores["nombre"] = ["El nombre del Festival es obligatorio."];
        if (!string.IsNullOrWhiteSpace(solicitud.CorreoContacto) && !ValidationHelpers.IsValidEmail(solicitud.CorreoContacto))
            errores["correoContacto"] = ["El correo de contacto no es válido."];

        var periodicidad = PeriodicidadesFestival.Normalizar(solicitud.Periodicidad);
        if (periodicidad is not null && !PeriodicidadesFestival.Todas.Contains(periodicidad))
            errores["periodicidad"] = ["Selecciona una periodicidad de la lista disponible."];
        if (PeriodicidadesFestival.RequiereDetalle(periodicidad) && ValidationHelpers.IsMissing(solicitud.PeriodicidadDetalle))
            errores["periodicidadDetalle"] = ["Explica brevemente la periodicidad seleccionada."];
        if (!string.IsNullOrWhiteSpace(solicitud.PeriodicidadDetalle) && solicitud.PeriodicidadDetalle.Trim().Length > 600)
            errores["periodicidadDetalle"] = ["La explicación de periodicidad no puede superar 600 caracteres."];

        var nivel = NormalizarNivel(solicitud.NivelCobertura);
        if (nivel is not ("municipal" or "departamental" or "nacional"))
        {
            errores["nivelCobertura"] = ["El nivel territorial no es válido."];
            return errores;
        }

        if (nivel != "nacional")
        {
            var departamento = LimpiarTexto(solicitud.CodigoDepartamento);
            var departamentoValido = departamento is not null && await dbContext.DivipolaLocations.AsNoTracking()
                .AnyAsync(item => item.DepartmentCode == departamento, cancellationToken);
            if (!departamentoValido)
            {
                errores["codigoDepartamento"] = ["El departamento indicado no existe en DIVIPOLA."];
                return errores;
            }

            if (nivel == "municipal")
            {
                var municipio = LimpiarTexto(solicitud.CodigoMunicipio);
                var municipioValido = municipio is not null && await dbContext.DivipolaLocations.AsNoTracking()
                    .AnyAsync(item => item.DepartmentCode == departamento && item.MunicipalityCode == municipio, cancellationToken);
                if (!municipioValido) errores["codigoMunicipio"] = ["El municipio indicado no existe en DIVIPOLA."];
            }
        }

        var practicasIds = solicitud.PracticasMusicalesIds.Distinct().ToArray();
        if (practicasIds.Length > 0)
        {
            var existentes = await dbContext.PracticasMusicales.AsNoTracking().CountAsync(item => practicasIds.Contains(item.Id), cancellationToken);
            if (existentes != practicasIds.Length) errores["practicasMusicalesIds"] = ["Una o más prácticas musicales no existen en el catálogo."];
        }

        var territoriosIds = solicitud.TerritoriosSonorosIds.Distinct().ToArray();
        if (territoriosIds.Length > 0)
        {
            var existentes = await dbContext.TerritoriosSonoros.AsNoTracking().CountAsync(item => territoriosIds.Contains(item.Id), cancellationToken);
            if (existentes != territoriosIds.Length) errores["territoriosSonorosIds"] = ["Uno o más territorios sonoros no existen en el catálogo."];
        }

        return errores;
    }

    // ---------- Alta ----------------------------------------------------------------------

    /// <summary>
    /// Escribe el Festival, sus prácticas, sus territorios sonoros y su procedencia.
    /// </summary>
    /// <remarks>
    /// <para>
    /// QUIEN LLAMA TIENE QUE ESTAR DENTRO DE UNA TRANSACCIÓN. El Festival, sus relaciones y su
    /// procedencia son una sola cosa: un fallo a mitad dejaría un Festival sin clasificación o sin
    /// rastro de quién lo incorporó, y nadie sabría que falta.
    /// </para>
    /// <para>
    /// <c>organizacionResponsableId</c> PUEDE SER NULO, y eso es una respuesta válida en la
    /// consola: un Festival histórico puede no tener todavía una organización que responda por él.
    /// La base lo permite y el propio contexto asigna la institucional como responsable
    /// provisional —<c>PnmcDbContext.AsignarOrganizacionResponsable</c>— sin que eso tenga nada
    /// que ver con su procedencia.
    /// </para>
    /// </remarks>
    internal static async Task<FestivalRow> CrearAsync(
        PnmcDbContext dbContext,
        CrearFestivalBorradorSolicitud solicitud,
        int? organizacionResponsableId,
        int usuarioId,
        string contexto,
        int? organizacionProcedenciaId,
        DateTime ahora,
        CancellationToken cancellationToken)
    {
        var festival = new FestivalRow
        {
            Name = ValidationHelpers.SanitizeText(solicitud.Nombre, 240),
            Description = LimpiarTexto(solicitud.Descripcion),
            Periodicidad = PeriodicidadesFestival.Normalizar(solicitud.Periodicidad),
            PeriodicidadDetalle = LimpiarTexto(solicitud.PeriodicidadDetalle),
            ContactEmail = CorreoElectronico.Normalizar(solicitud.CorreoContacto),
            InstagramUrl = LimpiarTexto(solicitud.Instagram),
            FacebookUrl = LimpiarTexto(solicitud.Facebook),
            WebsiteUrl = LimpiarTexto(solicitud.PaginaWeb),
            OtherUrl = LimpiarTexto(solicitud.OtroEnlace),
            ContactPhone = LimpiarTexto(solicitud.TelefonoCelular),
            ObservacionesContacto = LimpiarTexto(solicitud.ObservacionesContacto),
            CoverageLevel = NormalizarNivel(solicitud.NivelCobertura),
            DepartmentCode = DepartamentoSegunNivel(solicitud),
            MunicipalityCode = MunicipioSegunNivel(solicitud),
            OrganizacionPrincipalId = organizacionResponsableId,
            StatusCode = EstadoInicial,
            CreatedAt = ahora,
            UpdatedAt = ahora,
        };

        dbContext.FestivalRecords.Add(festival);
        await dbContext.SaveChangesAsync(cancellationToken);

        dbContext.Agregar<PracticaMusicalDeRegistroRow>(
            Modulos.Festivales, festival.Id, solicitud.PracticasMusicalesIds, ahora);
        dbContext.Agregar<TerritorioSonoroDeRegistroRow>(
            Modulos.Festivales, festival.Id, solicitud.TerritoriosSonorosIds, ahora);

        await ProcedenciaDeRegistro.AnotarAsync(
            dbContext,
            Modulos.Festivales,
            festival.Id.ToString(CultureInfo.InvariantCulture),
            contexto,
            organizacionProcedenciaId,
            usuarioId,
            cancellationToken);

        return festival;
    }
}
