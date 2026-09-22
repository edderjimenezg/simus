using PNMC.Api.ConsultaGuiada;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// Lo que el intérprete reconoce dentro de una pregunta: el sitio y el año.
/// </summary>
/// <remarks>
/// <para>
/// <b>SE PRUEBA AQUI Y NO CONTRA EL EXTREMO HTTP</b> porque lo que hay que fijar son las reglas de
/// reconocimiento, y para eso hace falta una DIVIPOLA con nombres repetidos, con artículo dentro y
/// con coma. La semilla de la fábrica de pruebas tiene dos municipios: sembrarle más movería la
/// cuenta de municipios de otras pruebas para comprobar algo que no es de ellas.
/// </para>
/// <para>
/// <b>LAS FILAS SON REALES</b>, copiadas de <c>PNMC_LOCAL</c> —códigos, nombres y la repetición de
/// «San Andrés» en Santander y en el Archipiélago—. En producción el vocabulario lo construye
/// <c>CacheDivipola</c> leyendo la tabla entera, que es la única fuente del proyecto.
/// </para>
/// </remarks>
public sealed class EntidadesDeLaPreguntaTests
{
    private static readonly VocabularioTerritorial Vocabulario = VocabularioTerritorial.Construir(
    [
        new("41", "Huila", "41001", "Neiva"),
        new("41", "Huila", "41551", "Pitalito"),
        new("44", "La Guajira", "44001", "Riohacha"),
        new("76", "Valle del Cauca", "76001", "Cali"),
        new("11", "Bogotá, D.C.", "11001", "Bogotá, D.C."),
        new("73", "Tolima", "73001", "Ibagué"),
        new("68", "Santander", "68001", "Bucaramanga"),
        new("68", "Santander", "68669", "San Andrés"),
        new("88", "Archipiélago de San Andrés, Providencia y Santa Catalina", "88001", "San Andrés"),
    ]);

    private const int AnioDeReferencia = 2026;

    private static EntidadesDeLaPregunta Leer(string pregunta) =>
        ExtractorDeEntidades.Extraer(
            CatalogoDeConsultas.PalabrasDe(CatalogoDeConsultas.Normalizar(pregunta)),
            Vocabulario,
            AnioDeReferencia);

    [Theory]
    [InlineData("¿cuántos festivales hay en el Huila?", "41")]
    [InlineData("festivales de Huila", "41")]
    [InlineData("festivales en La Guajira", "44")]
    [InlineData("municipios del Valle del Cauca", "76")]
    [InlineData("qué pasa en Bogotá", "11")]
    public void Reconoce_el_departamento_que_la_pregunta_nombra(string pregunta, string codigo)
    {
        var entidades = Leer(pregunta);

        Assert.NotNull(entidades.Territorio);
        Assert.Equal(codigo, entidades.Territorio.Codigo);
        Assert.True(entidades.Territorio.EsDepartamento);
    }

    /// <summary>
    /// «Valle» no es «Valle del Cauca»: gana siempre el nombre más largo que empieza ahí.
    /// </summary>
    [Fact]
    public void Se_queda_con_el_nombre_mas_largo()
    {
        var entidades = Leer("cuántos festivales hay en el Valle del Cauca");

        Assert.NotNull(entidades.Territorio);
        Assert.Equal("76", entidades.Territorio.Codigo);
    }

    [Fact]
    public void Reconoce_un_municipio_y_sabe_de_que_departamento_es()
    {
        var entidades = Leer("¿cuántos festivales hay en Ibagué?");

        Assert.NotNull(entidades.Territorio);
        Assert.Equal("73001", entidades.Territorio.Codigo);
        Assert.False(entidades.Territorio.EsDepartamento);
        Assert.Equal("73", entidades.Territorio.CodigoDelDepartamento);
        Assert.Equal("Ibagué (Tolima)", entidades.Territorio.EnPalabras);
    }

    /// <summary>
    /// Un nombre que está en dos departamentos no se resuelve: se declara.
    /// </summary>
    /// <remarks>
    /// ELEGIR EL PRIMERO POR ORDEN DE CODIGO SERIA INVENTARSE LA PREGUNTA, y la respuesta llegaría
    /// con su tabla y su fuente hablando de un sitio que nadie preguntó.
    /// </remarks>
    [Fact]
    public void Un_nombre_repetido_en_varios_departamentos_no_se_resuelve()
    {
        var entidades = Leer("cuántos festivales hay en San Andrés");

        Assert.Null(entidades.Territorio);
        Assert.Equal("san andres", entidades.TerritorioAmbiguo);
    }

    /// <summary>
    /// El departamento gana al municipio que se llama igual.
    /// </summary>
    [Fact]
    public void El_departamento_gana_al_municipio_homonimo()
    {
        var entidades = Leer("festivales en Santander");

        Assert.NotNull(entidades.Territorio);
        Assert.Equal("68", entidades.Territorio.Codigo);
        Assert.True(entidades.Territorio.EsDepartamento);
    }

    /// <summary>
    /// Sin preposición delante, un nombre de sitio no se reconoce.
    /// </summary>
    /// <remarks>
    /// <b>ES LA DEFENSA PRINCIPAL DEL RECONOCEDOR, y por eso se fija con una prueba.</b> Con mil
    /// ciento veintidós nombres de municipio, cualquier palabra corriente que además sea el nombre
    /// de un pueblo dejaría de responderse en silencio si bastara con que apareciera.
    /// </remarks>
    [Theory]
    [InlineData("cuántos festivales hay")]
    [InlineData("Huila")]
    [InlineData("resumen de la plataforma")]
    [InlineData("¿qué está pendiente de revisar?")]
    public void Sin_preposicion_delante_no_hay_territorio(string pregunta)
    {
        Assert.Null(Leer(pregunta).Territorio);
    }

    /// <summary>
    /// Los nombres de sitio se comparan exactos, sin la tolerancia a erratas del intérprete.
    /// </summary>
    /// <remarks>
    /// EN LAS PISTAS UNA LETRA DE DIFERENCIA AYUDA —«festivles» sigue siendo «festivales»—; aquí
    /// convertiría un municipio en otro municipio, y el daño no se vería.
    /// </remarks>
    [Fact]
    public void Un_nombre_de_sitio_mal_escrito_no_se_adivina()
    {
        Assert.Null(Leer("cuántos festivales hay en Huilla").Territorio);
    }

    private static readonly VocabularioDeEstados Estados = VocabularioDeEstados.Construir(
    [
        new("borrador", "Borrador"),
        new("en_revision", "En revisión"),
        new("ajustes_solicitados", "Ajustes solicitados"),
        new("rechazado", "Rechazado"),
        new("publicado", "Publicado"),
        new("archivado", "Archivado"),
    ]);

    private static EstadoNombrado? Estado(string pregunta) =>
        Estados.Resolver(CatalogoDeConsultas.PalabrasDe(CatalogoDeConsultas.Normalizar(pregunta)));

    [Theory]
    [InlineData("¿cuántos festivales hay en revisión?", "en_revision")]
    [InlineData("cuántos borradores hay", "borrador")]
    [InlineData("festivales publicados", "publicado")]
    [InlineData("festivales con ajustes solicitados", "ajustes_solicitados")]
    public void Reconoce_el_estado_que_la_pregunta_nombra(string pregunta, string codigo)
    {
        var estado = Estado(pregunta);

        Assert.NotNull(estado);
        Assert.Equal(codigo, estado.Codigo);
        Assert.False(estado.Negado);
    }

    /// <summary>
    /// «Sin publicar» es un estado negado, y «publicar» es «publicado».
    /// </summary>
    /// <remarks>
    /// <b>LAS DOS COSAS TUVIERON QUE ENTRAR JUNTAS.</b> La negación sola no habría servido de nada
    /// mientras el reconocedor de raíces no supiera que «publicar» y «publicado» son la misma
    /// palabra: «sin publicar» no alcanzaba ningún estado, y es la forma más corriente de preguntar
    /// por lo que falta por publicar.
    /// </remarks>
    [Theory]
    [InlineData("¿cuántos festivales hay sin publicar?")]
    [InlineData("festivales no publicados")]
    [InlineData("¿qué festivales no están publicados?")]
    public void Reconoce_un_estado_negado(string pregunta)
    {
        var estado = Estado(pregunta);

        Assert.NotNull(estado);
        Assert.Equal("publicado", estado.Codigo);
        Assert.True(estado.Negado);
        Assert.True(estado.Incluye("borrador"));
        Assert.False(estado.Incluye("publicado"));
    }

    /// <summary>
    /// Un «no» lejano no niega el estado.
    /// </summary>
    /// <remarks>
    /// LA NEGACION EN CASTELLANO VA PEGADA A LO QUE NIEGA, con un verbo de enlace como mucho en
    /// medio. Mirar la frase entera convertiría cualquier pregunta con un «no» al principio en una
    /// pregunta por lo contrario de lo que dice.
    /// </remarks>
    [Fact]
    public void Un_no_lejano_no_niega_el_estado()
    {
        var estado = Estado("no entiendo por qué me deja publicar un festival archivado");

        Assert.NotNull(estado);
        Assert.False(estado.Negado);
    }

    [Theory]
    [InlineData("¿cuántas ediciones hubo en 2024?", 2024)]
    [InlineData("ediciones de este año", AnioDeReferencia)]
    [InlineData("ediciones del año pasado", AnioDeReferencia - 1)]
    public void Reconoce_el_ano_escrito_y_el_dicho(string pregunta, int esperado)
    {
        Assert.Equal(esperado, Leer(pregunta).Anio);
    }

    /// <summary>
    /// Un número de cuatro cifras que no puede ser un año no lo es.
    /// </summary>
    [Theory]
    [InlineData("ediciones de 1024")]
    [InlineData("el festival 4100")]
    public void Un_numero_cualquiera_no_es_un_ano(string pregunta)
    {
        Assert.Null(Leer(pregunta).Anio);
    }

    /// <summary>
    /// Sin DIVIPOLA cargada no se reconoce ningún sitio, y el año se sigue leyendo.
    /// </summary>
    /// <remarks>
    /// IMPORTA PORQUE PASA: DIVIPOLA se siembra con un guion aparte y el orden habitual en local es
    /// levantar el API antes de sembrarla. Lo que no puede ocurrir es que la Consulta Guiada
    /// reviente por eso.
    /// </remarks>
    [Fact]
    public void Sin_vocabulario_cargado_no_se_inventa_un_territorio()
    {
        var entidades = ExtractorDeEntidades.Extraer(
            CatalogoDeConsultas.PalabrasDe(CatalogoDeConsultas.Normalizar("festivales en el Huila en 2024")),
            VocabularioTerritorial.Vacio,
            AnioDeReferencia);

        Assert.Null(entidades.Territorio);
        Assert.Null(entidades.TerritorioAmbiguo);
        Assert.Equal(2024, entidades.Anio);
    }
}
