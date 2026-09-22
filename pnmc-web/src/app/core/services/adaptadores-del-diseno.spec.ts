import { aEventoDeDiseno, aNoticiaDeDiseno, aRecursoEditorialDeDiseno } from './adaptadores-del-diseno';
import { EventoAgenda } from './agenda.service';
import { Noticia } from './noticias.service';
import { PublicacionEditorialPublica } from './catalogo-editorial.service';

/**
 * La traducción entre nuestro modelo y los diseños aprobados.
 *
 * <b>POR QUE SE PRUEBA AQUI Y NO EN LAS PANTALLAS.</b> Las tres pantallas —Catálogo Editorial,
 * Agenda y Noticias— son el diseño aprobado portado tal cual desde el diseño aprobado del portal; su lógica no es
 * nuestra y se reproduce sin tocarla. Lo nuestro es este fichero: es donde el modelo real se
 * convierte en la forma que aquellas plantillas esperan, y donde un fallo apaga una tarjeta entera
 * sin que nada reviente.
 */

/**
 * LA FICHA COMO LA RECIBE EL PORTAL, que es la entrada real de este adaptador. No lleva estados, ni
 * versión, ni confianza, ni procedencia: eso se queda en la consola. Ver
 * `PublicacionEditorialPublica`.
 */
// LOS TIPOS DE AGENTE VAN EN MINUSCULA, que es como los devuelve el API y como los fija
// `CK_AgentesEditoriales_Tipo`. Esta ficha de ejemplo los escribía capitalizados —«Entidad»— y con
// eso la prueba medía que el código hacía lo que el código hacía: la comparación del adaptador
// tampoco se cumplía nunca, así que «Autor corporativo» no se vio jamás en el catálogo y la prueba
// seguía en verde.
function publicacion(parcial: Partial<PublicacionEditorialPublica> = {}): PublicacionEditorialPublica {
  return {
    codigo: 'PNMC-ED-001', titulo: 'Acento', subtitulo: null, designacionVolumen: null,
    serieOColeccion: null, resumen: 'Resumen', fechaEdtf: null, anioInicio: 2016, anioFin: 2016,
    notaFecha: null, idioma: 'es', tipoPublicacion: 'Partitura', formato: 'Físico', tipologia: [],
    tamanoFormato: '21 x 29,7 cm', paginas: '48 p.', duracion: null,
    categoria: 'Bandas', categoriaSecundaria: null, practicaMusical: 'Banda', subcategoria: 'Arreglos',
    palabrasClave: ['banda'], ambito: 'Nacional', ambitoTexto: null,
    seccionPrincipal: 'Partituras y arreglos', rutaSeccion: '/editorial/partituras',
    miniaturaRuta: '/editorial/thumbs/PNMC-ED-001.jpg', textoPortada: null,
    creditos: [], identificadores: [], accesos: [], licencia: null,
    practicasMusicales: [], territoriosSonoros: [],
    fechaActualizacion: '2026-09-11T00:00:00Z',
    ...parcial,
  };
}

describe('el catálogo editorial, en la forma del diseño aprobado', () => {
  it('separa la autoría persona de la corporativa, que el diseño enseña por separado', () => {
    const recurso = aRecursoEditorialDeDiseno(publicacion({
      creditos: [
        { agenteId: 1, nombre: 'Ana Restrepo', tipo: 'persona', rolCodigo: 'aut', rol: 'Autora', principal: true },
        { agenteId: 2, nombre: 'Ministerio de las Culturas', tipo: 'entidad', rolCodigo: 'aut', rol: 'Autor corporativo', principal: true },
        { agenteId: 3, nombre: 'Luis Pérez', tipo: 'persona', rolCodigo: 'arr', rol: 'Arreglos', principal: false },
      ],
    }));

    expect(recurso.author).toBe('Ana Restrepo');
    expect(recurso.corporateAuthor).toBe('Ministerio de las Culturas');
    // Los que no son autoría principal van con su rol, que es como los lee una persona.
    expect(recurso.additionalCredits).toBe('Luis Pérez (Arreglos)');
    expect(recurso.displayAuthor).toBe('Ana Restrepo');
    // LA LISTA ENTERA TAMBIEN VIAJA, y con el papel EN LETRA: la ficha la pinta como lista, con un
    // crédito por línea, que es lo que una cadena aplanada no puede dar.
    expect(recurso.credits.map(c => `${c.rol}: ${c.nombre}`)).toEqual([
      'Autora: Ana Restrepo', 'Autor corporativo: Ministerio de las Culturas', 'Arreglos: Luis Pérez',
    ]);
  });

  it('cae a la autoría corporativa cuando no hay persona', () => {
    const recurso = aRecursoEditorialDeDiseno(publicacion({
      creditos: [{ agenteId: 1, nombre: 'Mincultura', tipo: 'entidad', rolCodigo: 'aut', rol: 'Autor', principal: true }],
    }));

    expect(recurso.displayAuthor).toBe('Mincultura');
  });

  it('reconoce la entidad por el valor que el API escribe de verdad, en minúscula', () => {
    // ESTA ES LA PRUEBA QUE FALTABA. El adaptador comparaba con `'Entidad'` capitalizado y el API
    // devuelve `entidad`, así que la autoría corporativa NUNCA se separó: toda entidad acreditada
    // se contaba como persona y la fila «Autor corporativo» del diseño no se vio jamás. El vocabulario
    // lo fija `CK_AgentesEditoriales_Tipo CHECK (Tipo IN (N'persona', N'entidad'))`, y en el lado del
    // API lo custodia `ElVocabularioDeTipoDeAgente...` en CatalogoEditorialContratoTests.
    const ficha = aRecursoEditorialDeDiseno(publicacion({
      creditos: [
        { agenteId: 1, nombre: 'Ministerio de las Culturas', tipo: 'entidad', rolCodigo: 'aut', rol: 'Autor corporativo', principal: true },
        { agenteId: 2, nombre: 'Ana Restrepo', tipo: 'persona', rolCodigo: 'aut', rol: 'Autora', principal: true },
      ],
    }));

    expect(ficha.corporateAuthor).toBe('Ministerio de las Culturas');
    expect(ficha.author).toBe('Ana Restrepo');
  });

  it('acota la autoría visible a tres nombres, que es una ranura de una línea y no la lista de créditos', () => {
    // `PNMC-ED-010` acredita a 39 agentes. Sin acotar, la mención de responsabilidad del mosaico
    // pasaba a ser un muro de 39 nombres donde cabe uno.
    const ficha = aRecursoEditorialDeDiseno(publicacion({
      creditos: Array.from({ length: 39 }, (_, i) => ({
        agenteId: i + 1, nombre: `Compositor ${i + 1}`, tipo: 'persona',
        rolCodigo: 'cmp', rol: 'Compositor', principal: false,
      })),
    }));

    expect(ficha.displayAuthor).toBe('Compositor 1; Compositor 2; Compositor 3 y 36 más');
  });

  it('reparte los identificadores por esquema', () => {
    const recurso = aRecursoEditorialDeDiseno(publicacion({
      identificadores: [
        { esquema: 'ISBN', codigo: '978-958-1', cualificador: null },
        { esquema: 'ISBN', codigo: '978-958-2', cualificador: null },
        { esquema: 'ISMN', codigo: 'M-001', cualificador: null },
      ],
    }));

    // UNA OBRA PUEDE TENER VARIOS del mismo esquema: el reparto real del acervo llega a cinco.
    //
    // EL SEPARADOR ES PUNTO Y COMA EN TODO EL ADAPTADOR, y la razón está medida sobre el acervo: de
    // los nombres acreditados, NUEVE llevan una coma dentro —«Franco Duque, Luis Fernando»— y
    // NINGUNO un punto y coma. Con coma, esos nueve nombres se leían como dos. Un solo separador
    // para todas las listas evita que la ficha use dos convenciones según la fila.
    expect(recurso.isbn).toBe('978-958-1; 978-958-2');
    expect(recurso.ismn).toBe('M-001');
  });

  it('conserva los accesos COMO LISTA, sin aplanarlos a dos cadenas', () => {
    // El adaptador exponía además `url` y `location`: la misma información unida con «; ». Dos
    // representaciones de lo mismo, y la pantalla acabó enseñando la ubicación dos veces y abriendo
    // «url1; url2» en las tres fichas que tienen varios enlaces. Ahora solo existe la lista.
    const recurso = aRecursoEditorialDeDiseno(publicacion({
      accesos: [
        { tipo: 'enlace', url: 'https://www.mincultura.gov.co/a', ubicacionFisica: null, etiqueta: null, nota: null },
        { tipo: 'enlace', url: 'https://www.mincultura.gov.co/b', ubicacionFisica: null, etiqueta: null, nota: null },
        { tipo: 'ubicacion', url: null, ubicacionFisica: 'Centro de documentación', etiqueta: null, nota: null },
      ],
    }));

    expect(recurso.access.length).toBe(3);
    expect(recurso.access.filter(a => a.url).map(a => a.url))
      .toEqual(['https://www.mincultura.gov.co/a', 'https://www.mincultura.gov.co/b']);
    expect(recurso.access.find(a => !a.url)?.ubicacionFisica).toBe('Centro de documentación');
    expect(Object.keys(recurso)).not.toContain('url');
    expect(Object.keys(recurso)).not.toContain('location');
  });

  it('no repite el año cuando inicio y fin coinciden', () => {
    expect(aRecursoEditorialDeDiseno(publicacion({ anioInicio: 2016, anioFin: 2016 })).year).toBe('2016');
    expect(aRecursoEditorialDeDiseno(publicacion({ anioInicio: 2016, anioFin: 2018 })).year).toBe('2016 – 2018');
    expect(aRecursoEditorialDeDiseno(publicacion({ anioInicio: null, anioFin: null })).year).toBe('');
  });

  it('deja vacío lo que el modelo no tiene, sin inventar nada', () => {
    const recurso = aRecursoEditorialDeDiseno(publicacion({ duracion: null }));

    // LA PLANTILLA YA SABE OCULTAR LO VACIO: sus tarjetas filtran por valor antes de dibujarse.
    expect(recurso.duration).toBe('');
    expect(recurso.author).toBe('');
  });
});

describe('las noticias, en la forma del diseño aprobado', () => {
  function noticia(parcial: Partial<Noticia> = {}): Noticia {
    return {
      id: 7, slug: 'una-noticia', titulo: 'Una noticia', resumen: 'Resumen', cuerpo: 'Cuerpo',
      fechaPublicacion: '2026-09-11', imagenRuta: null, imagenAlternativa: null,
      autoriaNombre: null, imagenArchivoId: null, imagenUrl: null, imagenAlt: null,
      categoriaId: 4, categoria: 'Encuentros', estado: 'publicado', estadoEfectivo: 'publicado', version: 1,
      etiquetas: [], practicasMusicales: [], territoriosSonoros: [], procedencia: null, proyectosTransversales: [],
      fechaActualizacion: '2026-09-11T00:00:00Z',
      ...parcial,
    };
  }

  it('escribe la fecha como el diseño: «11 SEP 2026»', () => {
    expect(aNoticiaDeDiseno(noticia()).date).toBe('11 SEP 2026');
  });

  it('no retrocede un día por la zona horaria', () => {
    // `new Date('2026-01-01')` se interpreta como UTC y en Colombia —cinco horas atrás— se pinta
    // como 31 de diciembre. Es el fallo clásico y no se nota hasta que alguien mira el calendario.
    expect(aNoticiaDeDiseno(noticia({ fechaPublicacion: '2026-01-01' })).date).toBe('1 ENE 2026');
  });

  it('una noticia sin fecha no inventa una', () => {
    expect(aNoticiaDeDiseno(noticia({ fechaPublicacion: null })).date).toBe('');
  });

  it('una noticia sin imagen se queda sin imagen', () => {
    // El mapeador del desarrollo de origen asignaba una ALEATORIA, que es lo que hacía que aquella
    // pantalla estuviera falsamente conectada.
    expect(aNoticiaDeDiseno(noticia()).img).toBe('');
  });
});

describe('la agenda, en la forma del diseño aprobado', () => {
  function evento(parcial: Partial<EventoAgenda> = {}): EventoAgenda {
    return {
      id: 3, slug: 'festival', titulo: 'Festival', descripcion: 'Descripción',
      fechaInicio: '2026-09-09', fechaFin: '2026-09-16', horaInicio: '18:00:00',
      modalidad: 'presencial', lugar: 'Plaza principal', codigoDepartamento: '15',
      nombreDepartamento: 'BOYACÁ', codigoMunicipio: '15516', nombreMunicipio: 'PAIPA',
      url: null, imagenRuta: null, imagenAlternativa: null, categoriaId: 2, categoria: 'Festivales',
      organizador: null, descripcionLarga: null, horaFin: null, nivelCobertura: 'municipal',
      ordenVisualizacion: null, festivalId: null,
      imagenArchivoId: null, imagenUrl: null, imagenAlt: null,
      estado: 'publicado', situacion: 'en_curso', version: 1,
      etiquetas: [], practicasMusicales: [], territoriosSonoros: [], procedencia: null, proyectosTransversales: [],
      fechaActualizacion: '2026-09-11T00:00:00Z',
      ...parcial,
    };
  }

  it('despieza la fecha como la enseña el diseño', () => {
    const e = aEventoDeDiseno(evento());

    expect(e.d).toBe('9');
    expect(e.m).toBe('SEP');
    expect(e.y).toBe('2026');
  });

  it('escribe la hora en doce horas con meridiano', () => {
    expect(aEventoDeDiseno(evento({ horaInicio: '18:00:00' })).time).toBe('6:00 PM');
    expect(aEventoDeDiseno(evento({ horaInicio: '09:30:00' })).time).toBe('9:30 AM');
    // MEDIANOCHE Y MEDIODIA son los dos casos que rompen el resto por doce.
    expect(aEventoDeDiseno(evento({ horaInicio: '00:15:00' })).time).toBe('12:15 AM');
    expect(aEventoDeDiseno(evento({ horaInicio: '12:05:00' })).time).toBe('12:05 PM');
    expect(aEventoDeDiseno(evento({ horaInicio: null })).time).toBe('');
  });

  it('no repite el nombre cuando el municipio y el departamento coinciden', () => {
    const distrito = aEventoDeDiseno(evento({ nombreMunicipio: 'BOGOTÁ, D.C.', nombreDepartamento: 'BOGOTÁ, D.C.' }));

    expect(distrito.l).toBe('BOGOTÁ, D.C.');
    expect(aEventoDeDiseno(evento()).l).toBe('PAIPA, BOYACÁ');
  });

  it('conserva la fecha de fin, que la forma heredada no tenía', () => {
    // Sin ella un festival de una semana dura un día en el calendario de quien lo añade.
    expect(aEventoDeDiseno(evento()).fechaFin).toBe('2026-09-16');
    expect(aEventoDeDiseno(evento({ fechaFin: null })).fechaFin).toBe('2026-09-09');
  });

  it('la fecha comparable se fija a mediodía local', () => {
    const e = aEventoDeDiseno(evento());

    // A MEDIODIA Y NO A MEDIANOCHE: así ningún desfase de zona mueve el día en ningún sentido.
    expect(e.dateObj.getDate()).toBe(9);
    expect(e.dateObj.getMonth()).toBe(8);
  });
});
