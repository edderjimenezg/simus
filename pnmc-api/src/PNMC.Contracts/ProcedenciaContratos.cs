namespace PNMC.Contracts;

/// <summary>
/// De dónde vino un registro, para enseñarlo en la ficha y en la auditoría.
/// </summary>
/// <remarks>
/// <para>
/// LAS TRES DIMENSIONES VIAJAN SEPARADAS A PROPÓSITO, porque son tres preguntas distintas:
/// </para>
/// <list type="bullet">
///   <item><b>Procedencia</b> — qué entidad lo incorporó al sistema.</item>
///   <item><b>Usuario</b> — qué cuenta ejecutó la acción.</item>
///   <item><b>Organización responsable</b> — quién gestiona realmente el proceso. <b>No viaja
///   aquí</b>: vive en el propio registro, porque es un dato suyo y puede cambiar de manos.</item>
/// </list>
/// <para>
/// QUE EL PNMC HAYA INCORPORADO UN FESTIVAL NO LO CONVIERTE EN SU ORGANIZACIÓN RESPONSABLE.
/// Mezclarlo acabaría contándole al Programa festivales que no organiza.
/// </para>
/// </remarks>
public sealed record ProcedenciaDeRegistroDto(
    /// <summary>administrativo · externo · importacion · siembra.</summary>
    string ContextoOrigen,
    /// <summary>Cómo se lee ese contexto en pantalla.</summary>
    string ContextoEtiqueta,
    /// <summary><c>true</c> cuando el registro entró por la consola institucional.</summary>
    bool EsInstitucional,
    int? OrganizacionProcedenciaId,
    string? OrganizacionProcedenciaNombre,
    int? UsuarioCreadorId,
    string? UsuarioCreadorNombre,
    DateTime FechaRegistro);
