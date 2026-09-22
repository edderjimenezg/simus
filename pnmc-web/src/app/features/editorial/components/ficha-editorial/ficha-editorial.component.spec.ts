import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FichaEditorialComponent } from './ficha-editorial.component';
import { RecursoEditorialDeDiseno } from '../../../../core/services/adaptadores-del-diseno';

/**
 * La ficha catalográfica del portal.
 *
 * <b>QUE FIJAN ESTAS PRUEBAS.</b> Lo que la ficha enseñaba a medias y por qué importaba:
 *
 * <ul>
 *   <li>Que el subtítulo y el volumen se lean. Con nueve series en el acervo, dos volúmenes de
 *       «Acento» se veían idénticos.</li>
 *   <li>Que los identificadores sean una LISTA con su cualificador: PNMC-ED-103 trae cinco ISBN
 *       —PDF, EPUB, HTML, iBook y MOBI— y sin el cualificador serían cinco líneas iguales.</li>
 *   <li>Que el papel de cada crédito salga EN LETRA y no en código MARC.</li>
 *   <li>Que una ubicación física no se pinte como enlace: no lleva a ningún sitio.</li>
 *   <li>Que un vídeo se reconozca por su DIRECCION y no por su sección, que la escribe quien
 *       cataloga y puede equivocarse.</li>
 * </ul>
 */
function recurso(cambios: Partial<RecursoEditorialDeDiseno> = {}): RecursoEditorialDeDiseno {
  return {
    id: 'PNMC-ED-019', title: 'Ramón el camaleón', year: '2009', section: 'Repertorio',
    sectionPath: 'Repertorio > Orquesta', publicationType: 'Estuche', practice: 'Orquesta',
    category: '', subcategory: 'Repertorio y partituras', regionalScope: 'Nacional',
    author: 'Luis Fernando Franco Duque', corporateAuthor: '', additionalCredits: '',
    displayAuthor: 'Luis Fernando Franco Duque', isbn: '', ismn: '',
    formatSize: '24 x 34,5 cm', pages: '78 p.', duration: '', summary: 'Resumen de la obra.', coverText: '',
    thumbnail: '/editorial/thumbs/PNMC-ED-019.jpg', keywords: ['orquesta'],
    subtitle: '', volume: '', series: '', language: 'es', edtfDate: '2009', dateNote: '',
    secondaryCategory: '', scopeText: 'Nacional', mediaFormat: 'Físico', licence: '',
    typology: [], credits: [], identifiers: [], access: [],
    ...cambios,
  };
}

describe('FichaEditorialComponent', () => {
  let fixture: ComponentFixture<FichaEditorialComponent>;

  const montar = (item: RecursoEditorialDeDiseno, variante: 'fila' | 'dialogo' = 'dialogo') => {
    fixture = TestBed.createComponent(FichaEditorialComponent);
    fixture.componentRef.setInput('item', item);
    fixture.componentRef.setInput('variante', variante);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [FichaEditorialComponent] }).compileComponents();
  });

  it('agrupa los créditos que comparten papel en una sola fila, con los nombres separados', () => {
    // MEDIDO SOBRE EL ACERVO: 133 de las 171 publicaciones repiten algún papel, y `PNMC-ED-010`
    // acredita a VEINTIOCHO compositores. Una fila por crédito convertía la ficha en una columna de
    // «Compositor · nombre» repetida veintiocho veces, que no se lee.
    const raiz = montar(recurso({
      credits: [
        { agenteId: 101, nombre: 'Ana Restrepo', rol: 'Arreglista', rolCodigo: 'arr', principal: false, tipo: 'persona' },
        { agenteId: 102, nombre: 'Luis Pérez', rol: 'Arreglista', rolCodigo: 'arr', principal: false, tipo: 'persona' },
        { agenteId: 103, nombre: 'Carlos Gómez', rol: 'Autor', rolCodigo: 'aut', principal: true, tipo: 'persona' },
      ],
    }));

    expect(raiz.textContent).toContain('Arreglistas');
    expect(raiz.textContent).toContain('Ana Restrepo; Luis Pérez');
    // El papel que sale una sola vez se queda en singular: no hay «Autores» de un solo autor.
    expect(raiz.textContent).toContain('Autor');
    expect(raiz.textContent).not.toContain('Autores');
  });

  it('no pluraliza los papeles que ya son un nombre de actividad', () => {
    // De los 16 papeles del acervo, SIETE no son oficios sino actividades —«Asesoría»,
    // «Conceptualización», «Metodología»…—. «Asesorías» sería un error de lengua, no un plural.
    const raiz = montar(recurso({
      credits: [
        { agenteId: 104, nombre: 'Ana Restrepo', rol: 'Asesoría', rolCodigo: 'ase', principal: false, tipo: 'persona' },
        { agenteId: 105, nombre: 'Luis Pérez', rol: 'Asesoría', rolCodigo: 'ase', principal: false, tipo: 'persona' },
      ],
    }));

    expect(raiz.textContent).toContain('Asesoría');
    expect(raiz.textContent).not.toContain('Asesorías');
  });

  it('lee el subtítulo y el volumen, que antes no llegaban a la pantalla', () => {
    const raiz = montar(recurso({ title: 'Acento', subtitle: 'Arreglos para banda-escuela', volume: 'Volumen I' }));

    expect(raiz.textContent).toContain('Arreglos para banda-escuela');
    expect(raiz.textContent).toContain('Volumen I');
  });

  it('pinta los identificadores como lista y conserva el cualificador que los distingue', () => {
    const raiz = montar(recurso({
      identifiers: [
        { esquema: 'ISBN', codigo: '978-1', cualificador: 'PDF' },
        { esquema: 'ISBN', codigo: '978-2', cualificador: 'EPUB' },
      ],
    }));

    const lineas = Array.from(raiz.querySelectorAll('li')).map(l => l.textContent!.replace(/\s+/g, ' ').trim());
    expect(lineas).toContain('ISBN 978-1 · PDF');
    expect(lineas).toContain('ISBN 978-2 · EPUB');
  });

  it('escribe el papel de cada crédito en letra, no en código MARC', () => {
    const raiz = montar(recurso({
      credits: [{ agenteId: 7, nombre: 'Ana Restrepo', tipo: 'persona', rolCodigo: 'arr', rol: 'Arreglista', principal: false }],
    }));

    expect(raiz.textContent).toContain('Arreglista');
    expect(raiz.textContent).toContain('Ana Restrepo');
    expect(raiz.textContent).not.toContain('arr');
  });

  it('une los cuatro ejes de la tipología citando su norma', () => {
    const raiz = montar(recurso({
      typology: [
        { eje: 'soporte', codigo: 'volumen', etiqueta: 'volumen', norma: 'RDA 338' },
        { eje: 'soporte', codigo: 'disco-de-audio', etiqueta: 'disco de audio', norma: 'RDA 338' },
      ],
    }));

    const texto = raiz.textContent!.replace(/\s+/g, ' ');
    // UN LIBRO CON SU CD DECLARA DOS SOPORTES: se leen juntos y no en dos filas.
    // LA NORMA NO SE CITA EN LA FICHA PUBLICA. El valor sí se enseña —es lo que describe el
    // objeto—, pero «(RDA 338)» es jerga de catalogación y «MovingImage» es un término inglés del
    // DCMI Type Vocabulary: los dos viven ahora en la consola, que es donde sirven.
    expect(texto).toContain('Soporte');
    expect(texto).toContain('volumen · disco de audio');
    expect(texto).not.toContain('RDA 338');
    expect(texto).not.toContain('Dublin Core');
  });

  it('una consulta física no se pinta como enlace', () => {
    const raiz = montar(recurso({
      access: [{ tipo: 'ubicacion', url: null, ubicacionFisica: 'Biblioteca Nacional', etiqueta: null, nota: null }],
    }));

    expect(raiz.textContent).toContain('Biblioteca Nacional');
    // UN ENLACE QUE NO LLEVA A NINGUN SITIO ES PEOR QUE UN TEXTO.
    expect(raiz.querySelectorAll('a[href]').length).toBe(0);
  });

  it('reconoce el vídeo por su dirección y no por la sección, que puede estar mal escrita', () => {
    const raiz = montar(recurso({
      section: 'Repertorio',
      access: [{ tipo: 'enlace', url: 'https://youtu.be/pT3Lif5DKhU', ubicacionFisica: null, etiqueta: null, nota: null }],
    }));

    const marco = raiz.querySelector('iframe');
    expect(marco).not.toBeNull();
    expect(marco!.getAttribute('src')).toContain('youtube-nocookie.com/embed/pT3Lif5DKhU');
  });

  it('sin vídeo enseña la portada entera, sin recortarla', () => {
    const raiz = montar(recurso());
    const portada = raiz.querySelector('img');

    expect(raiz.querySelector('iframe')).toBeNull();
    // EN UN CATALOGO LA PORTADA ES EL DATO: `object-cover` le cortaba el título a unas y la firma
    // a otras, porque las cubiertas del acervo no comparten proporción.
    expect(portada!.className).toContain('object-contain');
  });

  it('dice qué va a pasar antes de pulsar, y lo deriva del destino y no de la etiqueta', () => {
    // Los 44 enlaces del acervo traen todos la misma etiqueta genérica, «Recurso en línea», así que
    // la etiqueta de la fuente no distingue un PDF de un vídeo. El destino sí.
    const conPdf = montar(recurso({
      access: [{ tipo: 'enlace', url: 'https://mincultura.gov.co/guia.pdf', ubicacionFisica: null, etiqueta: 'Recurso en línea', nota: null }],
    }));
    expect(conPdf.textContent).toContain('Descargar el PDF');
    expect(conPdf.textContent).toContain('mincultura.gov.co');

    const conPagina = montar(recurso({
      access: [{ tipo: 'enlace', url: 'https://www.scribd.com/doc/76693694', ubicacionFisica: null, etiqueta: 'Recurso en línea', nota: null }],
    }));
    expect(conPagina.textContent).toContain('Ver el recurso en línea');
  });

  it('cada vía de consulta aparece UNA vez, sin repetir la acción ni la ubicación', () => {
    // Lo que había: la URL listada arriba como «Enlace», un botón abajo para abrirla, y la misma
    // ubicación bajo dos rótulos distintos —«Consulta física» y «Ubicación de la publicación»—.
    const raiz = montar(recurso({
      access: [
        { tipo: 'enlace', url: 'https://ejemplo.org/a', ubicacionFisica: null, etiqueta: 'Recurso en línea', nota: null },
        { tipo: 'ubicacion', url: null, ubicacionFisica: 'Biblioteca Nacional', etiqueta: null, nota: null },
      ],
    }));

    expect(raiz.querySelectorAll('a[href="https://ejemplo.org/a"]').length).toBe(1);
    expect(raiz.textContent!.split('Biblioteca Nacional').length - 1).toBe(1);
    expect(raiz.textContent).not.toContain('Ubicación de la publicación');
  });

  it('una publicación con varios enlaces ofrece una acción por enlace, no una dirección pegada', () => {
    // MEDIDO: PNMC-ED-104 tiene cuatro enlaces, PNMC-ED-099 tres y PNMC-ED-100 dos. El campo
    // aplanado los unía con «; » y el botón abría una dirección que no existe.
    const raiz = montar(recurso({
      access: [
        { tipo: 'enlace', url: 'https://ejemplo.org/uno', ubicacionFisica: null, etiqueta: null, nota: null },
        { tipo: 'enlace', url: 'https://ejemplo.org/dos', ubicacionFisica: null, etiqueta: null, nota: null },
      ],
    }));

    const enlaces = [...raiz.querySelectorAll('a[href]')].map(a => a.getAttribute('href'));
    expect(enlaces).toContain('https://ejemplo.org/uno');
    expect(enlaces).toContain('https://ejemplo.org/dos');
    expect(enlaces.some(h => (h ?? '').includes(';'))).toBe(false);
  });

  it('pone primero lo que se puede abrir ahora y después dónde ir a verlo', () => {
    const raiz = montar(recurso({
      access: [
        { tipo: 'ubicacion', url: null, ubicacionFisica: 'Biblioteca Nacional', etiqueta: null, nota: null },
        { tipo: 'enlace', url: 'https://ejemplo.org/a.pdf', ubicacionFisica: null, etiqueta: null, nota: null },
      ],
    }));

    const texto = raiz.textContent ?? '';
    expect(texto.indexOf('Descargar el PDF')).toBeLessThan(texto.indexOf('Consulta presencial'));
  });

  it('dice en voz alta cuando no hay ninguna vía de consulta', () => {
    // Cuatro publicaciones del acervo están así. Un hueco se lee como un fallo de carga.
    const raiz = montar(recurso({ access: [] }));

    expect(raiz.textContent).toContain('aún no tiene una vía de consulta registrada');
  });
});
