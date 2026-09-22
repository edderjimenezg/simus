import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import {
  CatalogoFestivalPublico,
  FestivalPublico,
  FestivalesPublicosService,
  ResumenAnaliticoFestivales,
} from '../../../../core/services/festivales-publicos.service';
import { DEFAULT_TEXTS } from '../../../../core/cms/registro-de-textos-web';
import { TextosWebService } from '../../../../core/services/textos-web.service';
import {
  FestivalesPublicosPageComponent,
  TAMANO_PAGINA_FESTIVALES,
} from './festivales-publicos-page.component';

/**
 * El directorio publico de Festivales: filtros, contadores y paginacion.
 *
 * POR QUE EXISTE. La pagina anterior tenia un solo buscador y tres cifras que
 * NO se movian al buscar: salian de `/analitica/festivales/resumen`, que cuenta
 * el total publicado. Al escribir en el buscador la lista bajaba a dos filas y
 * el encabezado seguia diciendo el total — un numero cierto puesto donde
 * describe otra cosa.
 *
 * LO QUE SE VIGILA AQUI es que cada cifra de la cabecera se calcule sobre la
 * CONSULTA FILTRADA, que los desplegables no ofrezcan combinaciones imposibles,
 * y que la lista nunca diga «N resultados» pintando menos sin avisar. El aspecto
 * —colores, rejilla, iconos— no se prueba: cambia por decision de diseño.
 */
describe('FestivalesPublicosPageComponent · consulta pública', () => {
  const catalogo = (id: number, nombre: string): CatalogoFestivalPublico => ({ id, nombre });

  const festival = (parcial: Partial<FestivalPublico> & { id: string; nombre: string }): FestivalPublico => ({
    descripcion: null,
    organizacionResponsable: null,
    territorioPrincipal: { departamento: null, municipio: null, nivelCobertura: 'municipal' },
    periodicidad: null,
    practicasMusicales: [],
    territoriosSonoros: [],

    instagram: null,
    facebook: null,
    sitioWeb: null,
    otroEnlace: null,
    ...parcial,
  });

  /**
   * Cuatro registros que reproducen rarezas REALES de
   * `/api/v1/publico/festivales` el 30 ago 2026: municipios con tilde
   * («ABRIAQUÍ»), `periodicidad` guardada como «anual» y «Anual», y fichas sin
   * ningun enlace publicado.
   */
  const FESTIVALES: FestivalPublico[] = [
    festival({
      id: '1',
      nombre: 'Festival PNMC 01 - ABEJORRAL',
      descripcion: 'Encuentro de bandas del oriente antioqueño.',
      organizacionResponsable: 'Plan Nacional de Música para la Convivencia',
      territorioPrincipal: { departamento: 'ANTIOQUIA', municipio: 'ABEJORRAL', nivelCobertura: 'municipal' },
      periodicidad: 'anual',
      practicasMusicales: [catalogo(10, 'Bandas de marcha')],
      territoriosSonoros: [catalogo(5, 'Chirimía')],
      sitioWeb: 'https://festivalpnmc01.local',
    }),
    festival({
      id: '2',
      nombre: 'Festival PNMC 02 - ABRIAQUÍ',
      organizacionResponsable: 'Plan Nacional de Música para la Convivencia',
      territorioPrincipal: { departamento: 'ANTIOQUIA', municipio: 'ABRIAQUÍ', nivelCobertura: 'municipal' },
      periodicidad: 'Anual',
      practicasMusicales: [catalogo(4, 'Músicas comunitarias')],
      territoriosSonoros: [catalogo(8, 'Amazonas')],
    }),
    festival({
      id: '3',
      nombre: 'Festival PNMC 11 - BARANOA',
      organizacionResponsable: 'Pruebas',
      territorioPrincipal: { departamento: 'ATLÁNTICO', municipio: 'BARANOA', nivelCobertura: 'departamental' },
      practicasMusicales: [catalogo(10, 'Bandas de marcha')],
      territoriosSonoros: [catalogo(0, 'Cantos, Pitos y Tambores')],
      instagram: 'https://instagram.com/festivalpnmc11',
    }),
    festival({
      id: '4',
      nombre: 'Festival PNMC 26 - ACACÍAS',
      organizacionResponsable: 'Pruebas',
      territorioPrincipal: { departamento: 'META', municipio: 'ACACÍAS', nivelCobertura: 'municipal' },
      practicasMusicales: [catalogo(11, 'Músicas académicas')],
      territoriosSonoros: [catalogo(5, 'Chirimía')],
    }),
  ];

  const RESUMEN: ResumenAnaliticoFestivales = {
    totalFestivales: 4,
    porDepartamento: [],
    porMunicipio: [],
    porPracticaMusical: [],
    porTerritorioSonoro: [],
    porPeriodicidad: [],
  };

  /**
   * `queryParams` simula lo que ya trae la URL al entrar -recarga, «Atrás» o un enlace
   * compartido-; `navegar` es el espía sobre `Router.navigate` que las pruebas de sincronización
   * usan para comprobar qué escribe el componente de vuelta en la URL.
   */
  const montar = (
    items: FestivalPublico[] = FESTIVALES,
    total = items.length,
    queryParams: Record<string, string> = {},
  ) => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [FestivalesPublicosPageComponent],
      providers: [
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { queryParamMap: convertToParamMap(queryParams) } },
        },
        {
          provide: TextosWebService,
          useValue: { getWebText: (clave: string) => DEFAULT_TEXTS[clave] ?? '' },
        },
        {
          provide: FestivalesPublicosService,
          useValue: {
            consultarFestivales: () => of({ items, limit: 500, offset: 0, total }),
            consultarResumenAnalitico: () => of(RESUMEN),
          },
        },
      ],
    });

    const fixture = TestBed.createComponent(FestivalesPublicosPageComponent);
    const navegar = spyOn(TestBed.inject(Router), 'navigate');
    fixture.detectChanges();
    return { fixture, componente: fixture.componentInstance, navegar };
  };

  // --- Contadores --------------------------------------------------------------

  it('las cifras de la cabecera describen la consulta filtrada, no el total', () => {
    const { componente } = montar();

    expect(componente.totalResultados()).toBe(4);
    expect(componente.totalDepartamentos()).toBe(3);
    expect(componente.totalMunicipios()).toBe(4);
    expect(componente.totalPracticas()).toBe(3);
    expect(componente.totalTerritoriosSonoros()).toBe(3);
    expect(componente.totalOrganizaciones()).toBe(2);

    componente.actualizarDepartamento('ANTIOQUIA');

    // Este es el defecto que la prueba persigue: con las cifras colgadas del
    // resumen analitico, las seis seguirian diciendo lo mismo de arriba.
    expect(componente.totalResultados()).toBe(2);
    expect(componente.totalDepartamentos()).toBe(1);
    expect(componente.totalMunicipios()).toBe(2);
    expect(componente.totalPracticas()).toBe(2);
    expect(componente.totalTerritoriosSonoros()).toBe(2);
    expect(componente.totalOrganizaciones()).toBe(1);
  });

  it('el universo contra el que se compara sale de la consulta, no del resumen', () => {
    // El resumen dice 4; la respuesta paginada dice 40. Manda la respuesta, que
    // es la que llenó la lista que el visitante tiene delante.
    const { componente } = montar(FESTIVALES, 40);

    expect(componente.universo()).toBe(40);
    expect(componente.consultaTruncada()).toBeTrue();
  });

  it('sin truncamiento no se avisa de nada', () => {
    const { componente } = montar();

    expect(componente.consultaTruncada()).toBeFalse();
  });

  it('cuenta cuántas fichas publican algún enlace propio', () => {
    // SE LLAMABA «canal de contacto» Y CONTABA CORREOS. El 30 de agosto de 2026 la lectura
    // publica dejo de servir correo, telefono y director —decision dla direccion de producto—, asi
    // que lo unico que queda de contacto en el cable son los cuatro enlaces del Festival. El
    // contador mide eso, y por eso el fixture 1 lleva sitio web y el 3 lleva Instagram.
    const { componente } = montar();

    // 1 publica sitio web y 3 publica Instagram; 2 y 4 no publican ninguno.
    expect(componente.totalConEnlaces()).toBe(2);

    componente.alternarSoloConEnlaces();

    expect(componente.resultados().map(f => f.id)).toEqual(['1', '3']);
  });

  // --- Desplegables ------------------------------------------------------------

  it('los desplegables solo ofrecen valores que existen en la consulta', () => {
    const { componente } = montar();

    expect(componente.departamentos()).toEqual(['ANTIOQUIA', 'ATLÁNTICO', 'META']);
    expect(componente.coberturas()).toEqual(['departamental', 'municipal']);
    expect(componente.organizaciones()).toEqual(['Plan Nacional de Música para la Convivencia', 'Pruebas']);
  });

  it('«anual» y «Anual» son una sola opción de periodicidad', () => {
    // Los dos están en la base. Ofrecerlos por separado partiría en dos un
    // filtro que el visitante lee como uno.
    const { componente } = montar();

    expect(componente.periodicidades()).toEqual(['anual']);

    componente.actualizarPeriodicidad('anual');

    expect(componente.resultados().map(f => f.id)).toEqual(['1', '2']);
  });

  it('el municipio se limita al departamento elegido', () => {
    const { componente } = montar();

    expect(componente.municipios()).toEqual(['ABEJORRAL', 'ABRIAQUÍ', 'ACACÍAS', 'BARANOA']);

    componente.actualizarDepartamento('ATLÁNTICO');

    expect(componente.municipios()).toEqual(['BARANOA']);
  });

  it('cambiar de departamento suelta un municipio que ya no le pertenece', () => {
    // Sin esto queda el par imposible ANTIOQUIA + BARANOA, que devuelve cero
    // sin decir por qué.
    const { componente } = montar();

    componente.actualizarMunicipio('BARANOA');
    componente.actualizarDepartamento('ANTIOQUIA');

    expect(componente.municipio()).toBe('');
    expect(componente.totalResultados()).toBe(2);
  });

  it('un municipio que sobrevive al cambio de departamento no se borra', () => {
    const { componente } = montar();

    componente.actualizarMunicipio('BARANOA');
    componente.actualizarDepartamento('ATLÁNTICO');

    expect(componente.municipio()).toBe('BARANOA');
  });

  // --- Buscador ----------------------------------------------------------------

  it('el buscador ignora las tildes en los dos sentidos', () => {
    const { componente } = montar();

    componente.actualizarBusqueda('abriaqui');
    expect(componente.resultados().map(f => f.id)).toEqual(['2']);

    componente.actualizarBusqueda('ACACÍAS');
    expect(componente.resultados().map(f => f.id)).toEqual(['4']);
  });

  it('el buscador mira también la descripción', () => {
    const { componente } = montar();

    componente.actualizarBusqueda('oriente antioqueño');

    expect(componente.resultados().map(f => f.id)).toEqual(['1']);
  });

  // --- Filtros combinados ------------------------------------------------------

  it('los filtros se acumulan y «Limpiar filtros» los suelta todos', () => {
    const { componente } = montar();

    componente.actualizarDepartamento('ANTIOQUIA');
    componente.actualizarPractica('Bandas de marcha');
    componente.actualizarBusqueda('festival');

    expect(componente.filtrosActivos()).toBe(3);
    expect(componente.resultados().map(f => f.id)).toEqual(['1']);

    componente.limpiarFiltros();

    expect(componente.filtrosActivos()).toBe(0);
    expect(componente.hayFiltros()).toBeFalse();
    expect(componente.totalResultados()).toBe(4);
  });

  it('filtra por territorio sonoro', () => {
    const { componente } = montar();

    componente.actualizarTerritorioSonoro('Chirimía');

    expect(componente.resultados().map(f => f.id)).toEqual(['1', '4']);
  });

  it('filtra por nivel de cobertura', () => {
    const { componente } = montar();

    componente.actualizarCobertura('departamental');

    expect(componente.resultados().map(f => f.id)).toEqual(['3']);
  });

  // --- Orden -------------------------------------------------------------------

  it('ordena por nombre, por territorio y por organización', () => {
    // Fixture propio: con el general los cuatro criterios devolvían el mismo
    // orden, y una prueba que no puede distinguirlos no vigila nada.
    const { componente } = montar([
      festival({
        id: 'a',
        nombre: 'Zafra',
        organizacionResponsable: 'Beta',
        territorioPrincipal: { departamento: 'ANTIOQUIA', municipio: 'BELLO', nivelCobertura: 'municipal' },
      }),
      festival({
        id: 'b',
        nombre: 'Bambuco',
        organizacionResponsable: 'Alfa',
        territorioPrincipal: { departamento: 'META', municipio: 'ACACÍAS', nivelCobertura: 'municipal' },
      }),
      festival({
        id: 'c',
        nombre: 'Mono Núñez',
        organizacionResponsable: 'Gamma',
        territorioPrincipal: { departamento: 'ANTIOQUIA', municipio: 'AMAGÁ', nivelCobertura: 'municipal' },
      }),
    ]);

    expect(componente.resultados().map(f => f.id)).toEqual(['b', 'c', 'a']);

    componente.actualizarOrden('nombre-desc');
    expect(componente.resultados().map(f => f.id)).toEqual(['a', 'c', 'b']);

    componente.actualizarOrden('territorio');
    expect(componente.resultados().map(f => f.id)).toEqual(['c', 'a', 'b']);

    componente.actualizarOrden('organizacion');
    expect(componente.resultados().map(f => f.id)).toEqual(['b', 'a', 'c']);
  });

  it('un criterio de orden desconocido cae en el de fábrica', () => {
    const { componente } = montar();

    componente.actualizarOrden('por-simpatía');

    expect(componente.orden()).toBe('nombre-asc');
  });

  // --- Paginacion --------------------------------------------------------------

  const muchos = (cantidad: number): FestivalPublico[] =>
    Array.from({ length: cantidad }, (_, indice) =>
      festival({
        id: `x${indice}`,
        nombre: `Festival ${String(indice).padStart(2, '0')}`,
        territorioPrincipal: { departamento: 'META', municipio: `MUN ${indice}`, nivelCobertura: 'municipal' },
      }),
    );

  it('nunca dice un total y pinta menos sin ofrecer el resto', () => {
    const { componente } = montar(muchos(15));

    expect(componente.totalResultados()).toBe(15);
    expect(componente.totalPaginas()).toBe(2);
    expect(componente.mostrados().length).toBe(TAMANO_PAGINA_FESTIVALES);
    expect(componente.rangoMostrado()).toEqual({ desde: 1, hasta: TAMANO_PAGINA_FESTIVALES });
    expect(componente.hayPaginaSiguiente()).toBeTrue();
    expect(componente.hayPaginaAnterior()).toBeFalse();

    componente.paginaSiguiente();

    expect(componente.paginaActual()).toBe(2);
    expect(componente.mostrados().length).toBe(15 - TAMANO_PAGINA_FESTIVALES);
    expect(componente.rangoMostrado()).toEqual({ desde: TAMANO_PAGINA_FESTIVALES + 1, hasta: 15 });
    expect(componente.hayPaginaSiguiente()).toBeFalse();
    expect(componente.hayPaginaAnterior()).toBeTrue();
  });

  it('filtrar reinicia la paginación', () => {
    const { componente } = montar(muchos(30));

    componente.irAPagina(2);
    expect(componente.paginaActual()).toBe(2);

    componente.actualizarBusqueda('Festival');

    expect(componente.paginaActual()).toBe(1);
  });

  it('no navega a una página fuera de rango', () => {
    const { componente } = montar(muchos(15));

    componente.irAPagina(0);
    expect(componente.paginaActual()).toBe(1);

    componente.irAPagina(99);
    expect(componente.paginaActual()).toBe(1);
  });

  /**
   * UNA PÁGINA QUE DEJA DE EXISTIR NO SE QUEDA MIRANDO AL VACÍO.
   *
   * Filtrar desde la página 2 ya reinicia a la 1 (`reiniciarPaginacion`), pero `paginaActual()`
   * tiene que defenderse igual: es lo que impide que una página guardada en la URL -compartida,
   * o de una consulta anterior con más resultados- deje la pantalla en blanco si el conteo bajó.
   */
  it('paginaActual() se ajusta si la página pedida ya no existe', () => {
    const { componente } = montar(muchos(15));

    componente.pagina.set(5);

    expect(componente.paginaActual()).toBe(componente.totalPaginas());
    expect(componente.mostrados().length).toBeGreaterThan(0);
  });

  // --- Estado en la URL ---------------------------------------------------------

  /**
   * COMPARTIR, RECARGAR Y VOLVER ATRÁS TIENEN QUE LLEVAR AL MISMO LUGAR.
   *
   * Lo resuelve `enlazarFiltrosConLaUrl`, la pieza que comparten los cuatro listados públicos
   * desde el Bloque 10: lee de `queryParamMap` al montar y vuelve a escribir en cada cambio. Se
   * prueban las dos direcciones por separado: que lo que hay en la URL llega al componente, y que
   * lo que cambia en el componente llega a la URL.
   *
   * <b>HACE FALTA `detectChanges` DESPUES DE LA ACCION.</b> La escritura la hace un `effect`, que
   * se vuelca en la detección de cambios. En la aplicación eso ocurre siempre; en una prueba que
   * llama al método a pelo hay que provocarlo, o se comprueba el estado de antes.
   */
  it('arranca con el estado que trae la URL', () => {
    const { componente } = montar(FESTIVALES, FESTIVALES.length, {
      q: 'bambuco', departamento: 'ANTIOQUIA', orden: 'territorio', vista: 'cuadricula', pagina: '2',
    });

    expect(componente.busqueda()).toBe('bambuco');
    expect(componente.departamento()).toBe('ANTIOQUIA');
    expect(componente.orden()).toBe('territorio');
    expect(componente.vista()).toBe('cuadricula');
    expect(componente.pagina()).toBe(2);
  });

  it('un orden que no existe en la URL cae en el de fábrica, no en un error', () => {
    const { componente } = montar(FESTIVALES, FESTIVALES.length, { orden: 'por-simpatía' });

    expect(componente.orden()).toBe('nombre-asc');
  });

  it('cambiar de página escribe la página en la URL, sin apilar historial', () => {
    const { componente, navegar, fixture } = montar(muchos(30));

    componente.irAPagina(2);
    fixture.detectChanges();

    // COMO TEXTO Y NO COMO NUMERO: un parámetro de URL es texto, y la pieza compartida lo escribe
    // así en vez de fiarlo a que el Router lo convierta. La URL resultante es la misma, `?pagina=2`.
    expect(navegar).toHaveBeenCalledWith([], jasmine.objectContaining({
      queryParams: jasmine.objectContaining({ pagina: '2' }),
      replaceUrl: true,
    }));
  });

  it('la página 1 no ensucia la URL con «pagina=1»', () => {
    const { componente, navegar, fixture } = montar(muchos(30));

    componente.actualizarBusqueda('Festival');
    fixture.detectChanges();

    expect(navegar).toHaveBeenCalledWith([], jasmine.objectContaining({
      queryParams: jasmine.objectContaining({ q: 'Festival', pagina: null }),
    }));
  });

  // --- Enlaces publicados ------------------------------------------------------

  it('solo devuelve los enlaces que la organización publicó', () => {
    const { componente } = montar();

    expect(componente.enlaces(FESTIVALES[0]).map(e => e.clave)).toEqual(['sitioWeb']);
    expect(componente.enlaces(FESTIVALES[1])).toEqual([]);
  });

  it('un enlace sin esquema se sirve por https y uno con esquema raro se descarta', () => {
    const { componente } = montar();

    expect(componente.enlaceExterno('festival.example.co')).toBe('https://festival.example.co');
    expect(componente.enlaceExterno('http://festival.example.co')).toBe('http://festival.example.co');

    // El campo lo escribe una organización externa desde su ficha. Un
    // `javascript:` aquí acabaría en un `href` del sitio público.
    expect(componente.enlaceExterno('javascript:alert(1)')).toBe('');
    expect(componente.enlaceExterno('  ')).toBe('');
    expect(componente.enlaceExterno(null)).toBe('');
  });

  // --- Formas de lectura -------------------------------------------------------

  it('el listado y la cuadrícula pintan la misma consulta con distinta forma', () => {
    const { fixture, componente } = montar();
    const filas = () => fixture.nativeElement.querySelectorAll('[data-festival-fila]').length;
    const tarjetas = () => fixture.nativeElement.querySelectorAll('[data-festival-tarjeta]').length;

    expect(componente.vista()).toBe('listado');
    expect(filas()).toBe(4);
    expect(tarjetas()).toBe(0);

    componente.cambiarVista('cuadricula');
    fixture.detectChanges();

    expect(filas()).toBe(0);
    expect(tarjetas()).toBe(4);
  });

  it('cambiar de forma de lectura no toca los filtros', () => {
    const { componente } = montar();

    componente.actualizarDepartamento('ANTIOQUIA');
    componente.cambiarVista('cuadricula');

    expect(componente.departamento()).toBe('ANTIOQUIA');
    expect(componente.totalResultados()).toBe(2);
  });

  /**
   * §8 DEL PLAN: «clic en Territorio sonoro → acción contextual → listado filtrado».
   *
   * <b>LO QUE ESTAS PRUEBAS IMPIDEN.</b> Dos cosas que convivían en la misma pantalla. Las
   * prácticas y los territorios se pintaban unidos en una cadena muerta —«Bandas de marcha ·
   * Chirimía»— en la vista de listado, mientras en la vista de cuadrícula eran enlaces a
   * `/ecosistema/festivales?practica=…`, que navegaban a esta misma página y de paso BORRABAN los
   * demás filtros puestos. La misma etiqueta, dos comportamientos, y ninguno de los dos era el que
   * pedía el plan.
   */
  describe('las etiquetas de una tarjeta acotan el listado', () => {
    it('pulsar una práctica filtra por ella, y volver a pulsarla lo deshace', () => {
      const { componente } = montar();

      componente.alternarFiltroDeEtiqueta('practica', 'Bandas de marcha');
      expect(componente.practica()).toBe('Bandas de marcha');
      expect(componente.totalResultados()).toBe(2);

      // ALTERNA, NO SOLO PONE: quien llegó pulsando una etiqueta no tiene por qué saber que existe
      // un desplegable arriba para deshacerlo.
      componente.alternarFiltroDeEtiqueta('practica', 'Bandas de marcha');
      expect(componente.practica()).toBe('');
      expect(componente.totalResultados()).toBe(4);
    });

    it('conserva los demás filtros en vez de reemplazarlos', () => {
      const { componente } = montar();

      componente.actualizarDepartamento('ANTIOQUIA');
      componente.alternarFiltroDeEtiqueta('territorio', 'Chirimía');

      // MUTANTE QUE MATA: navegar a `/ecosistema/festivales?territorio=…` como hacía la cuadrícula.
      // El departamento se perdía sin que nada se lo dijera a quien estaba filtrando.
      expect(componente.departamento()).toBe('ANTIOQUIA');
      expect(componente.territorioSonoro()).toBe('Chirimía');
      expect(componente.totalResultados()).toBe(1);
    });

    it('cambiar el departamento desde una etiqueta suelta el municipio que ya no aplica', () => {
      const { componente } = montar();

      componente.alternarFiltroDeEtiqueta('departamento', 'ANTIOQUIA');
      componente.alternarFiltroDeEtiqueta('municipio', 'ABEJORRAL');
      expect(componente.totalResultados()).toBe(1);

      // ABEJORRAL NO EXISTE EN META: dejarlo puesto daría cero resultados sin explicar por qué.
      // La regla ya vive en `actualizarDepartamento`; la etiqueta pasa por ahí en vez de repetirla.
      componente.alternarFiltroDeEtiqueta('departamento', 'META');
      expect(componente.departamento()).toBe('META');
      expect(componente.municipio()).toBe('');
      expect(componente.totalResultados()).toBe(1);
    });

    it('el rótulo accesible dice qué va a pasar, y cambia cuando el filtro ya está puesto', () => {
      const { componente } = montar();

      expect(componente.rotuloDeEtiqueta('practica', 'Bandas de marcha'))
        .toBe('Filtrar por la práctica musical Bandas de marcha');

      componente.alternarFiltroDeEtiqueta('practica', 'Bandas de marcha');

      expect(componente.filtroActivo('practica', 'Bandas de marcha')).toBeTrue();
      expect(componente.rotuloDeEtiqueta('practica', 'Bandas de marcha'))
        .toBe('Quitar el filtro por la práctica musical Bandas de marcha');
    });

    it('las etiquetas son botones con aria-pressed en las DOS vistas, no enlaces en una y texto en otra', () => {
      const { fixture, componente } = montar();
      const etiquetas = () => Array.from(
        fixture.nativeElement.querySelectorAll('[data-etiqueta-filtro]') as NodeListOf<HTMLElement>);

      const enListado = etiquetas();
      expect(enListado.length).toBeGreaterThan(0);
      expect(enListado.every(e => e.tagName === 'BUTTON')).toBeTrue();
      expect(enListado.every(e => e.hasAttribute('aria-pressed'))).toBeTrue();

      componente.cambiarVista('cuadricula');
      fixture.detectChanges();

      const enCuadricula = etiquetas();
      expect(enCuadricula.length).toBeGreaterThan(0);
      expect(enCuadricula.every(e => e.tagName === 'BUTTON')).toBeTrue();
      expect(enCuadricula.every(e => e.hasAttribute('aria-pressed'))).toBeTrue();
    });

    it('lo que no tiene filtro en la URL no se vuelve accionable', () => {
      const { fixture } = montar();
      const textos = Array.from(
        fixture.nativeElement.querySelectorAll('[data-etiqueta-filtro]') as NodeListOf<HTMLElement>)
        .map(e => (e.textContent || '').trim());

      // EL §8 AVISA: «no convertir indiscriminadamente todas las etiquetas en enlaces». La
      // organización responsable no tiene listado público al que llevar, y la periodicidad y el
      // nivel de cobertura no viajan en la URL: un enlace llegaría a la lista sin filtrar.
      expect(textos).not.toContain('Plan Nacional de Música para la Convivencia');
      expect(textos).not.toContain('anual');
      expect(textos).not.toContain('municipal');
    });
  });
});
