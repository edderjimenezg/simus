namespace PNMC.Domain.Entities;

/// <summary>
/// Un valor de vocabulario controlado atribuido a un registro cualquiera del Ecosistema.
/// </summary>
/// <remarks>
/// <para>
/// <b>POR QUE HAY UNA TABLA POR VOCABULARIO Y NO UNA SOLA PARA TODOS.</b> Cada una conserva su
/// clave ajena real contra su catálogo, que es la única defensa efectiva contra guardar un
/// identificador que no existe. Una tabla única con una columna «vocabulario» habría dejado las
/// cinco en una, pero a costa de esa garantía, y el proyecto prefiere la integridad declarada.
/// </para>
/// <para>
/// <b>Y POR QUE NO HAY UNA POR PROCESO.</b> Hasta las había: catorce
/// tablas —Festival, versión del perfil público, edición de festival, edición de mercado, por
/// cinco vocabularios— con exactamente la misma forma, <c>Id</c> + dueño + valor + fecha, sin un
/// solo campo propio. El dueño lo dijo así: se habían ido creando estructuras según iban haciendo
/// falta y varias se podían comprimir. Ahora el dueño viaja en <see cref="ModuloId"/> +
/// <see cref="RegistroId"/>, y el proceso que venga hereda las cinco relaciones sin escribir
/// ninguna.
/// </para>
/// </remarks>
public interface IClasificacionDeRegistro : IRelacionDeRegistro
{
    /// <summary>El identificador del valor dentro de su catálogo.</summary>
    int ValorId { get; set; }
}

/// <summary>
/// Cualquier fila que cuelga de un registro del Ecosistema sin nombrar su proceso.
/// </summary>
/// <remarks>
/// Es lo mínimo que comparten las ocho tablas de relación: de qué módulo es el dueño y cuál es su
/// clave. Tenerlo declarado permite que el ayudante que las lee y las escribe sea uno solo, y que
/// el filtro por módulo no se pueda olvidar en ninguna consulta.
/// </remarks>
public interface IRelacionDeRegistro
{
    long Id { get; set; }

    /// <summary>Uno de <see cref="Modulos.Clasificables"/>.</summary>
    string ModuloId { get; set; }

    /// <summary>El identificador del registro dentro de su módulo, como texto.</summary>
    string RegistroId { get; set; }

    DateTime FechaCreacion { get; set; }
}

/// <summary>Una práctica musical que congrega un registro.</summary>
public sealed class PracticaMusicalDeRegistroRow : IClasificacionDeRegistro
{
    public long Id { get; set; }
    public string ModuloId { get; set; } = string.Empty;
    public string RegistroId { get; set; } = string.Empty;
    public int ValorId { get; set; }
    public DateTime FechaCreacion { get; set; }
}

/// <summary>Un territorio sonoro donde un registro tiene lugar.</summary>
public sealed class TerritorioSonoroDeRegistroRow : IClasificacionDeRegistro
{
    public long Id { get; set; }
    public string ModuloId { get; set; } = string.Empty;
    public string RegistroId { get; set; } = string.Empty;
    public int ValorId { get; set; }
    public DateTime FechaCreacion { get; set; }
}

/// <summary>Una expresión artística que acompaña a un registro.</summary>
public sealed class ExpresionArtisticaDeRegistroRow : IClasificacionDeRegistro
{
    public long Id { get; set; }
    public string ModuloId { get; set; } = string.Empty;
    public string RegistroId { get; set; } = string.Empty;
    public int ValorId { get; set; }
    public DateTime FechaCreacion { get; set; }
}

/// <summary>Una modalidad con la que se participa en un registro.</summary>
public sealed class ModalidadParticipacionDeRegistroRow : IClasificacionDeRegistro
{
    public long Id { get; set; }
    public string ModuloId { get; set; } = string.Empty;
    public string RegistroId { get; set; } = string.Empty;
    public int ValorId { get; set; }
    public DateTime FechaCreacion { get; set; }
}

/// <summary>Una forma de ingreso del público a un registro.</summary>
public sealed class TipoIngresoDeRegistroRow : IClasificacionDeRegistro
{
    public long Id { get; set; }
    public string ModuloId { get; set; } = string.Empty;
    public string RegistroId { get; set; } = string.Empty;
    public int ValorId { get; set; }
    public DateTime FechaCreacion { get; set; }
}
