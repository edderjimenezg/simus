using System.Globalization;
using PNMC.Infrastructure.Data;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// La lectura de un documento para proponer una ficha.
/// </summary>
/// <remarks>
/// <para>
/// LO QUE FIJAN ESTAS PRUEBAS, y por qué importa que sean deterministas: la herramienta propone
/// título, autoría e identificadores a quien cataloga, y una propuesta se acepta sin mirar mucho más
/// a menudo que un campo vacío se rellena mal. Un identificador inventado entra al catálogo y de ahí
/// a cualquier sitio que lo cite.
/// </para>
/// <para>
/// NINGUNA DE ESTAS COMPROBACIONES NECESITA UN MODELO. Un dígito de control se calcula; una marca de
/// orden de bytes se mira; un nombre del fichero de autoridades se busca. Solo lo que queda fuera de
/// esto —el resumen, la tipología— pediría uno.
/// </para>
/// </remarks>
public sealed class LecturaDeDocumentosTests
{
    [Theory]
    // ISBN-13 reales de publicaciones del acervo y sus variantes rotas.
    [InlineData("9789587534856", true)]
    [InlineData("9789587534857", false)]   // último dígito cambiado
    // ISBN-10 con dígito de control «X», que vale diez. La primera versión de esta prueba usó un
    // número inventado y falló: el código tenía razón y la ficha de ejemplo no. Este está calculado.
    [InlineData("958972342X", true)]
    [InlineData("9589723421", false)]
    [InlineData("123456789012", false)]    // doce cifras: no es ningún esquema
    [InlineData("", false)]
    public void Un_isbn_solo_vale_si_su_digito_de_control_cuadra(string cifras, bool esperado)
    {
        // AQUI NO SE CONJETURA: SE COMPRUEBA. Sin el dígito de control, cualquier cadena de trece
        // cifras encontrada en una página de créditos —un teléfono, un NIT— pasaría por ISBN.
        Assert.Equal(esperado, LecturaDelDocumento.EsIsbnValido(cifras));
    }

    [Theory]
    [InlineData("9790801632210", true)]
    [InlineData("9789587534856", false)]   // ISBN válido, pero no empieza por 979-0
    [InlineData("9790801632211", false)]   // prefijo bueno, control malo
    public void Un_ismn_exige_ademas_el_prefijo_de_las_partituras(string cifras, bool esperado)
    {
        // Un ISMN es un EAN-13 que empieza por 979-0. Sin comprobar el prefijo, el código de barras
        // de cualquier producto pasaría por identificador de una partitura.
        Assert.Equal(esperado, LecturaDelDocumento.EsIsmnValido(cifras));
    }

    [Fact]
    public void El_cotejo_ignora_tildes_y_mayusculas_porque_las_portadas_gritan()
    {
        // Una portada escribe «MINISTERIO DE CULTURA» y el fichero de autoridades «Ministerio de
        // Cultura». Comparar tal cual perdería la mitad de las coincidencias por tipografía.
        var reconocidos = CotejoConElAcervo.Reconocer(
            "Publicado por el MINISTERIO DE CULTURA en Bogotá",
            [("Ministerio de Cultura", 127), ("Fundación Canto por la vida", 3)]);

        Assert.Single(reconocidos);
        // Se devuelve el término COMO LO TIENE EL CATALOGO, no como aparecía en la portada: es el que
        // enlaza con el agente que ya existe.
        Assert.Equal("Ministerio de Cultura", reconocidos[0].Valor);
        Assert.Equal(127, reconocidos[0].Usos);
    }

    [Fact]
    public void La_eñe_sobrevive_al_cotejo()
    {
        // Quitar diacríticos a lo bruto convierte «Muñoz» en «Munoz», y entonces un apellido del
        // fichero de autoridades deja de coincidir consigo mismo.
        Assert.Equal("muñoz peña", CotejoConElAcervo.Normalizar("Muñoz Peña"));
        Assert.Equal("musica andina", CotejoConElAcervo.Normalizar("Música Andina"));
    }

    [Fact]
    public void Con_palabra_completa_un_toponimo_corto_no_se_confunde()
    {
        // SIN EXIGIR PALABRA ENTERA, bajar el umbral a cuatro reconocería «Cali» dentro de
        // «California» y de «calidad». El umbral bajo solo es seguro con el borde comprobado.
        var dentroDeOtra = CotejoConElAcervo.Reconocer(
            "Un estudio sobre la calidad en California", [("Cali", 0)],
            largoMinimo: 4, exigirPalabraCompleta: true);
        Assert.Empty(dentroDeOtra);

        var comoPalabra = CotejoConElAcervo.Reconocer(
            "Encuentro en Cali, Valle del Cauca", [("Cali", 0)],
            largoMinimo: 4, exigirPalabraCompleta: true);
        Assert.Single(comoPalabra);
    }

    [Fact]
    public void No_se_proponen_terminos_demasiado_cortos()
    {
        // «Banda» aparece en «banda sonora» y en cualquier frase: una coincidencia así no dice nada,
        // y proponerla llenaría la pantalla de ruido que hay que quitar a mano.
        var reconocidos = CotejoConElAcervo.Reconocer(
            "La banda de la escuela interpretó el repertorio",
            [("Banda", 56), ("Coro", 24)]);

        Assert.Empty(reconocidos);
    }

    [Fact]
    public void Entre_dos_coincidencias_encajadas_manda_la_larga()
    {
        // Si el texto dice «Pedagogía Instrumental» y el vocabulario tiene también «Pedagogía», las
        // dos coinciden; la larga es la que informa.
        var reconocidos = CotejoConElAcervo.Reconocer(
            "Colección de Pedagogía Instrumental para bandas",
            [("Pedagogía", 9), ("Pedagogía Instrumental", 25)]);

        Assert.Equal("Pedagogía Instrumental", reconocidos[0].Valor);
    }

    [Theory]
    // Las tres formas en que un afiche colombiano escribe una fecha.
    [InlineData("Concierto el 12 de octubre de 2025 en la plaza", "2025-10-12", null)]
    [InlineData("Del 3 al 7 de agosto de 2024, Festival de Bandas", "2024-08-03", "2024-08-07")]
    [InlineData("Inscripciones hasta el 15/09/2025", "2025-09-15", null)]
    public void Las_fechas_se_leen_como_las_escribe_la_gente(string texto, string inicio, string? fin)
    {
        var fechas = FechasEnElTexto.Leer(texto);

        Assert.NotEmpty(fechas);
        Assert.Equal(inicio, fechas[0].Inicio.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        Assert.Equal(fin, fechas[0].Fin?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Un_rango_no_se_parte_en_dos_fechas_sueltas()
    {
        // SI SE BUSCARAN PRIMERO LAS FECHAS SUELTAS, «del 3 al 7 de agosto» daría dos eventos de un
        // día en vez de uno de cinco. Por eso el rango se busca antes.
        var fechas = FechasEnElTexto.Leer("Del 3 al 7 de agosto de 2024");

        Assert.Single(fechas);
        Assert.NotNull(fechas[0].Fin);
    }

    [Fact]
    public void Una_fecha_sin_año_usa_el_del_documento_y_no_el_de_hoy()
    {
        // UN AFICHE DICE «12 de octubre» porque quien lo lee está dentro de ese año. Inventar el año
        // en curso pondría el evento en la agenda del año equivocado sin que nadie lo decidiera.
        var conRespaldo = FechasEnElTexto.Leer("Gran concierto el 12 de octubre", 2019);
        Assert.Equal(2019, conRespaldo[0].Inicio.Year);

        var sinRespaldo = FechasEnElTexto.Leer("Gran concierto el 12 de octubre");
        Assert.Empty(sinRespaldo);
    }

    [Fact]
    public void Un_dia_que_no_existe_no_se_propone()
    {
        // «31 de febrero» aparece en afiches con erratas, y proponerlo crearía un evento imposible.
        Assert.Empty(FechasEnElTexto.Leer("Evento"));
        Assert.Empty(FechasEnElTexto.Leer("Evento el 45/13/2025"));
    }

    [Fact]
    public void Un_afiche_entrega_su_nombre_su_fecha_y_su_lugar()
    {
        // ES EL CAMINO DE LA AGENDA, y usa la misma tubería que el Catálogo Editorial: cambia qué se
        // reconoce dentro, no cómo se lee. Aquí importa cuándo y dónde.
        var afiche = PortadasDePrueba.ConstruirAfiche(
            "Festival de Bandas de Paipa",
            "Del 3 al 7 de agosto de 2024",
            "Plaza principal, Paipa, Boyaca",
            "Fundacion Musical de Colombia");

        var lectura = LecturaDelDocumento.Leer(afiche);

        // El nombre del evento es lo más grande, igual que el título en la portada de un libro.
        Assert.Contains("Festival de Bandas", lectura.Portada[0].Texto, StringComparison.OrdinalIgnoreCase);

        var fechas = FechasEnElTexto.Leer(lectura.TextoDeLasPrimerasPaginas);
        Assert.Single(fechas);
        Assert.Equal("2024-08-03", fechas[0].Inicio.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        Assert.Equal("2024-08-07", fechas[0].Fin?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

        // Y el municipio se reconoce contra el catálogo territorial, que es lo que lo hace útil: no
        // llega como texto sino resuelto, listo para que el evento salga en el mapa.
        // EL MUNICIPIO SE RECONOCE CON EL UMBRAL DE TOPONIMO. Con el del vocabulario editorial
        // —ocho letras— «Paipa» quedaba fuera, y con él decenas de municipios colombianos.
        var municipios = CotejoConElAcervo.Reconocer(
            lectura.TextoDeLasPrimerasPaginas,
            [("Paipa", 0), ("Bogotá, D.C.", 0), ("Medellín", 0)],
            largoMinimo: 4,
            exigirPalabraCompleta: true);

        Assert.Single(municipios);
        Assert.Equal("Paipa", municipios[0].Valor);
    }
}
