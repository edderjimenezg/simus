import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AdminCatalogoEditorialPanelComponent, inicialDeTitulo } from './admin-catalogo-editorial-panel.component';
import { CatalogoEditorialService, PublicacionEditorial } from '../../../core/services/catalogo-editorial.service';
import { VocabulariosEditoriales } from '../../../core/contratos/catalogo-editorial';

/**
 * El Catálogo Editorial en la consola.
 *
 * <b>Lo que fijan estas pruebas.</b> Que catalogar y publicar siguen siendo dos decisiones —en la
 * pantalla y no solo en el servidor—, y que cuando publicar se rechaza el motivo del API llega
 * entero a quien cataloga. Un «no fue posible» genérico dejaría adivinando cuál de las cuatro
 * condiciones falta.
 *
 * <b>Y una que ya costó un bucle.</b> El efecto de arranque llama al cargador con `untracked`;
 * sin él se suscribiría a `cargando`, que el propio cargador escribe, y el panel golpearía el
 * API sin parar. Es el mismo defecto que apareció en Gestión del sitio, y por eso se mide igual:
 * contando llamadas.
 */
function ficha(cambios: Partial<PublicacionEditorial> = {}): PublicacionEditorial {
  return {
    id: 1, codigo: 'ED-001', titulo: 'Acento', subtitulo: null, designacionVolumen: null,
    serieOColeccion: null, resumen: null, fechaEdtf: null, anioInicio: 2016, anioFin: 2016,
    idioma: 'es', notaFecha: null,
    tipoPublicacion: null, categoriaId: null, categoria: null, ambito: null, ambitoTexto: null,
    formato: null, categoriaSecundaria: null, palabrasClave: [], miniaturaRuta: null,
    seccionPrincipal: null, rutaSeccion: null, practicaMusical: null, subcategoria: null,
    tamanoFormato: null, paginas: null, duracion: null, camposAdicionales: null, textoPortada: null,
    tipologia: [], confianza: null, revisarClasificacion: false, revisarCreditos: false,
    notasCatalogacion: null, diapositivaOrigen: null,
    estadoCatalogacion: 'pendiente_revision', estadoPublicacion: 'borrador', version: 1,
    derechos: { estado: 'pendiente', permitePublicarFicha: false, permitePublicarArchivo: false,
      licenciaONota: null, fuenteId: null, fechaVerificacion: null, verificadoPor: null },
    fuentes: [], creditos: [], identificadores: [], accesos: [], programas: [],
    practicasMusicales: [], territoriosSonoros: [], procedencia: null,
    fechaActualizacion: '2026-09-11T00:00:00Z', ...cambios,
  };
}

class ApiDoble {
  llamadas = 0;
  items: PublicacionEditorial[] = [ficha()];
  respuestaPublicar: { ok: boolean; data?: PublicacionEditorial | null; error?: string } =
    { ok: true, data: ficha({ estadoPublicacion: 'publicado' }) };

  /** El acervo entero: es lo que pide ahora la consola para poder facetar. */
  async listarTodasInternas() {
    this.llamadas += 1;
    return { ok: true, data: this.items };
  }

  async listarInternas() {
    this.llamadas += 1;
    return { ok: true, data: { items: this.items, pagina: 1, tamano: 20, total: this.items.length, totalPaginas: 1 } };
  }
  /**
   * Las listas del formulario.
   *
   * DEVUELVE ALGO, aunque el panel sepa seguir sin ellas: si el doble no las tiene, la llamada
   * revienta al construir el componente y todas las pruebas del panel caen por una razón que no
   * tiene nada que ver con lo que miden.
   */
  vocabulariosPedidos = 0;
  async vocabularios(): Promise<VocabulariosEditoriales> {
    this.vocabulariosPedidos += 1;
    return {
      siguienteCodigo: 'PNMC-ED-172',
      idiomas: [
        { codigo: 'es', nombre: 'Español', usos: 164, pendienteDePrecisar: false },
        { codigo: 'en', nombre: 'Inglés', usos: 2, pendienteDePrecisar: false },
        { codigo: '(lengua nativa)', nombre: 'Lengua nativa (sin especificar)', usos: 5, pendienteDePrecisar: true },
      ],
      tiposDePublicacion: [{ valor: 'DVD', usos: 30 }, { valor: 'Libro impreso', usos: 14 }],
      ambitos: [{ valor: 'Nacional', usos: 149 }],
      formatos: [{ valor: 'Físico', usos: 137 }],
      rutas: [
        { ruta: 'Repertorio > Banda', seccion: 'Repertorio', usos: 12 },
        { ruta: 'Formación > Pedagogía Instrumental > Guías y Cuadernos de instrumento', seccion: 'Formación', usos: 9 },
      ],
      practicasMusicales: [{ valor: 'Banda', usos: 56 }],
      subcategorias: [{ valor: 'Pedagogía y formación', usos: 58 }],
      categoriasSecundarias: [{ valor: 'Orquesta', usos: 27 }],
      esquemasIdentificador: [{ valor: 'ISBN', usos: 0 }, { valor: 'ISMN', usos: 0 }],
      rolesDeCredito: [{ codigo: 'aut', etiqueta: 'Autor', usos: 285 }],
      tipologias: [{ eje: 'contenido', codigo: 'texto', etiqueta: 'texto', norma: 'RDA 336' }],
      tiposDeAgente: ['persona', 'entidad'],
    };
  }
  async cambiarCatalogacion() { return { ok: true, data: ficha({ estadoCatalogacion: 'validada' }) }; }
  async cambiarPublicacion() { return this.respuestaPublicar; }
}

describe('AdminCatalogoEditorialPanelComponent', () => {
  let api: ApiDoble;

  async function montar() {
    api = new ApiDoble();
    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      imports: [AdminCatalogoEditorialPanelComponent],
      providers: [provideRouter([]), { provide: CatalogoEditorialService, useValue: api }],
    }).compileComponents();
    const fixture = TestBed.createComponent(AdminCatalogoEditorialPanelComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return fixture;
  }

  it('consulta el catálogo UNA sola vez al abrirse, no en bucle', async () => {
    await montar();
    expect(api.llamadas).toBe(1);
  });

  it('presenta catalogación y publicación como dos estados distintos', async () => {
    const fixture = await montar();
    const texto = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(texto).toContain('Pendiente de revisión');
    expect(texto).toContain('Borrador');
  });

  it('avisa en la fila de lo que le falta a una ficha para poder publicarse', async () => {
    const fixture = await montar();
    const texto = (fixture.nativeElement as HTMLElement).textContent ?? '';

    // Las dos condiciones que se olvidan: sin ellas el servidor rechaza publicar.
    expect(texto).toContain('0 fuentes');
    expect(texto).toContain('sin derechos');
  });

  it('cuando publicar se rechaza, enseña el motivo del servidor sin traducirlo', async () => {
    const fixture = await montar();
    api.respuestaPublicar = { ok: false, error: 'No se puede publicar: la ficha no está validada; no tiene ninguna fuente registrada.' };

    await fixture.componentInstance.publicar(ficha(), 'publicado');
    fixture.detectChanges();

    expect(fixture.componentInstance.error()).toContain('no tiene ninguna fuente registrada');
  });

  it('sin permiso de publicación no ofrece el botón de publicar', async () => {
    const fixture = await montar();
    const raiz = fixture.nativeElement as HTMLElement;

    // LAS ACCIONES VIVEN EN UN DESPLEGABLE desde, así que hay que
    // abrirlo. Se miran las OPCIONES, no el texto de la página: la explicación de cabecera dice
    // «Publicar dice si se ve», y buscar esa palabra en todo el cuerpo daría un falso positivo.
    raiz.querySelector<HTMLButtonElement>('tbody [data-disparador-acciones]')!.click();
    fixture.detectChanges();
    const opciones = Array.from(raiz.querySelectorAll('tbody [data-opcion-accion]'))
      .map(b => (b.textContent ?? '').trim());

    // `puedePublicar` llega en false por omisión: publicar y despublicar son del webmaster.
    // Editar y validar sí los hace un gestor interno, y por eso siguen ofreciéndose.
    //
    // «HISTORIAL» NO DEPENDE DEL PERMISO DE PUBLICAR, y por eso está aquí: consultar quién ha
    // tocado una ficha es lectura, y negársela a quien la cataloga la dejaría trabajando a ciegas
    // sobre el trabajo de otra persona.
    //
    // «PREVISUALIZAR» TAMPOCO DEPENDE DEL PERMISO DE PUBLICAR, y es deliberado: quien cataloga sin
    // poder publicar es justo quien necesita ver cómo va a quedar la ficha antes de pedir que la
    // publique otra persona. Solo aparece mientras la ficha NO esté publicada; cuando lo está, lo
    // que sirve es «Ver en el portal».
    // «VER FICHA» VA PRIMERO Y ESTA SIEMPRE: consultar es lo único que se puede hacer con cualquier
    // ficha, esté en el estado que esté, y no depende de ningún permiso de publicación.
    expect(opciones).toEqual(['Abrir ficha', 'Editar', 'Validar', 'Previsualizar', 'Historial']);

    // Y NO HAY ACCION ESPERADA: «Publicar» es la única que se marca como tal, y sin permiso no
    // existe. La comprobación se hace con el menú ya abierto —ahí es donde vive desde el 14 de
    // septiembre de 2026—, así que un `null` significa que no está, no que no se ha abierto.
    expect(raiz.querySelector('tbody [data-accion-principal]')).toBeNull();
  });
});

/**
 * El formulario de alta y edición.
 *
 * <b>Lo que fija.</b> Que el código sea la identidad estable —no se cambia una vez creada la
 * ficha, porque de él cuelgan la URL pública y la portada— y que guardar una ficha existente cite
 * la versión que se abrió, para que dos personas editando a la vez no se pisen en silencio.
 */
describe('AdminCatalogoEditorialPanelComponent · formulario', () => {
  /** El mismo montaje que repetían las pruebas de este bloque, escrito una sola vez. */
  async function montarFormulario() {
    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      imports: [AdminCatalogoEditorialPanelComponent],
      providers: [provideRouter([]), { provide: CatalogoEditorialService, useValue: new ApiDoble() }],
    }).compileComponents();
    const fixture = TestBed.createComponent(AdminCatalogoEditorialPanelComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return fixture.componentInstance;
  }

  it('una ficha nueva nace sin identificador y sin versión que citar', async () => {
    const fixture = await (async () => {
      const api = new ApiDoble();
      TestBed.resetTestingModule();
      await TestBed.configureTestingModule({
        imports: [AdminCatalogoEditorialPanelComponent],
        providers: [provideRouter([]), { provide: CatalogoEditorialService, useValue: api }],
      }).compileComponents();
      const f = TestBed.createComponent(AdminCatalogoEditorialPanelComponent);
      f.detectChanges();
      await f.whenStable();
      f.detectChanges();
      return f;
    })();

    fixture.componentInstance.nueva();

    expect(fixture.componentInstance.ficha()?.id).toBeNull();
    expect(fixture.componentInstance.ficha()?.version).toBe(0);
  });

  it('editar arrastra la versión leída, que es lo que impide pisar trabajo ajeno', async () => {
    const api = new ApiDoble();
    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      imports: [AdminCatalogoEditorialPanelComponent],
      providers: [provideRouter([]), { provide: CatalogoEditorialService, useValue: api }],
    }).compileComponents();
    const fixture = TestBed.createComponent(AdminCatalogoEditorialPanelComponent);
    fixture.detectChanges();
    await fixture.whenStable();

    fixture.componentInstance.editar(ficha({ version: 7, codigo: 'PNMC-ED-010', palabrasClave: ['banda', 'arreglos'] }));

    expect(fixture.componentInstance.ficha()?.version).toBe(7);
    expect(fixture.componentInstance.ficha()?.codigo).toBe('PNMC-ED-010');
    // Se editan como una línea y el servidor las recibe separadas: así las dicta quien cataloga.
    expect(fixture.componentInstance.ficha()?.palabrasClave).toBe('banda, arreglos');
  });

  describe('el recorrido del formulario', () => {
    /**
     * LO QUE FIJAN ESTAS PRUEBAS. Tres correcciones que pidió la dirección de producto sobre el
     * formulario ya en uso: que el código no se escriba, que el idioma deje de enseñar
     * combinaciones como si fueran idiomas, y que no se pida descripción física de algo que no
     * tiene objeto físico.
     */
    it('no pide el código: lo asigna el servidor al guardar', async () => {
      // Enseñarlo en solo lectura tampoco resolvía nada —«no tiene sentido que diga código y que
      // diga se asigna al guardar»— y ocupaba la primera ranura de la pantalla.
      const componente = await montarFormulario();
      componente.nueva();

      expect(componente.ficha()?.codigo).toBe('');
      expect(componente.resumenDeLaFicha().map(f => f.rotulo)).not.toContain('Código');
    });

    it('se puede guardar una ficha nueva sin escribir ningún código', async () => {
      // REGRESION DE VERDAD, y ninguna prueba la miraba: al quitar el código del formulario, la
      // condición del botón se quedó pidiéndolo, así que Guardar nunca se habilitaba y NO SE PODIA
      // CREAR NINGUNA FICHA. La destapó el recorrido completo de un registro en navegador.
      const componente = await montarFormulario();
      componente.nueva();
      componente.campo('titulo', 'Una publicación');

      const f = componente.ficha()!;
      expect(f.codigo).toBe('');
      expect(componente.sePuedeGuardar()).toBe(true);
    });

    it('pero no sin título, que es lo único que de verdad hace falta', async () => {
      const componente = await montarFormulario();
      componente.nueva();

      expect(componente.sePuedeGuardar()).toBe(false);
    });

    it('el idioma se elige, y un segundo idioma se agrega', async () => {
      // La versión anterior pintaba una casilla por idioma del vocabulario. Para 171 fichas de las
      // que 164 son solo en español, puntear una lista obliga a todos a resolver el caso raro.
      const componente = await montarFormulario();
      componente.nueva();

      expect(componente.idiomasDeLaFicha()).toEqual(['es']);

      componente.agregarIdioma();
      expect(componente.idiomasDeLaFicha().length).toBe(2);

      componente.cambiarIdioma(1, 'en');
      expect(componente.ficha()?.idioma).toBe('es ; en');

      componente.cambiarIdioma(1, '');
      expect(componente.ficha()?.idioma).toBe('es');
    });

    it('la sección sale de la ubicación elegida, no se pide aparte', async () => {
      // Comprobado: el primer tramo de la ruta ES la sección en las 170 fichas que tienen ambas, y hay
      // 22 rutas para 9 secciones. Pedir las dos por separado deja que discrepen.
      const componente = await montarFormulario();
      componente.nueva();

      componente.elegirRuta('Formación > Pedagogía Instrumental > Guías y Cuadernos de instrumento');

      expect(componente.ficha()?.seccionPrincipal).toBe('Formación');
      expect(componente.ficha()?.rutaSeccion).toContain('Guías y Cuadernos');
    });

    it('agrupa las ubicaciones por sección y no repite la sección en cada opción', async () => {
      const componente = await montarFormulario();
      const grupos = componente.rutasPorSeccion();

      expect(grupos.map(g => g.seccion)).toEqual(['Repertorio', 'Formación']);
      expect(grupos[0].rutas[0].hoja).toBe('Banda');
    });

    it('no pide tamaño ni páginas de algo que no tiene objeto físico', async () => {
      const componente = await montarFormulario();
      componente.nueva();

      componente.campo('formato', 'Digital');
      expect(componente.aplicaDescripcionFisica()).toBe(false);

      componente.campo('formato', 'Mixto');
      expect(componente.aplicaDescripcionFisica()).toBe(true);
    });

    it('pero no esconde un dato que alguien ya escribió', async () => {
      // Esconder el campo dejaría la ficha sin forma de corregir lo que tiene escrito.
      const componente = await montarFormulario();
      componente.nueva();

      componente.campo('formato', 'Digital');
      componente.campo('paginas', '500 p');

      expect(componente.aplicaDescripcionFisica()).toBe(true);
    });

    it('la duración la gobierna el contenido y no el soporte', async () => {
      // Comprobado: 33 publicaciones físicas la declaran, pero también DOS videoclips digitales.
      const componente = await montarFormulario();
      componente.nueva();

      componente.campo('formato', 'Digital');
      expect(componente.aplicaDuracion()).toBe(false);

      componente.campo('tipologias', ['imagen-movimiento']);
      expect(componente.aplicaDuracion()).toBe(true);
    });
  });
});
describe('inicialDeTitulo', () => {
  /**
   * LOS TRES CASOS SON DEL ACERVO, no inventados: «¡Ay ’ombe juepa jé!», «¡Que viva San Juan, que
   * viva San Pedro!» y «“Ramón el camaleón”» quedaban archivados bajo «¡» y bajo la comilla, en
   * botones que no son letras y que nadie pulsaría buscando nada.
   */
  it('se salta los signos de apertura y las comillas', () => {
    expect(inicialDeTitulo('¡Que viva San Juan, que viva San Pedro!')).toBe('Q');
    expect(inicialDeTitulo('¡Ay ’ombe juepa jé!')).toBe('A');
    expect(inicialDeTitulo('“Ramón el camaleón”')).toBe('R');
    expect(inicialDeTitulo('«Acento»')).toBe('A');
  });

  it('una vocal con tilde se archiva con la misma letra sin tilde', () => {
    expect(inicialDeTitulo('Álbum de coros')).toBe('A');
    expect(inicialDeTitulo('Ópera para bandas')).toBe('O');
  });

  it('pero la eñe es una letra propia y no una ene con adorno', () => {
    // Quitar diacríticos a lo bruto la convertiría en N, y en español tiene su sitio en el alfabeto.
    expect(inicialDeTitulo('Ñapanga')).toBe('Ñ');
  });

  it('los títulos que empiezan por cifra van todos juntos', () => {
    // El acervo empieza títulos con 1, 2 y 8: cinco publicaciones en tres botones de un solo dígito.
    expect(inicialDeTitulo('8 Arreglos para Banda')).toBe('0-9');
    expect(inicialDeTitulo('26 Arreglos Musicales')).toBe('0-9');
  });

  it('un título sin ninguna letra ni cifra no se archiva en ningún sitio', () => {
    expect(inicialDeTitulo('¿¡…!?')).toBe('');
    expect(inicialDeTitulo('')).toBe('');
  });
});

describe('AdminCatalogoEditorialPanelComponent · el índice del acervo', () => {
  /**
   * LO QUE FIJAN ESTAS PRUEBAS. La consola listaba las fichas como una bandeja de trabajo —buscar
   * por título y filtrar por estado—, y el criterio pide otra cosa: 171 publicaciones
   * INDEPENDIENTES que hay que poder recorrer por ubicación, por autoría y por práctica musical,
   * «de manera completa, clara, gestionable».
   */
  function acervoDePrueba(): PublicacionEditorial[] {
    const credito = (nombre: string, principal = true) => ({
      id: 1, agenteId: 1, agenteNombre: nombre, agenteTipo: 'entidad' as const,
      rolCodigo: 'aut', rolEtiqueta: 'Autor', principal, orden: 0,
    });
    return [
      // LAS RUTAS TIENEN TRES NIVELES A PROPOSITO. El acervo real los tiene —«Repertorio > Música
      // Popular y Tradicional > Homenajes»— y con rutas de dos el defecto del nivel intermedio no
      // se puede reproducir: solo aparece cuando hay algo POR DEBAJO del nivel que se elige.
      ficha({ id: 1, codigo: 'ED-001', titulo: 'Acento', seccionPrincipal: 'Repertorio',
        rutaSeccion: 'Repertorio > Banda > Homenajes',
        practicaMusical: 'Banda', formato: 'Físico', anioInicio: 2002,
        creditos: [credito('Ministerio de Cultura')] }),
      ficha({ id: 2, codigo: 'ED-002', titulo: 'Bambuqueando', seccionPrincipal: 'Formación',
        rutaSeccion: 'Formación > Pedagogía Instrumental',
        practicaMusical: 'Banda', formato: 'Digital', anioInicio: 2016,
        creditos: [credito('Ministerio de Cultura')] }),
      ficha({ id: 3, codigo: 'ED-003', titulo: 'Cantoría', seccionPrincipal: 'Formación',
        rutaSeccion: 'Formación > Pedagogía Instrumental > Guías y Cuadernos',
        practicaMusical: 'Coro y música vocal', formato: 'Físico', anioInicio: 2011,
        creditos: [credito('Fundación Canto')] }),
    ];
  }

  async function montarConAcervo() {
    const api = new ApiDoble();
    api.items = acervoDePrueba();
    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      imports: [AdminCatalogoEditorialPanelComponent],
      providers: [provideRouter([]), { provide: CatalogoEditorialService, useValue: api }],
    }).compileComponents();
    const fixture = TestBed.createComponent(AdminCatalogoEditorialPanelComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return fixture.componentInstance;
  }

  it('entra enseñando el acervo entero, sin ningún filtro puesto', async () => {
    // Una pantalla que arranca filtrada esconde fichas sin decirlo.
    const componente = await montarConAcervo();

    expect(componente.fichasVisibles().length).toBe(3);
    expect(componente.hayFiltros()).toBe(false);
  });

  it('cuenta cada faceta y filtra por ella', async () => {
    const componente = await montarConAcervo();

    expect(componente.practicas()).toEqual([
      { valor: 'Banda', cuantas: 2 },
      { valor: 'Coro y música vocal', cuantas: 1 },
    ]);

    componente.alternarPractica('Banda');
    expect(componente.fichasVisibles().map(f => f.codigo)).toEqual(['ED-001', 'ED-002']);
  });

  it('recalcula los recuentos de las OTRAS facetas, pero no los de la propia', async () => {
    // SI SE CONTARA SOBRE EL ACERVO ENTERO, con «Banda» puesto el panel seguiría prometiendo
    // «Formación: 2» y al pulsarlo aparecería una. Y si se contara sobre el resultado final, la
    // faceta elegida se quedaría sola y no se podría cambiar de idea sin borrar el filtro.
    const componente = await montarConAcervo();
    componente.alternarPractica('Banda');

    expect(componente.ubicaciones()).toEqual([
      { valor: 'Formación', cuantas: 1 },
      { valor: 'Repertorio', cuantas: 1 },
    ]);
    // La propia faceta sigue ofreciendo sus dos opciones: cambiar de idea no exige limpiar antes.
    expect(componente.practicas().length).toBe(2);
  });

  it('la búsqueda también mira la autoría, que es como se busca de verdad', async () => {
    const componente = await montarConAcervo();

    componente.texto.set('Fundación');

    expect(componente.fichasVisibles().map(f => f.codigo)).toEqual(['ED-003']);
  });

  it('el índice de autoría solo lista a quien tiene más de una obra', async () => {
    // Con 410 agentes en el acervo real, listarlos todos es un directorio telefónico. Quien aparece
    // una sola vez se alcanza escribiendo su nombre.
    const componente = await montarConAcervo();

    expect(componente.indiceDeAutoria()).toEqual([{ nombre: 'Ministerio de Cultura', cuantas: 2 }]);
  });

  it('ordena por título, por código o por año, y el desplegable trae su sentido', async () => {
    // EL ORDEN VIVE EN LA PIEZA COMPARTIDA DE TABLAS desde, para que
    // el desplegable y las cabeceras -que antes no ordenaban- escriban en el mismo sitio.
    const componente = await montarConAcervo();

    // «Por año» es un criterio COMPLETO: dice por qué ordena y hacia dónde, de la más reciente.
    componente.ordenarPorCriterio('anio');
    expect(componente.fichasVisibles().map(f => f.anioInicio)).toEqual([2016, 2011, 2002]);

    componente.ordenarPorCriterio('obra');
    expect(componente.fichasVisibles().map(f => f.titulo)).toEqual(['Acento', 'Bambuqueando', 'Cantoría']);
  });

  it('pulsar la cabecera «Obra» ordena, y pulsarla otra vez le da la vuelta', async () => {
    // La tabla no ordenaba al pulsar sus columnas: lo reportó la dirección de producto el 15 de
    // septiembre de 2026 sobre esta y sobre otras siete.
    const componente = await montarConAcervo();

    // La tabla ABRE ordenada por «Obra» ascendente, así que el primer golpe sobre esa cabecera es
    // ya el que la invierte: es lo mismo que hace cualquier tabla con su columna de partida.
    expect(componente.fichasVisibles().map(f => f.titulo)).toEqual(['Acento', 'Bambuqueando', 'Cantoría']);

    componente.orden.alternar('obra');
    expect(componente.fichasVisibles().map(f => f.titulo)).toEqual(['Cantoría', 'Bambuqueando', 'Acento']);

    componente.orden.alternar('obra');
    expect(componente.fichasVisibles().map(f => f.titulo)).toEqual(['Acento', 'Bambuqueando', 'Cantoría']);

    // Y cambiar de columna NO hereda el sentido de la anterior, como en las otras nueve tablas.
    componente.orden.alternar('obra');
    componente.orden.alternar('publicacion');
    expect(componente.orden.direccion()).toBe('asc');
  });

  it('una ruta intermedia encuentra todo lo que cuelga de ella', async () => {
    // EL FILTRO COMPARABA LA RUTA ENTERA, LETRA A LETRA, así que elegir un nivel intermedio de una
    // ruta de tres —«Repertorio > Música Popular y Tradicional», con fichas en «... > Homenajes»—
    // no encontraba NADA: ninguna ficha tiene exactamente esa ruta, la tienen más larga. Lo
    // reportó la dirección de producto con ese mismo ejemplo.
    const componente = await montarConAcervo();

    componente.explorarDesdeElTramo('Formación > Pedagogía Instrumental > Guías y Cuadernos', 1);

    expect(componente.facetaRuta()).toBe('Formación > Pedagogía Instrumental');
    // Las DOS: la que está exactamente en ese nivel y la que cuelga de él.
    expect(componente.fichasVisibles().map(f => f.titulo).sort()).toEqual(['Bambuqueando', 'Cantoría']);

    // Y el primer tramo se queda en la sección, sin bajar un nivel que nadie pidió.
    componente.explorarDesdeElTramo('Formación > Pedagogía Instrumental > Guías y Cuadernos', 0);
    expect(componente.facetaRuta()).toBe('');
    expect(componente.facetaUbicacion()).toBe('Formación');
  });

  it('el árbol de ubicación no se lo lleva su propio filtro', async () => {
    // Con una ruta puesta, ninguna otra sección puede tener fichas POR CONSTRUCCION, así que la
    // lista de ubicaciones se quedaba en una y el `@if` del árbol lo borraba de la pantalla: no
    // había forma de quitar el filtro desde el control que lo puso. «Se compacta automáticamente
    // todas las subcategorías», lo describió la dirección de producto.
    const componente = await montarConAcervo();

    const antes = componente.hayVariasUbicaciones();
    componente.explorarDesdeElTramo('Repertorio > Banda', 1);

    expect(antes).toBe(componente.hayVariasUbicaciones());
  });

  it('quitar un filtro no pliega el árbol', async () => {
    // Lo plegaba: el efecto que escribe la dirección dispara una navegación, y al releerla se
    // hacía `seccionAbierta.set(facetaUbicacion())`, que con la ubicación vacía cerraba todo.
    const componente = await montarConAcervo();

    componente.alternarUbicacion('Repertorio');
    expect(componente.seccionAbierta()).toBe('Repertorio');

    componente.alternarUbicacion('Repertorio');
    expect(componente.facetaUbicacion()).toBe('');
    // Dejar de filtrar por una sección no es dejar de querer ver qué hay dentro.
    expect(componente.seccionAbierta()).toBe('Repertorio');
  });

  it('la ubicación es un árbol: elegir la sección despliega sus rutas', async () => {
    // Son el mismo gesto porque son la misma intención: mirar ahí dentro. Y las 22 rutas juntas
    // serían una lista plana que no enseña que «Repertorio > Banda» está DENTRO de «Repertorio».
    const componente = await montarConAcervo();

    componente.alternarUbicacion('Formación');

    expect(componente.seccionAbierta()).toBe('Formación');
    expect(componente.fichasVisibles().length).toBe(2);
  });

  it('la ruta activa enseña el camino y deja retroceder un paso', async () => {
    // Con las facetas repartidas por la columna, después de tres clics nadie sabe qué tiene puesto
    // y la única salida era borrarlo todo.
    const componente = await montarConAcervo();
    componente.alternarUbicacion('Formación');
    componente.alternarPractica('Banda');

    expect(componente.rutaActiva().map(p => p.eje)).toEqual(['Ubicación', 'Práctica']);

    componente.rutaActiva()[1].quitar();

    expect(componente.rutaActiva().map(p => p.eje)).toEqual(['Ubicación']);
    expect(componente.fichasVisibles().length).toBe(2);
  });

  it('quitar la sección quita también la ruta fina que colgaba de ella', async () => {
    // Una ruta sin su sección es un filtro huérfano que nadie puso a conciencia.
    const componente = await montarConAcervo();
    componente.alternarUbicacion('Formación');
    componente.alternarRuta('Formación > Pedagogía');

    componente.rutaActiva()[0].quitar();

    expect(componente.facetaRuta()).toBe('');
  });

  it('el abecedario se cuenta sobre lo ya filtrado, no sobre el acervo entero', async () => {
    // Contándolo sobre el acervo entero, las letras seguían pulsables aunque los filtros activos
    // las hubieran vaciado: se pulsaba una y el listado se quedaba en cero.
    const componente = await montarConAcervo();
    const conTodo = componente.abecedario().filter(x => x.cuantas > 0).map(x => x.letra);
    expect(conTodo).toEqual(['A', 'B', 'C']);

    componente.alternarPractica('Coro y música vocal');

    expect(componente.abecedario().filter(x => x.cuantas > 0).map(x => x.letra)).toEqual(['C']);
  });

  it('los años se agrupan por lustros y se leen en orden cronológico', async () => {
    // Veinticuatro años distintos son veinticuatro filas casi todas de una ficha. Por lustros
    // quedan siete tramos con reparto real. Y una línea de tiempo se lee en orden, no por cantidad.
    const componente = await montarConAcervo();

    expect(componente.lustros()).toEqual([
      { valor: '2000–2004', cuantas: 1 },
      { valor: '2010–2014', cuantas: 1 },
      { valor: '2015–2019', cuantas: 1 },
    ]);
  });

  it('no ofrece la faceta de estado mientras todas las fichas estén igual', async () => {
    // Hoy las 171 están «validada · publicado»: ofrecerla sería un botón que no quita ninguna ficha.
    const componente = await montarConAcervo();

    expect(componente.estados()).toEqual([]);
  });

  it('la ruta se parte en tramos para poder pintarla como jerarquía', async () => {
    // Escrita de corrido, «Formación > Pedagogía Instrumental > Guías y Cuadernos» es una línea que
    // se salta al leer. Partida, se ve de dónde cuelga la ficha.
    const componente = await montarConAcervo();

    expect(componente.tramosDeLaRuta('Formación > Pedagogía Instrumental > Guías'))
      .toEqual(['Formación', 'Pedagogía Instrumental', 'Guías']);
    expect(componente.tramosDeLaRuta(null)).toEqual([]);
  });

  it('desde una ficha se salta al resto de su ruta, limpiando lo demás', async () => {
    // El índice llevaba del acervo a una publicación, pero desde la publicación no se volvía. Y se
    // limpia lo anterior a propósito: el resultado es «todo lo de esta ruta», no «lo de esta ruta
    // que además cumplía los filtros que traía puestos», que casi nunca es lo que se quiere.
    const componente = await montarConAcervo();
    componente.alternarPractica('Banda');

    componente.explorarDesdeLaFicha('ruta', 'Formación > Pedagogía Instrumental');

    expect(componente.facetaPractica()).toBe('');
    expect(componente.facetaUbicacion()).toBe('Formación');
    expect(componente.facetaRuta()).toBe('Formación > Pedagogía Instrumental');
    expect(componente.seccionAbierta()).toBe('Formación');
  });

  it('y al resto de la obra de una autoría', async () => {
    const componente = await montarConAcervo();

    componente.explorarDesdeLaFicha('agente', 'Ministerio de Cultura');

    expect(componente.facetaAgente()).toBe('Ministerio de Cultura');
    expect(componente.fichasVisibles().length).toBe(2);
  });

  it('la selección solo alcanza a lo que se está viendo', async () => {
    // Una acción que alcanzara a registros fuera de la pantalla es justo el accidente del que hay
    // que proteger cuando se actúa sobre varios a la vez.
    const componente = await montarConAcervo();

    componente.alternarTodas();
    expect(componente.seleccionadas().length).toBe(3);

    componente.alternarPractica('Coro y música vocal');
    expect(componente.seleccionadas().length).toBe(1);
  });

  it('marcar todas alterna, y filtrar después no arrastra lo que ya no se ve', async () => {
    const componente = await montarConAcervo();

    componente.alternarTodas();
    expect(componente.todasSeleccionadas()).toBe(true);

    componente.alternarTodas();
    expect(componente.seleccionadas().length).toBe(0);
  });

  it('lo que falla al actuar sobre varias se dice con su código', async () => {
    // «No se pudieron publicar 3» obliga a revisar las doce a mano para encontrarlas.
    const componente = await montarConAcervo();
    componente.alternarTodas();

    await componente.publicarSeleccionadas('publicado');

    // El doble acepta todas, así que no hay fallos y sí un aviso con el recuento.
    expect(componente.aviso()).toContain('3');
    expect(componente.seleccionadas().length).toBe(0);
  });

  it('quitar los filtros devuelve el acervo entero', async () => {
    const componente = await montarConAcervo();
    componente.alternarPractica('Banda');
    componente.texto.set('Acento');

    componente.limpiarFiltros();

    expect(componente.fichasVisibles().length).toBe(3);
    expect(componente.hayFiltros()).toBe(false);
  });
});
