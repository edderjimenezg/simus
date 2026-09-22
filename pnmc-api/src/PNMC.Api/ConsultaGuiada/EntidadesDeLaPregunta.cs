namespace PNMC.Api.ConsultaGuiada;

/// <summary>Un territorio que la pregunta nombró, ya resuelto contra DIVIPOLA.</summary>
/// <param name="Codigo">El código DANE del departamento o del municipio, según lo que se nombró.</param>
/// <param name="Nombre">El nombre presentable, para escribirlo en la respuesta.</param>
/// <param name="EsDepartamento">Si lo nombrado fue un departamento entero o un municipio suyo.</param>
/// <param name="CodigoDelDepartamento">El departamento al que pertenece, también cuando es un municipio.</param>
/// <param name="NombreDelDepartamento">Cómo se llama ese departamento, para situar el municipio.</param>
/// <remarks>
/// <b>NO GUARDA LO QUE SE ESCRIBIO, SINO LO QUE SE RESOLVIO.</b> Lo que viaja es el código del DANE,
/// no el texto de la pregunta: así la consulta acota comparando códigos, que es como están guardados
/// los Festivales, y la respuesta escribe el nombre oficial y no la forma en que alguien lo tecleó.
/// </remarks>
public sealed record TerritorioNombrado(
    string Codigo,
    string Nombre,
    bool EsDepartamento,
    string CodigoDelDepartamento,
    string NombreDelDepartamento)
{
    /// <summary>Si un registro con esos códigos cae dentro del territorio nombrado.</summary>
    public bool Cubre(string? codigoDepartamento, string? codigoMunicipio) =>
        EsDepartamento
            ? string.Equals(codigoDepartamento, Codigo, StringComparison.Ordinal)
            : string.Equals(codigoMunicipio, Codigo, StringComparison.Ordinal);

    /// <summary>Cómo se nombra dentro de una frase: «Huila», «Ibagué (Tolima)».</summary>
    public string EnPalabras => EsDepartamento ? Nombre : $"{Nombre} ({NombreDelDepartamento})";
}

/// <summary>Un estado del circuito que la pregunta nombra, resuelto contra el catálogo.</summary>
/// <remarks>
/// <b>EL CODIGO ES EL DE <c>EstadosContenido</c> Y EL NOMBRE SALE DE ALLI TAMBIEN.</b> Escribir aquí
/// «en revisión» sería empezar una segunda lista de estados, que es exactamente el problema que
/// <c>EstadosFestival</c> documenta: el circuito llegó a tener dos vocabularios y dos de los estados
/// eran imposibles de escribir en la base.
/// </remarks>
public sealed record EstadoNombrado(string Codigo, string Nombre, bool Negado = false)
{
    /// <summary>
    /// Cómo se nombra dentro de una frase que habla de una cifra concreta.
    /// </summary>
    /// <remarks>
    /// <b>NECESITA LA CIFRA PORQUE EL VERBO CONCUERDA CON ELLA.</b> Al preguntárselo a la API
    /// levantada salió «Hay 1 Festival que no están en estado «Publicado» en Huila»: un territorio
    /// con un solo registro pendiente es el caso corriente, no el raro, y la frase se leía como un
    /// error del sistema.
    /// </remarks>
    public string Frase(int cuantos) => Negado
        ? cuantos == 1 ? $"que no está en estado «{Nombre}»" : $"que no están en estado «{Nombre}»"
        : $"en estado «{Nombre}»";

    /// <summary>
    /// Lo mismo sin verbo, para encabezar una tabla o declarar un alcance.
    /// </summary>
    /// <remarks>
    /// <para>
    /// SIN VERBO A PROPOSITO: un título no tiene una cifra al lado con la que concordar, y escribir
    /// «Festivales que no están en estado X» encima de una tabla de una fila volvería a chirriar.
    /// </para>
    /// <para>
    /// <b>Y CON SU PREPOSICION DENTRO</b>, porque la que le toca cambia con la negación: «en estado
    /// X» y «fuera del estado X». Dejarla fuera producía «Festivales en fuera del estado
    /// «Publicado»», que es lo que llegó a la pantalla la primera vez.
    /// </para>
    /// </remarks>
    public string EnPalabras => Negado ? $"fuera del estado «{Nombre}»" : $"en estado «{Nombre}»";

    /// <summary>Si el registro guardado con ese código entra en lo que se preguntó.</summary>
    public bool Incluye(string? codigoGuardado)
    {
        var casa = string.Equals(codigoGuardado, Codigo, StringComparison.OrdinalIgnoreCase);
        return Negado ? !casa : casa;
    }
}

/// <summary>
/// Los estados del circuito preparados para reconocerlos dentro de una pregunta.
/// </summary>
/// <remarks>
/// <b>SOLO LOS DEL CIRCUITO DE UN FESTIVAL, y eso descarta un choque real.</b> El catálogo
/// <c>EstadosContenido</c> comparte tabla con las organizaciones y las suscripciones, así que
/// contiene «Registrada», «Activa» o «Pendiente de confirmación». Aceptar «Registrada» habría hecho
/// que «¿cuántos festivales hay registrados?» —que significa «en todos los estados»— se acotara al
/// estado de una entidad.
/// </remarks>
public sealed class VocabularioDeEstados
{
    /// <summary>El vocabulario de quien todavía no ha leído el catálogo. No reconoce nada.</summary>
    public static readonly VocabularioDeEstados Vacio = Construir([]);

    private readonly List<(IReadOnlyList<string> Palabras, EstadoNombrado Estado)> _estados;

    private VocabularioDeEstados(List<(IReadOnlyList<string>, EstadoNombrado)> estados) => _estados = estados;

    /// <summary>Un estado del catálogo, con su código y su nombre tal como está guardado.</summary>
    public readonly record struct FilaDeEstado(string Codigo, string Nombre);

    /// <summary>Prepara el vocabulario, de nombre más largo a más corto.</summary>
    public static VocabularioDeEstados Construir(IEnumerable<FilaDeEstado> filas)
    {
        var estados = filas
            .Select(fila => (
                Palabras: CatalogoDeConsultas.PalabrasDe(CatalogoDeConsultas.Normalizar(fila.Nombre)),
                Estado: new EstadoNombrado(fila.Codigo, fila.Nombre)))
            .Where(par => par.Palabras.Count > 0)
            .OrderByDescending(par => par.Palabras.Count)
            .Select(par => ((IReadOnlyList<string>)par.Palabras, par.Estado))
            .ToList();
        return new VocabularioDeEstados(estados);
    }

    /// <summary>
    /// El estado que la pregunta nombra, si nombra alguno.
    /// </summary>
    /// <remarks>
    /// <b>AQUI SI SE COMPARA COMO EN LAS PISTAS</b>, con raíz y plurales, y no exacto como en los
    /// territorios: «publicados», «publicadas», «publicado» y «publicar» son la misma palabra, y
    /// equivocarse entre dos estados no es como equivocarse entre dos municipios —hay cinco, se leen
    /// en la respuesta y no hay ninguno que se parezca a otro—.
    /// </remarks>
    public EstadoNombrado? Resolver(IReadOnlyList<string> palabras)
    {
        foreach (var (delEstado, estado) in _estados)
        {
            for (var i = 0; i + delEstado.Count <= palabras.Count; i++)
            {
                var casan = true;
                for (var j = 0; j < delEstado.Count && casan; j++)
                {
                    casan = CatalogoDeConsultas.PalabraCasa(delEstado[j], palabras[i + j]);
                }
                if (casan) { return estado with { Negado = EstaNegado(palabras, i) }; }
            }
        }
        return null;
    }

    /// <summary>Palabras que niegan lo que viene detrás.</summary>
    private static readonly HashSet<string> Negaciones = new(StringComparer.Ordinal) { "sin", "no" };

    /// <summary>Verbos de enlace que pueden meterse entre la negación y el estado.</summary>
    /// <remarks>
    /// «QUE NO ESTEN ARCHIVADOS» ES LA FORMA CORRIENTE DE PEDIRLO, y entre el «no» y el estado
    /// cabe un verbo. Sin esta lista corta, la negación solo funcionaría pegada —«no archivados»—,
    /// que es como casi nadie escribe.
    /// </remarks>
    private static readonly HashSet<string> Enlaces =
        new(StringComparer.Ordinal) { "esten", "estan", "esta", "este", "son", "sean", "sea", "es", "tienen", "tiene", "hayan", "haya" };

    /// <summary>
    /// Si lo que hay justo antes del estado lo niega.
    /// </summary>
    /// <remarks>
    /// <b>SOLO MIRA DOS PALABRAS ATRAS, y esa cortedad es deliberada.</b> Un «no» al principio de la
    /// frase no niega un estado que aparece seis palabras después —«¿por qué no me deja publicar un
    /// festival archivado?» no pregunta por lo que no está archivado—. La negación en castellano va
    /// pegada a lo que niega, con un verbo de enlace como mucho en medio.
    /// </remarks>
    private static bool EstaNegado(IReadOnlyList<string> palabras, int inicio)
    {
        if (inicio == 0) { return false; }
        if (Negaciones.Contains(palabras[inicio - 1])) { return true; }
        return inicio >= 2 && Enlaces.Contains(palabras[inicio - 1]) && Negaciones.Contains(palabras[inicio - 2]);
    }
}

/// <summary>
/// Lo que la pregunta nombra además de su asunto: dónde, cuándo y en qué estado.
/// </summary>
/// <remarks>
/// <para>
/// <b>POR QUE SE EXTRAEN APARTE DE LAS PISTAS.</b> «¿Cuántos festivales hay en el Huila?» no es una
/// pregunta distinta de «¿cuántos festivales hay?»: es la misma acotada. Tratar «Huila» como una
/// pista más obligaría a escribir mil ciento veintidós pistas —una por municipio— y aun así la
/// respuesta seguiría siendo la del país entero, porque una pista solo elige consulta y no acota
/// nada. Lo que hace falta es al revés: reconocer la entidad y <b>pasársela a la consulta</b>.
/// </para>
/// <para>
/// <b>ES LO QUE MAS DAÑO HACIA SIN ELLO.</b> Medido contra el banco de
/// preguntas: «¿Cuántos festivales hay publicados en el Huila?» recibía el resumen administrativo
/// del país entero, con su tabla y su fuente, que es la peor forma de equivocarse —una cifra
/// presentada como respuesta a una pregunta que no se hizo—.
/// </para>
/// </remarks>
/// <param name="Territorio">El departamento o municipio nombrado, o <c>null</c> si no se nombró ninguno.</param>
/// <param name="Anio">El año nombrado, o <c>null</c>.</param>
/// <param name="Estado">El estado del circuito nombrado, o <c>null</c>.</param>
/// <param name="TerritorioAmbiguo">
/// El nombre de un territorio que existe en varios departamentos y por eso no se pudo resolver.
/// Se conserva para poder decirlo: callarlo sería contestar del país entero a quien nombró un sitio.
/// </param>
public sealed record EntidadesDeLaPregunta(
    TerritorioNombrado? Territorio,
    int? Anio,
    string? TerritorioAmbiguo,
    EstadoNombrado? Estado = null)
{
    /// <summary>Cuando la pregunta no nombra nada, o cuando no hay que mirarlo.</summary>
    public static readonly EntidadesDeLaPregunta Ninguna = new(null, null, null);
}

/// <summary>
/// Los nombres territoriales del DANE preparados para buscarlos dentro de una pregunta.
/// </summary>
/// <remarks>
/// <para>
/// <b>SE CONSTRUYE DESDE DIVIPOLA Y NUNCA DESDE UNA LISTA ESCRITA A MANO.</b> Escribir aquí los
/// treinta y tres departamentos sería rápido y sería exactamente el vestigio que el proyecto
/// persigue: una segunda copia de la división político-administrativa que se separa de la primera
/// en cuanto el DANE cambie algo. La construye <c>CacheDivipola</c> con la misma lectura que ya
/// hacía para el geovisor, así que no cuesta ni una consulta más.
/// </para>
/// <para>
/// <b>UN NOMBRE AMBIGUO NO SE RESUELVE, SE DECLARA.</b> Sesenta y seis nombres de municipio se
/// repiten en varios departamentos —medido sobre <c>PNMC_LOCAL</c>—, y
/// «San Andrés» es dos municipios distintos. Elegir uno por orden de código sería inventarse la
/// pregunta; quedan marcados como ambiguos para poder decir que existen en varios sitios.
/// </para>
/// <para>
/// <b>EL DEPARTAMENTO GANA AL MUNICIPIO DEL MISMO NOMBRE.</b> «Sucre», «Santander» o «Córdoba» son
/// las dos cosas. Quien escribe «en Córdoba» en una consola nacional casi siempre habla del
/// departamento, y contestar del departamento incluye al municipio; al revés no.
/// </para>
/// </remarks>
public sealed class VocabularioTerritorial
{
    /// <summary>El vocabulario de quien todavía no ha cargado DIVIPOLA. No reconoce nada.</summary>
    public static readonly VocabularioTerritorial Vacio = Construir([]);

    private readonly Dictionary<string, TerritorioNombrado> _porNombre;
    private readonly HashSet<string> _ambiguos;
    private readonly Dictionary<string, List<MunicipioDelDepartamento>> _municipiosPorDepartamento;

    private VocabularioTerritorial(
        Dictionary<string, TerritorioNombrado> porNombre,
        HashSet<string> ambiguos,
        Dictionary<string, List<MunicipioDelDepartamento>> municipiosPorDepartamento,
        int maximoPalabras)
    {
        _porNombre = porNombre;
        _ambiguos = ambiguos;
        _municipiosPorDepartamento = municipiosPorDepartamento;
        MaximoPalabras = maximoPalabras;
    }

    /// <summary>Cuántas palabras mide el nombre más largo. Acota la ventana de búsqueda.</summary>
    public int MaximoPalabras { get; }

    /// <summary>Si el vocabulario está cargado. Vacío significa que DIVIPOLA todavía no se leyó.</summary>
    public bool HayDatos => _porNombre.Count > 0;

    /// <summary>Un municipio dentro de la lista de los de su departamento.</summary>
    public readonly record struct MunicipioDelDepartamento(string Codigo, string Nombre);

    /// <summary>
    /// Todos los municipios de un departamento, para medir su cobertura y sus vacíos.
    /// </summary>
    /// <remarks>
    /// AQUI SI ESTAN LOS DE NOMBRE REPETIDO. La ambigüedad estorba para resolver un nombre escrito
    /// —«San Andrés» no dice cuál de los dos—, pero no para listar los municipios de un
    /// departamento, donde cada uno aparece una sola vez y con su código. Dejarlos fuera habría
    /// hecho que la cobertura de un departamento se midiera contra menos municipios de los que
    /// tiene, que es un error silencioso y a favor.
    /// </remarks>
    public IReadOnlyList<MunicipioDelDepartamento> MunicipiosDe(string codigoDepartamento) =>
        _municipiosPorDepartamento.GetValueOrDefault(codigoDepartamento) ?? [];

    /// <summary>El territorio que se llama así, si solo hay uno.</summary>
    public TerritorioNombrado? Resolver(string nombreNormalizado) =>
        _porNombre.GetValueOrDefault(nombreNormalizado);

    /// <summary>Si ese nombre existe en varios departamentos y por eso no se resuelve.</summary>
    public bool EsAmbiguo(string nombreNormalizado) => _ambiguos.Contains(nombreNormalizado);

    /// <summary>Una fila de DIVIPOLA con los nombres ya presentables.</summary>
    public readonly record struct FilaTerritorial(
        string CodigoDepartamento,
        string NombreDepartamento,
        string CodigoMunicipio,
        string NombreMunicipio);

    /// <summary>Prepara el vocabulario a partir de las filas de DIVIPOLA.</summary>
    public static VocabularioTerritorial Construir(IEnumerable<FilaTerritorial> filas)
    {
        var lista = filas.ToList();
        var porNombre = new Dictionary<string, TerritorioNombrado>(StringComparer.Ordinal);
        var ambiguos = new HashSet<string>(StringComparer.Ordinal);
        var municipiosPorDepartamento = new Dictionary<string, List<MunicipioDelDepartamento>>(StringComparer.Ordinal);

        // LOS DEPARTAMENTOS PRIMERO, para que ganen el nombre compartido sin necesidad de compararlo.
        foreach (var grupo in lista.GroupBy(fila => fila.CodigoDepartamento))
        {
            var nombre = grupo.First().NombreDepartamento;
            var territorio = new TerritorioNombrado(grupo.Key, nombre, true, grupo.Key, nombre);
            foreach (var clave in ClavesDe(nombre))
            {
                porNombre[clave] = territorio;
            }
            municipiosPorDepartamento[grupo.Key] = grupo
                .GroupBy(fila => fila.CodigoMunicipio)
                .Select(municipio => new MunicipioDelDepartamento(municipio.Key, municipio.First().NombreMunicipio))
                .ToList();
        }

        foreach (var grupo in lista.GroupBy(fila => fila.CodigoMunicipio))
        {
            var fila = grupo.First();
            var clave = ClaveDe(fila.NombreMunicipio);
            if (clave.Length == 0) { continue; }

            // UN NOMBRE QUE YA ES DE UN DEPARTAMENTO SE QUEDA CON EL DEPARTAMENTO, y eso no es una
            // ambigüedad que declarar: es la regla, y declararla como duda haría que «en Córdoba»
            // dejara de responderse.
            if (porNombre.TryGetValue(clave, out var ocupado) && ocupado.EsDepartamento) { continue; }

            if (porNombre.ContainsKey(clave) || ambiguos.Contains(clave))
            {
                porNombre.Remove(clave);
                ambiguos.Add(clave);
                continue;
            }

            porNombre[clave] = new TerritorioNombrado(
                grupo.Key, fila.NombreMunicipio, false, fila.CodigoDepartamento, fila.NombreDepartamento);
        }

        var maximo = porNombre.Keys.Concat(ambiguos)
            .Select(clave => clave.Count(caracter => caracter == ' ') + 1)
            .DefaultIfEmpty(1)
            .Max();

        return new VocabularioTerritorial(porNombre, ambiguos, municipiosPorDepartamento, maximo);
    }

    /// <summary>
    /// Con qué nombres se busca un departamento: el suyo entero y el trozo anterior a la coma.
    /// </summary>
    /// <remarks>
    /// EL TROZO ANTERIOR A LA COMA NO ES UN APODO INVENTADO, es el nombre corriente que el propio
    /// dato trae dentro: «BOGOTÁ, D.C.» se pregunta siempre como «Bogotá». Sale del dato y no de una
    /// tabla de sinónimos, que es lo que la haría envejecer.
    /// </remarks>
    private static IEnumerable<string> ClavesDe(string nombre)
    {
        var entero = ClaveDe(nombre);
        if (entero.Length > 0) { yield return entero; }

        var coma = nombre.IndexOf(',', StringComparison.Ordinal);
        if (coma <= 0) { yield break; }

        var corto = ClaveDe(nombre[..coma]);
        if (corto.Length > 0 && !string.Equals(corto, entero, StringComparison.Ordinal)) { yield return corto; }
    }

    private static string ClaveDe(string nombre) =>
        string.Join(' ', CatalogoDeConsultas.PalabrasDe(CatalogoDeConsultas.Normalizar(nombre)));
}

/// <summary>
/// Quien reconoce el sitio y el año dentro de una pregunta escrita.
/// </summary>
/// <remarks>
/// <para>
/// <b>EXIGE UNA PREPOSICION DELANTE, Y ESA ES SU DEFENSA PRINCIPAL.</b> En castellano el sitio se
/// introduce con una preposición —«en el Huila», «de Nariño», «del Valle del Cauca»—, y exigirla
/// convierte mil ciento veintidós nombres de municipio en algo que no puede saltar por accidente en
/// medio de una frase. Sin esa condición, cualquier pregunta que contuviera una palabra que también
/// es el nombre de un pueblo habría dejado de responderse, y lo habría hecho en silencio.
/// </para>
/// <para>
/// <b>Y COMPARA EL NOMBRE EXACTO, sin la tolerancia a erratas del resto del intérprete.</b> Ahí una
/// letra de diferencia ayuda —«festivles» sigue siendo «festivales»—; aquí convertiría un pueblo en
/// otro pueblo distinto, y el daño no se ve: la respuesta llega con su tabla y su fuente, de un
/// sitio que nadie preguntó.
/// </para>
/// <para>
/// <b>EL AÑO NO ELIGE CONSULTA, SOLO LA ACOTA.</b> Nombrar un sitio cambia qué tabla contesta;
/// nombrar un año es la misma pregunta mirada por una ventana. Por eso el año no compite con las
/// pistas, y cuando la consulta elegida no sabe acotarse por él, la respuesta lo dice en vez de
/// ignorarlo.
/// </para>
/// </remarks>
public static class ExtractorDeEntidades
{
    /// <summary>Lo que introduce un sitio en castellano.</summary>
    private static readonly HashSet<string> Preposiciones =
        new(StringComparer.Ordinal) { "en", "de", "del", "desde", "para", "hacia", "sobre" };

    /// <summary>Artículos que pueden ir entre la preposición y el nombre, o formar parte de él.</summary>
    private static readonly HashSet<string> Articulos =
        new(StringComparer.Ordinal) { "el", "la", "los", "las" };

    /// <summary>Desde cuándo un número de cuatro cifras es un año y no un código.</summary>
    private const int PrimerAnioCreible = 1900;

    /// <summary>Cuántos años por delante del actual se siguen aceptando: se programan ediciones futuras.</summary>
    private const int AnosPorDelante = 5;

    /// <summary>Lo que la pregunta nombra además de su asunto.</summary>
    public static EntidadesDeLaPregunta Extraer(
        IReadOnlyList<string> palabras,
        VocabularioTerritorial territorios,
        int anioActual,
        VocabularioDeEstados? estados = null)
    {
        var (territorio, ambiguo) = BuscarTerritorio(palabras, territorios);
        return new EntidadesDeLaPregunta(
            territorio,
            BuscarAnio(palabras, anioActual),
            ambiguo,
            (estados ?? VocabularioDeEstados.Vacio).Resolver(palabras));
    }

    private static (TerritorioNombrado? Territorio, string? Ambiguo) BuscarTerritorio(
        IReadOnlyList<string> palabras, VocabularioTerritorial territorios)
    {
        if (!territorios.HayDatos) { return (null, null); }

        for (var i = 0; i < palabras.Count - 1; i++)
        {
            if (!Preposiciones.Contains(palabras[i])) { continue; }

            // SE PRUEBA CON EL ARTICULO DENTRO Y FUERA, en ese orden. «La Guajira» lleva el artículo
            // en su nombre y «el Huila» no lo lleva en ninguna parte; probar primero el nombre largo
            // hace que la primera no se pierda y la segunda no sobre.
            foreach (var inicio in Comienzos(palabras, i + 1))
            {
                var encontrado = DesdeAqui(palabras, inicio, territorios);
                if (encontrado.Territorio is not null || encontrado.Ambiguo is not null) { return encontrado; }
            }
        }

        return (null, null);
    }

    private static IEnumerable<int> Comienzos(IReadOnlyList<string> palabras, int inicio)
    {
        if (inicio >= palabras.Count) { yield break; }
        yield return inicio;
        if (Articulos.Contains(palabras[inicio]) && inicio + 1 < palabras.Count) { yield return inicio + 1; }
    }

    /// <summary>El nombre más largo que empieza justo ahí, porque «Valle» no es «Valle del Cauca».</summary>
    private static (TerritorioNombrado? Territorio, string? Ambiguo) DesdeAqui(
        IReadOnlyList<string> palabras, int inicio, VocabularioTerritorial territorios)
    {
        var maximo = Math.Min(territorios.MaximoPalabras, palabras.Count - inicio);
        for (var largo = maximo; largo >= 1; largo--)
        {
            var clave = string.Join(' ', palabras.Skip(inicio).Take(largo));
            var territorio = territorios.Resolver(clave);
            if (territorio is not null) { return (territorio, null); }
            if (territorios.EsAmbiguo(clave)) { return (null, clave); }
        }
        return (null, null);
    }

    private static int? BuscarAnio(IReadOnlyList<string> palabras, int anioActual)
    {
        for (var i = 0; i < palabras.Count; i++)
        {
            var palabra = palabras[i];

            if (palabra.Length == 4 && palabra.All(char.IsDigit)
                && int.TryParse(palabra, out var escrito)
                && escrito >= PrimerAnioCreible && escrito <= anioActual + AnosPorDelante)
            {
                return escrito;
            }

            // «AÑO» LLEGA AQUI COMO «ANO»: la normalización quita la tilde de la eñe igual que las
            // demás, y eso está bien mientras se compare siempre contra texto normalizado.
            if (!string.Equals(palabra, "ano", StringComparison.Ordinal)) { continue; }
            if (i > 0 && string.Equals(palabras[i - 1], "este", StringComparison.Ordinal)) { return anioActual; }
            if (i + 1 < palabras.Count && string.Equals(palabras[i + 1], "pasado", StringComparison.Ordinal))
            {
                return anioActual - 1;
            }
        }

        return null;
    }
}
