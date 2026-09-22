namespace PNMC.Domain.Entities;

/// <summary>
/// Un lugar donde ocurre un registro del Ecosistema.
/// </summary>
/// <remarks>
/// <para>
/// <b>ERAN DOS TABLAS IDENTICAS.</b> <c>VersionesFestivalLocalizaciones</c> y
/// <c>EdicionesFestivalLocalizaciones</c> tenían exactamente los mismos siete campos: no se
/// diferenciaban en nada salvo en de quién colgaban. Desde son una,
/// con el dueño en <see cref="ModuloId"/> + <see cref="RegistroId"/>.
/// </para>
/// <para>
/// La comprobación de que el municipio pertenece al departamento —que solo tenía la de versiones—
/// se conserva para las dos: era la correcta, y tenerla en una sola era el descuido.
/// </para>
/// </remarks>
public sealed class LocalizacionDeRegistroRow : IRelacionDeRegistro
{
    public long Id { get; set; }
    public string ModuloId { get; set; } = string.Empty;
    public string RegistroId { get; set; } = string.Empty;
    public string CodigoDepartamento { get; set; } = string.Empty;
    public string CodigoMunicipio { get; set; } = string.Empty;
    public int? ZonaUrbanoRuralId { get; set; }
    public int? TitulacionColectivaId { get; set; }
    public DateTime FechaCreacion { get; set; }
}

/// <summary>
/// Una entidad que acompaña a un registro del Ecosistema en su realización.
/// </summary>
/// <remarks>
/// <b>ERA LA MISMA ENTIDAD CON DOS NOMBRES.</b> En la versión del perfil público se llamaba
/// «entidad socia» y en la edición anual «entidad aliada», con los mismos siete campos y con las
/// columnas del nombre y del correo escritas de dos maneras. Quedó una sola, «aliada», que es como
/// la nombra la pantalla que se usa; el nombre no era la parte importante, tener dos sí lo era.
/// </remarks>
public sealed class EntidadAliadaDeRegistroRow : IRelacionDeRegistro
{
    public long Id { get; set; }
    public string ModuloId { get; set; } = string.Empty;
    public string RegistroId { get; set; } = string.Empty;
    public string? Nombre { get; set; }
    public string? Correo { get; set; }
    public int? NaturalezaEntidadId { get; set; }

    /// <summary>La organización del sistema, cuando la entidad aliada ya está registrada.</summary>
    public int? EntidadId { get; set; }

    public DateTime FechaCreacion { get; set; }
}

/// <summary>
/// Un archivo o enlace que acompaña a un registro del Ecosistema.
/// </summary>
/// <remarks>
/// <para>
/// <b>ERAN «ARCHIVOS» EN UN SITIO Y «MATERIALES» EN OTRO.</b> Las dos guardaban lo mismo —un
/// archivo del banco o una URL, con descripción y orden— y solo se distinguían en el nombre y en
/// que la de versiones llevaba además <see cref="RolArchivo"/>. Quedó una, y el rol se conserva
/// porque lo usa el formulario del perfil público.
/// </para>
/// <para>
/// <b>EL ROL NO VIENE DEL VOLCADO DE SIMUS</b>, que solo tiene URL y descripción: es nuestro, con
/// valores como afiche, programa o logo. Cuando no llega, vale <c>material</c>, y eso es lo que
/// escriben las ediciones, que no lo preguntan.
/// </para>
/// </remarks>
public sealed class ArchivoDeRegistroRow : IRelacionDeRegistro
{
    public long Id { get; set; }
    public string ModuloId { get; set; } = string.Empty;
    public string RegistroId { get; set; } = string.Empty;

    /// <summary>El archivo del banco, cuando se subió uno.</summary>
    public int? ArchivoId { get; set; }

    /// <summary>El enlace, cuando en lugar de subir se enlaza.</summary>
    public string? Url { get; set; }

    /// <summary>Qué papel cumple: afiche, programa, logo o, por omisión, material.</summary>
    public string RolArchivo { get; set; } = RolesDeArchivo.Material;

    /// <summary>Lo que el volcado de SIMUS llama <c>DESCRIPCION_ARCHIVO</c>.</summary>
    public string? DescripcionArchivo { get; set; }

    public int OrdenVisualizacion { get; set; }
    public DateTime FechaCreacion { get; set; }
}

/// <summary>El valor que toma <see cref="ArchivoDeRegistroRow.RolArchivo"/> cuando no se declara.</summary>
public static class RolesDeArchivo
{
    public const string Material = "material";
}
