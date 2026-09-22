using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PNMC.Domain.Entities;
using PNMC.Infrastructure.Data;

namespace PNMC.Api.Endpoints;

/// <summary>
/// Una propuesta de cambio de Festival, vista como el perfil completo que propondría.
/// </summary>
/// <remarks>
/// <para>
/// <b>POR QUE HACE FALTA.</b> La tabla guarda SOLO LO QUE CAMBIA, pero el contrato del Festival
/// —que sus dos pantallas ya usan— habla de la propuesta como un objeto entero: nombre, territorio,
/// contacto, prácticas. Esta clase reconstruye ese objeto poniendo el diff ENCIMA de la versión
/// sobre la que se propuso, que es exactamente lo que la propuesta dice.
/// </para>
/// <para>
/// <b>LA VERSION DE ORIGEN NO ES LA VIGENTE DE HOY.</b> Sale de <c>SubregistroId</c>, que la
/// propuesta declaró al nacer: una propuesta de hace un mes se hizo contra lo que el público leía
/// entonces, y proyectarla sobre lo de ahora enseñaría un cambio que nadie propuso.
/// </para>
/// <para>
/// <b>Y EN EL SENTIDO CONTRARIO.</b> <see cref="DiferenciasAsync"/> toma un perfil completo —el que
/// manda el formulario— y devuelve solo los campos en que se aparta de la versión de origen. Es la
/// misma operación que el panel de la organización hace para un mercado, hecha aquí porque el
/// contrato de Festival manda el objeto entero: calcularla en el servidor impide que quien propone
/// decida contra qué se le compara.
/// </para>
/// </remarks>
internal sealed class PropuestaDeFestivalProyectada
{
    private PropuestaDeFestivalProyectada(
        PropuestaDeCambioRow expediente,
        VersionFestivalRow versionOrigen,
        IReadOnlyDictionary<string, string?> propuestos,
        List<int> practicas,
        List<int> territorios)
    {
        Expediente = expediente;
        VersionOrigen = versionOrigen;
        Propuestos = propuestos;
        PracticasMusicales = practicas;
        TerritoriosSonoros = territorios;
    }

    internal PropuestaDeCambioRow Expediente { get; }
    internal VersionFestivalRow VersionOrigen { get; }

    /// <summary>Lo que el expediente pide cambiar, por identificador de campo.</summary>
    internal IReadOnlyDictionary<string, string?> Propuestos { get; }

    internal List<int> PracticasMusicales { get; }
    internal List<int> TerritoriosSonoros { get; }

    /// <summary>El valor que tendría el campo si la propuesta se aplicara.</summary>
    internal string? Valor(string campoId) =>
        Propuestos.TryGetValue(campoId, out var propuesto)
            ? propuesto
            : CamposProponiblesDeFestival.Todos.TryGetValue(campoId, out var campo) ? campo.Leer(VersionOrigen) : null;

    internal string Nombre => Valor("nombre") ?? string.Empty;
    internal string? Descripcion => Valor("descripcion");
    internal string NivelCobertura => Valor("nivelCobertura") ?? string.Empty;
    internal string? CodigoDepartamento => Valor("codigoDepartamento");
    internal string? CodigoMunicipio => Valor("codigoMunicipio");
    internal string? Periodicidad => Valor("periodicidad");
    internal string? PeriodicidadDetalle => Valor("periodicidadDetalle");
    internal string? CorreoContacto => Valor("correoContacto");
    internal string? TelefonoContacto => Valor("telefonoContacto");
    internal string? Instagram => Valor("instagram");
    internal string? Facebook => Valor("facebook");
    internal string? SitioWeb => Valor("sitioWeb");
    internal string? OtroEnlace => Valor("otroEnlace");
    internal string? ObservacionesContacto => Valor("observacionesContacto");
    internal string? Director => Valor("director");
    internal int? TipoOrganizadorId =>
        int.TryParse(Valor("tipoOrganizadorId"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) && id > 0
            ? id : null;

    /// <summary>Monta la proyección de un expediente: su versión de origen más lo que pide cambiar.</summary>
    /// <returns><c>null</c> si la versión de origen ya no existe, que es un expediente sin suelo.</returns>
    internal static async Task<PropuestaDeFestivalProyectada?> DeAsync(
        PnmcDbContext db, PropuestaDeCambioRow expediente, CancellationToken ct)
    {
        if (!int.TryParse(expediente.SubregistroId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var versionId))
        {
            return null;
        }

        var version = await db.VersionesFestival.AsNoTracking().FirstOrDefaultAsync(v => v.Id == versionId, ct);
        if (version is null) return null;

        var campos = await db.PropuestasDeCambioCampos.AsNoTracking()
            .Where(c => c.IdPropuesta == expediente.Id)
            .ToListAsync(ct);
        var propuestos = campos.ToDictionary(c => c.CampoId, c => c.ValorPropuesto, StringComparer.Ordinal);

        var practicas = propuestos.TryGetValue(CamposProponiblesDeFestival.PracticasMusicales, out var pm)
            ? CamposProponiblesDeFestival.Identificadores(pm)
            : await db.ValoresAsync<PracticaMusicalDeRegistroRow>(Modulos.VersionesDeFestival, version.Id, ct);

        var territorios = propuestos.TryGetValue(CamposProponiblesDeFestival.TerritoriosSonoros, out var ts)
            ? CamposProponiblesDeFestival.Identificadores(ts)
            : await db.ValoresAsync<TerritorioSonoroDeRegistroRow>(Modulos.VersionesDeFestival, version.Id, ct);

        return new PropuestaDeFestivalProyectada(expediente, version, propuestos, practicas, territorios);
    }

    /// <summary>
    /// Deja los campos del expediente EXACTAMENTE como dice el perfil que llega: crea, actualiza y
    /// borra, guardando solo aquello en que se aparta de la versión de origen.
    /// </summary>
    internal static async Task GuardarDiferenciasAsync(
        PnmcDbContext db,
        PropuestaDeCambioRow expediente,
        VersionFestivalRow versionOrigen,
        IReadOnlyDictionary<string, string?> perfilCompleto,
        List<int> practicas,
        List<int> territorios,
        DateTime ahora,
        CancellationToken ct)
    {
        var diferencias = new Dictionary<string, string?>(StringComparer.Ordinal);

        foreach (var (campoId, campo) in CamposProponiblesDeFestival.Todos)
        {
            if (campo.Escribir is null) continue;
            if (!perfilCompleto.TryGetValue(campoId, out var propuesto)) continue;
            var antes = Vacio(campo.Leer(versionOrigen));
            var ahoraValor = Vacio(propuesto);
            if (!string.Equals(antes, ahoraValor, StringComparison.Ordinal))
            {
                diferencias[campoId] = ahoraValor;
            }
        }

        // LAS DOS LISTAS SE COMPARAN YA MONTADAS: identificadores separados por comas y en orden,
        // que es la forma en que las guardan las dos puertas. Así la comparación de texto basta.
        var practicasOrigen = CamposProponiblesDeFestival.Lista(
            await db.ValoresAsync<PracticaMusicalDeRegistroRow>(Modulos.VersionesDeFestival, versionOrigen.Id, ct));
        var territoriosOrigen = CamposProponiblesDeFestival.Lista(
            await db.ValoresAsync<TerritorioSonoroDeRegistroRow>(Modulos.VersionesDeFestival, versionOrigen.Id, ct));

        var practicasNuevas = CamposProponiblesDeFestival.Lista(practicas);
        var territoriosNuevos = CamposProponiblesDeFestival.Lista(territorios);
        if (!string.Equals(practicasOrigen, practicasNuevas, StringComparison.Ordinal))
        {
            diferencias[CamposProponiblesDeFestival.PracticasMusicales] = practicasNuevas;
        }
        if (!string.Equals(territoriosOrigen, territoriosNuevos, StringComparison.Ordinal))
        {
            diferencias[CamposProponiblesDeFestival.TerritoriosSonoros] = territoriosNuevos;
        }

        var existentes = expediente.Id == 0
            ? []
            : await db.PropuestasDeCambioCampos.Where(c => c.IdPropuesta == expediente.Id).ToListAsync(ct);

        foreach (var fila in existentes.Where(fila => !diferencias.ContainsKey(fila.CampoId)))
        {
            db.PropuestasDeCambioCampos.Remove(fila);
        }

        foreach (var (campoId, valor) in diferencias)
        {
            var definicion = CamposProponiblesDeFestival.Todos[campoId];
            var fila = existentes.Find(f => string.Equals(f.CampoId, campoId, StringComparison.Ordinal));
            if (fila is null)
            {
                db.PropuestasDeCambioCampos.Add(new PropuestaDeCambioCampoRow
                {
                    Propuesta = expediente,
                    SeccionId = definicion.SeccionId,
                    CampoId = campoId,
                    CampoEtiqueta = definicion.Etiqueta,
                    // EL «ANTES» SE COPIA YA, y no al enviar como en Mercados: aquí la versión de
                    // origen no puede cambiar —una versión publicada es inmutable—, así que copiarlo
                    // al escribir dice lo mismo y deja el expediente completo desde el primer guardado.
                    ValorAnterior = Vacio(definicion.Leer(versionOrigen)) ?? ValorDeLista(campoId, practicasOrigen, territoriosOrigen),
                    ValorPropuesto = valor,
                    FechaCreacion = ahora,
                });
                continue;
            }

            fila.SeccionId = definicion.SeccionId;
            fila.CampoEtiqueta = definicion.Etiqueta;
            fila.ValorPropuesto = valor;
            fila.FechaActualizacion = ahora;
        }
    }

    private static string? ValorDeLista(string campoId, string? practicas, string? territorios) =>
        campoId == CamposProponiblesDeFestival.PracticasMusicales ? practicas
        : campoId == CamposProponiblesDeFestival.TerritoriosSonoros ? territorios
        : null;

    /// <summary>Trata la cadena vacía como ausencia, que es como la trata el resto del circuito.</summary>
    private static string? Vacio(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
