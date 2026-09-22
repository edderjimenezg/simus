using PNMC.Infrastructure.Data;
using Xunit;
using Xunit.Abstractions;

namespace PNMC.Api.Tests;

/// <summary>
/// Cuánto acierta la lectura de documentos, medido y no supuesto.
/// </summary>
/// <remarks>
/// <para>
/// <b>POR QUE ESTA CLASE EXISTE.</b> Una herramienta que propone datos a quien cataloga tiene que
/// poder decir <b>cuánto acierta</b>, con un número. Sin eso, «funciona» significa «funcionó con el
/// documento que probé», y la decisión de si hace falta un modelo de lenguaje se toma por intuición.
/// </para>
/// <para>
/// <b>LAS PORTADAS SON SINTETICAS Y LOS DATOS SON REALES.</b> Se maquetan con títulos, subtítulos,
/// autorías e ISBN tomados del acervo del Catálogo Editorial, con la disposición medida en documentos
/// auténticos. Lo que se mide es si la tubería recupera valores conocidos; lo que NO se mide es el
/// comportamiento ante una portada escaneada o una maquetación insólita, y eso hay que decirlo para
/// que una puntuación alta no se lea como una garantía que no es.
/// </para>
/// </remarks>
public sealed class PuntuacionDeLaLecturaTests(ITestOutputHelper salida)
{
    /// <summary>
    /// Fichas del acervo real: título, subtítulo, autoría, ISBN y año tal como están catalogados.
    /// </summary>
    /// <remarks>
    /// LOS ISBN SON VALIDOS DE VERDAD, con su dígito de control calculado. La primera versión de esta
    /// clase usó dos inventados y la puntuación bajó a 3 de 5: el lector los rechazaba con razón, y el
    /// fallo estaba en la ficha de ejemplo. Una prueba que mide con datos falsos mide su propio error.
    /// </remarks>
    private static readonly PortadasDePrueba.Ficha[] Acervo =
    [
        new("Acento", "Arreglos para banda-escuela", "Ministerio de Cultura", "978-958-753-485-6", 2002),
        new("8 Arreglos para Banda", null, "Ministerio de Cultura", "978-958-9039-24-3", 2010),
        new("Ramon el camaleon", "Beca nacional de creacion musical", "Luis Fernando Franco Duque", null, 2009),
        new("Guia de iniciacion al fagot", null, "Ministerio de Cultura", "978-958-753-105-3", 2012),
        new("Cartilla de Arreglos para Banda", "Nivel 1", "Victoriano Valencia", null, 2005),
        new("Musica colombiana para banda", "Composiciones originales y arreglos", "Ministerio de Cultura", "978-958-753-486-3", 2021),
        new("Cuaderno de ejercicios para clarinete", null, "Ministerio de Cultura", null, 2002),
        new("De los andes al caribe", "Antologia", "Universidad Nacional", "978-958-775-123-9", 2016),
    ];

    [Fact]
    public void La_lectura_recupera_lo_que_la_portada_dice()
    {
        var titulos = 0;
        var subtitulos = 0;
        var subtitulosPosibles = 0;
        var isbn = 0;
        var isbnPosibles = 0;
        var anios = 0;

        foreach (var ficha in Acervo)
        {
            var pdf = PortadasDePrueba.Construir(ficha);
            var lectura = LecturaDelDocumento.Leer(pdf);

            // EL TITULO ES EL BLOQUE MAS GRANDE. Se compara normalizado porque lo que importa es que
            // recupere el texto, no que respete la tipografía de la portada.
            var propuesto = lectura.Portada.Count > 0 ? lectura.Portada[0].Texto : string.Empty;
            if (SeParecen(propuesto, ficha.Titulo)) { titulos++; }
            else { salida.WriteLine($"título no recuperado · esperado «{ficha.Titulo}» · propuesto «{propuesto}»"); }

            if (!string.IsNullOrWhiteSpace(ficha.Subtitulo))
            {
                subtitulosPosibles++;
                var segundo = lectura.Portada.Count > 1 ? lectura.Portada[1].Texto : string.Empty;
                if (SeParecen(segundo, ficha.Subtitulo!)) { subtitulos++; }
                else { salida.WriteLine($"subtítulo no recuperado · esperado «{ficha.Subtitulo}» · propuesto «{segundo}»"); }
            }

            if (!string.IsNullOrWhiteSpace(ficha.Isbn))
            {
                isbnPosibles++;
                var cifrasEsperadas = SoloCifras(ficha.Isbn!);
                if (lectura.Isbn.Any(x => SoloCifras(x) == cifrasEsperadas)) { isbn++; }
                else { salida.WriteLine($"ISBN no recuperado · esperado «{ficha.Isbn}» · leídos [{string.Join(", ", lectura.Isbn)}]"); }
            }

            if (ficha.Anio is { } anio && lectura.Anios.Contains(anio)) { anios++; }
        }

        salida.WriteLine(string.Empty);
        salida.WriteLine($"PUNTUACION sobre {Acervo.Length} portadas fabricadas con datos reales del acervo:");
        salida.WriteLine($"  título      {titulos}/{Acervo.Length}");
        salida.WriteLine($"  subtítulo   {subtitulos}/{subtitulosPosibles}");
        salida.WriteLine($"  ISBN        {isbn}/{isbnPosibles}");
        salida.WriteLine($"  año         {anios}/{Acervo.Length}");

        // EL SUELO ES EL PLENO, porque hoy lo cumple: 8/8 en título, 5/5 en subtítulo, 5/5 en ISBN y
        // 8/8 en año. Poner el listón por debajo de lo que ya se logra deja pasar en silencio la
        // regresión siguiente, y aquí hubo una de verdad: el año daba 0 de 8 porque el texto salía con
        // las palabras pegadas. Si algún día un caso legítimamente difícil baja el número, se baja el
        // suelo A CONCIENCIA y con su motivo escrito, no por dejarlo holgado desde el principio.
        Assert.Equal(Acervo.Length, titulos);
        Assert.Equal(subtitulosPosibles, subtitulos);
        Assert.Equal(isbnPosibles, isbn);
        Assert.Equal(Acervo.Length, anios);
    }


    [Fact]
    public void Un_isbn_falso_en_la_portada_no_se_propone()
    {
        // ESTE ES EL CASO QUE JUSTIFICA COMPROBAR EL DIGITO DE CONTROL. Sin él, cualquier cadena de
        // trece cifras de una página de créditos —un NIT, un teléfono, un número de contrato— entraría
        // al catálogo como identificador de la obra.
        var pdf = PortadasDePrueba.Construir(new PortadasDePrueba.Ficha(
            "Una publicacion", null, "Alguien", "978-958-753-485-9", 2020));

        var lectura = LecturaDelDocumento.Leer(pdf);

        Assert.Empty(lectura.Isbn);
    }

    [Fact]
    public void Un_articulo_de_revista_no_confunde_el_encabezado_con_el_titulo()
    {
        // ESTE ES EL CASO QUE LA PRIMERA VERSION FALLABA, y que la puntuación de 8 sobre 8 no veía
        // porque el banco solo fabricaba documentos a la medida de la hipótesis. Probado con un
        // artículo real, el lector daba «Seccio Clinica de Barcelona» por título —el nombre de la
        // plantilla de la revista— y el nombre de la autora por subtítulo.
        var pdf = PortadasDePrueba.ConstruirArticulo(
            encabezado: "NODVS XVIII - Setembre de 2006",
            titulo: "La dialectica hegeliana del amo y el esclavo",
            autor: "Irene Dominguez Diaz",
            bajada: "Referencia a Fenomenologia del espiritu, de Hegel",
            resumen: "El texto recorre la figura del reconocimiento en la Fenomenologia del espiritu.");

        var lectura = LecturaDelDocumento.Leer(pdf);
        var analisis = lectura.Analisis;

        // El título es el del artículo, no el encabezado que se repite en las tres páginas.
        Assert.Contains("dialectica", analisis.Titulo ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("NODVS", analisis.Titulo ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        // La autoría es autoría, no subtítulo.
        Assert.Contains(analisis.Autores, a => a.Contains("Dominguez", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("Dominguez", analisis.Subtitulo ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        // Y el resumen se recoge cuando el documento lo rotula.
        Assert.Contains("reconocimiento", analisis.Resumen ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        salida.WriteLine($"título    {analisis.Titulo}");
        salida.WriteLine($"motivo    {analisis.MotivoDelTitulo}");
        salida.WriteLine($"subtítulo {analisis.Subtitulo}");
        salida.WriteLine($"autoría   {string.Join(" · ", analisis.Autores)}");
    }

    [Fact]
    public void Un_titulo_de_metadatos_que_es_el_encabezado_no_gana()
    {
        // El fallo exacto del documento real: el PDF declaraba como título el nombre de la plantilla
        // de la revista, y los metadatos iban por delante del análisis de la página.
        var analisis = AnalisisDeLaPortada.Analizar(
            [
                new AnalisisDeLaPortada.Bloque(10, 0.91, "Seccio Clinica de Barcelona"),
                new AnalisisDeLaPortada.Bloque(24, 0.79, "La dialectica hegeliana del amo y el esclavo"),
            ],
            tituloDeMetadatos: "Seccio Clinica de Barcelona",
            textoDeOtrasPaginas: "Seccio Clinica de Barcelona cuerpo del articulo");

        Assert.Equal("La dialectica hegeliana del amo y el esclavo", analisis.Titulo);
    }

    [Fact]
    public void El_motivo_del_titulo_no_es_el_castigo_que_recibio()
    {
        // CASO REAL, «Teoría del Jazz»: la cubierta lleva el título en grande y ese título tiene forma
        // de nombre propio —tres palabras, dos capitalizadas—, así que se le resta. Gana igual, porque
        // es el texto mayor. Lo que estaba mal era lo que se ENSEÑABA: el bucle sobrescribía el motivo
        // con el último castigo, y la pantalla proponía el título con la nota «tiene forma de nombre
        // propio». Quien cataloga leía como argumento una objeción ya descartada.
        var analisis = AnalisisDeLaPortada.Analizar(
            [
                new AnalisisDeLaPortada.Bloque(28, 0.62, "Teoria del Jazz"),
                new AnalisisDeLaPortada.Bloque(12, 0.40, "Jaime Jaramillo Arias"),
            ],
            tituloDeMetadatos: null,
            textoDeOtrasPaginas: "contenido del metodo");

        Assert.Equal("Teoria del Jazz", analisis.Titulo);
        Assert.Equal("es el texto más grande de la primera página", analisis.MotivoDelTitulo);
    }

    [Theory]
    [InlineData("Irene Dominguez Diaz", true)]
    [InlineData("Luis Fernando Franco Duque", true)]
    [InlineData("La dialectica hegeliana del amo y el esclavo", false)]
    [InlineData("Guia de iniciacion al fagot", false)]
    [InlineData("Ministerio de Cultura", true)]
    public void Se_distingue_un_nombre_propio_de_un_titulo(string texto, bool esNombre)
    {
        // LA SEÑAL: un título casi siempre lleva una palabra funcional —«el», «para», «al»— y un
        // nombre no. Se dejan fuera «de», «del», «la» y «y» porque SI aparecen en nombres reales:
        // «De la Torre», «García y Ruiz». Es una heurística, y por eso la autoría se PROPONE.
        Assert.Equal(esNombre, AnalisisDeLaPortada.EsNombreDePersona(texto));
    }

    [Fact]
    public void Una_portada_sin_capa_de_texto_se_lee_mirandola()
    {
        // LA PRUEBA QUE NO HACE TRAMPA. Se fabrica una portada normal, se RASTERIZA y se vuelve a
        // empaquetar como PDF: el resultado es exactamente lo que llega de un escaneo, una página que
        // es una fotografía de un texto. La vía de extracción no puede sacar nada de ahí, así que si
        // el título aparece es porque el reconocimiento óptico lo leyó de verdad.
        //
        // El caso real que obligó a conectarlo: «Poemas Humanos», de César Vallejo, del que la capa de
        // texto solo devolvía «0þí´Ñ Q» y la herramienta proponía eso como título de la publicación.
        var ficha = new PortadasDePrueba.Ficha(
            "Cartilla de Arreglos para Banda", "Nivel 1", "Victoriano Valencia", null, 2005);

        var escaneada = PortadasDePrueba.SoloImagen(PortadasDePrueba.Construir(ficha));
        var lectura = LecturaDelDocumento.Leer(escaneada);

        salida.WriteLine($"disponible {LecturaOptica.Disponible} · {LecturaOptica.Modelo}");
        salida.WriteLine($"óptico     {lectura.LeidoConReconocimientoOptico}");
        salida.WriteLine($"título     {lectura.Analisis.Titulo}");
        foreach (var bloque in lectura.Portada.Take(5))
        {
            salida.WriteLine($"  {bloque.Tamano,4}pt  y={bloque.AlturaRelativa:P1}  {bloque.Texto}");
        }

        // Los modelos viajan dentro del paquete, así que esto tiene que estar disponible siempre. Si
        // deja de estarlo, es que los ficheros no llegaron al binario —que ya pasó una vez, con
        // PNMC.Api— y hay que verlo aquí y no al desplegar.
        Assert.True(LecturaOptica.Disponible, "Los modelos de reconocimiento óptico no llegaron al binario.");
        Assert.True(lectura.LeidoConReconocimientoOptico);
        Assert.Contains("Arreglos", lectura.Analisis.Titulo ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Un_documento_con_texto_no_gasta_reconocimiento_optico()
    {
        // EL CAMINO OPTICO ES LA RED DE ABAJO Y NO EL PRINCIPAL. Cuesta entre 175 y 520 ms por página
        // —medido—, y un PDF con capa de texto da un resultado mejor y gratis. Si esta prueba falla,
        // es que se está reconociendo lo que ya se podía leer.
        var lectura = LecturaDelDocumento.Leer(PortadasDePrueba.Construir(
            new PortadasDePrueba.Ficha("Guia de iniciacion al fagot", null, "Ministerio de Cultura", null, 2012)));

        Assert.False(lectura.LeidoConReconocimientoOptico);
    }

    private static bool SeParecen(string propuesto, string esperado)
    {
        var a = CotejoConElAcervo.Normalizar(propuesto);
        var b = CotejoConElAcervo.Normalizar(esperado);
        if (a.Length == 0 || b.Length == 0) { return false; }
        // Basta con que uno contenga al otro: la portada recorta los títulos muy largos.
        return a.Contains(b, StringComparison.Ordinal) || b.Contains(a, StringComparison.Ordinal);
    }

    private static string SoloCifras(string valor) =>
        new(valor.Where(char.IsAsciiDigit).ToArray());
}
