using System.Globalization;
using System.Text;
using PNMC.Contracts;
using PNMC.Domain.Entities;
using PNMC.Infrastructure.Common;

namespace PNMC.Api.ImportacionAsistida;

/// <summary>
/// Lo que cualquier dominio necesita para leer una fila de un archivo hecho por personas.
/// </summary>
/// <remarks>
/// <b>ESTABA ESCRITO DENTRO DEL PLANIFICADOR DE FESTIVALES.</b> Limpiar caracteres de control,
/// comparar sin tildes ni mayúsculas, resolver un territorio escrito a mano, normalizar un teléfono
/// con paréntesis: nada de eso es de Festivales, es de «leer un archivo». El segundo dominio lo
/// habría copiado, y a la tercera copia habría tres formas distintas de decidir si dos nombres son
/// el mismo.
/// </remarks>
public static class NormalizacionDeFilas
{
    /// <summary>Quita espacios y caracteres de control, conservando saltos de línea y tabuladores.</summary>
    public static string? Limpiar(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        return new string(valor.Trim()
            .Where(caracter => !char.IsControl(caracter) || caracter is '\n' or '\r' or '\t')
            .ToArray()).Trim();
    }

    /// <summary>
    /// El texto comparable: sin tildes, en minúsculas y con los espacios colapsados.
    /// </summary>
    /// <remarks>
    /// ES LO QUE DECIDE SI DOS NOMBRES SON EL MISMO, tanto al buscar un territorio como al detectar
    /// que dos filas del archivo traen el mismo registro. Escrito dos veces con criterios distintos,
    /// un dominio detectaría duplicados que el otro deja pasar.
    /// </remarks>
    public static string Comparable(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return string.Empty;
        var descompuesto = valor.Trim().Normalize(NormalizationForm.FormD);
        var letras = descompuesto.Where(caracter =>
            CharUnicodeInfo.GetUnicodeCategory(caracter) != UnicodeCategory.NonSpacingMark);
        return string.Join(' ', new string(letras.ToArray()).Normalize(NormalizationForm.FormC)
            .ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>Un correo sin espacios y en minúsculas.</summary>
    public static string? NormalizarCorreo(string? valor)
    {
        var limpio = Limpiar(valor);
        return limpio is null
            ? null
            : string.Concat(limpio.Where(caracter => !char.IsWhiteSpace(caracter))).ToLowerInvariant();
    }

    /// <summary>Un teléfono sin separadores de presentación.</summary>
    public static string? NormalizarTelefono(string? valor)
    {
        var limpio = Limpiar(valor);
        if (limpio is null) return null;
        var resultado = new string(limpio.Where(caracter => !" -.()".Contains(caracter, StringComparison.Ordinal)).ToArray());
        return resultado.Length == 0 ? null : resultado;
    }

    /// <summary>El valor de un campo de la fila, ya limpio.</summary>
    public static string? Campo(FilaDeArchivoDto fila, string nombre) =>
        Limpiar(fila.Valores.GetValueOrDefault(nombre));
}

/// <summary>
/// Recoge lo que se encontró al leer una fila: errores que la rechazan e información que la explica.
/// </summary>
/// <remarks>
/// <b>UN ERROR RECHAZA LA FILA; UNA INFORMACION NO.</b> Esa es toda la diferencia, y vive aquí para
/// que ningún dominio la reinvente al revés. «El correo se normalizó a minúsculas» es información
/// —la fila entra igual, y quien la revise sabe por qué el dato no es idéntico al del archivo—;
/// «el municipio no coincide con DIVIPOLA» es un error, porque importarlo guardaría un territorio
/// que no existe.
/// </remarks>
public sealed class Hallazgos
{
    private readonly List<HallazgoDeImportacionDto> _lista = [];

    public IReadOnlyList<HallazgoDeImportacionDto> Lista => _lista;

    /// <summary>Si la fila se puede importar: ningún hallazgo es un error.</summary>
    public bool SinErrores => _lista.All(item => item.Severidad != "error");

    public void Error(string codigo, string? campo, string mensaje) =>
        _lista.Add(new HallazgoDeImportacionDto("error", codigo, campo, mensaje));

    public void Info(string codigo, string? campo, string mensaje) =>
        _lista.Add(new HallazgoDeImportacionDto("informacion", codigo, campo, mensaje));

    public void Obligatorio(string? valor, string campo, string codigo, string mensaje)
    {
        if (string.IsNullOrWhiteSpace(valor)) Error(codigo, campo, mensaje);
    }

    public void Maximo(string? valor, int maximo, string campo)
    {
        if (valor?.Length > maximo)
            Error("longitud_excedida", campo, $"El campo supera el máximo de {maximo} caracteres.");
    }

    public void Url(string? valor, string campo)
    {
        if (!ValidationHelpers.IsValidHttpUrl(valor))
            Error("url_invalida", campo, "El enlace debe usar una dirección HTTP o HTTPS válida.");
        Maximo(valor, 500, campo);
    }

    public void Correo(string? valor, string campo)
    {
        if (!string.IsNullOrWhiteSpace(valor) && !ValidationHelpers.IsValidEmail(valor))
            Error("correo_invalido", campo, "El correo no tiene un formato válido.");
        Maximo(valor, 180, campo);
    }
}

/// <summary>Un territorio de DIVIPOLA, con el código que se guarda y el nombre que se lee.</summary>
public sealed record Territorio(string Codigo, string Nombre);

/// <summary>
/// Resuelve departamentos y municipios escritos a mano contra DIVIPOLA.
/// </summary>
/// <remarks>
/// <b>ADMITE CODIGO O NOMBRE, indistintamente.</b> Los archivos que llegan traen «05», «Antioquia»
/// o «ANTIOQUIA » según quién los hizo, y rechazar dos de las tres formas convertiría la
/// importación en un ejercicio de adivinar el formato.
/// <b>EL MUNICIPIO SE BUSCA DENTRO DE SU DEPARTAMENTO</b>, porque hay nombres repetidos en el país
/// y aceptar el primero que aparezca guardaría un municipio de otra región.
/// </remarks>
public sealed class TerritoriosDivipola
{
    private readonly Dictionary<string, Territorio> _departamentos = [];
    private readonly Dictionary<string, Dictionary<string, Territorio>> _municipios = [];

    public TerritoriosDivipola(IReadOnlyList<DivipolaLocationRow> filas)
    {
        foreach (var fila in filas)
        {
            var departamento = new Territorio(fila.DepartmentCode, fila.DepartmentName);
            _departamentos.TryAdd(NormalizacionDeFilas.Comparable(fila.DepartmentCode), departamento);
            _departamentos.TryAdd(NormalizacionDeFilas.Comparable(fila.DepartmentName), departamento);

            if (!_municipios.TryGetValue(fila.DepartmentCode, out var porNombre))
            {
                porNombre = [];
                _municipios[fila.DepartmentCode] = porNombre;
            }
            var municipio = new Territorio(fila.MunicipalityCode, fila.MunicipalityName);
            porNombre.TryAdd(NormalizacionDeFilas.Comparable(fila.MunicipalityCode), municipio);
            porNombre.TryAdd(NormalizacionDeFilas.Comparable(fila.MunicipalityName), municipio);
        }
    }

    public Territorio? Departamento(string? valor) =>
        _departamentos.GetValueOrDefault(NormalizacionDeFilas.Comparable(valor));

    public Territorio? Municipio(string? codigoDepartamento, string? valor) =>
        codigoDepartamento is not null && _municipios.TryGetValue(codigoDepartamento, out var porNombre)
            ? porNombre.GetValueOrDefault(NormalizacionDeFilas.Comparable(valor))
            : null;
}
