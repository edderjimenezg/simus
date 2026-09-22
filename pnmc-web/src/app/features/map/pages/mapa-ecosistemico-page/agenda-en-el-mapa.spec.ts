import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { MapDataService } from '../../../../core/services/map-data.service';
import { MapaEcosistemicoPageComponent } from './mapa-ecosistemico-page.component';

/**
 * La Agenda en el mapa: contexto del territorio, no una capa.
 *
 * <b>LO QUE ESTAS PRUEBAS IMPIDEN.</b> El mapa anunciaba seis capas y cinco marcaban cero: cuatro
 * de ellas son procesos cuyas tablas retiró `V20260904_01`. La Agenda sí tiene territorio y lectura
 * pública, y nunca se había llevado allí.
 *
 * <b>PERO NO COMO CAPA.</b> se corrigió: «tampoco
 * es necesario plantear un botón de agenda como el de festivales; la agenda es algo adicional que
 * aparece en la barra derecha y no un filtro». Y la distinción es justa: un Festival es un proceso
 * que se filtra y se dibuja; un evento es lo que ESTA PASANDO en el territorio que se acaba de
 * abrir. Por eso vive en la columna de lectura y se apaga cuando la pregunta deja de ser
 * territorial.
 */
describe('la Agenda en el mapa', () => {
  const departmentFeature = (code: string, name: string) => ({
    type: 'Feature',
    properties: { departmentCode: code, departmentName: name },
    geometry: { type: 'Polygon', coordinates: [[[0, 0], [1, 0], [1, 1], [0, 0]]] },
  });

  const evento = (id: string, name: string, deptName: string, municipio: string, organizador: string) => ({
    id,
    fields: {
      name, dpt: deptName, departamento: deptName, municipio,
      desc: 'Un evento de prueba', organizador, sitio_web: '',
      coverageLevel: 'municipal', fecha: '2026-11-20', modalidad: 'presencial',
      lugar: 'Casa de la Cultura', categoria: 'Conciertos',
      'Prácticas musicales': '', 'Territorios sonoros': '',
    },
  });

  const festival = (id: string, name: string, deptName: string, organizador: string) => ({
    id,
    fields: {
      name, dpt: deptName, departamento: deptName, municipio: 'Medellin',
      desc: 'Un festival de prueba', organizador, sitio_web: '',
      coverageLevel: 'municipal',
      'Prácticas musicales': '', 'Territorios sonoros': '',
    },
  });

  const bundle = {
    geoJson: {
      type: 'FeatureCollection',
      features: [departmentFeature('05', 'ANTIOQUIA'), departmentFeature('08', 'ATLANTICO')],
      municipalities: { type: 'FeatureCollection', features: [] },
    },
    baseCounts: {},
    festivalRecords: [festival('f1', 'Festival del Río', 'Antioquia', 'Fundación Cauce')],
    agendaRecords: [
      evento('e1', 'Concierto de bandas', 'Antioquia', 'Medellin', 'Fundación Cauce'),
      evento('e2', 'Taller de percusión', 'Antioquia', 'Envigado', 'Fundación Cauce'),
      evento('e3', 'Encuentro de gaitas', 'Atlantico', 'Barranquilla', 'Otra Fundación'),
    ],
    schoolRecords: [], marketRecords: [], redesRecords: [], luthierRecords: [],
    festivalCounts: {}, agendaCounts: {}, schoolCounts: {}, marketCounts: {},
  };

  let component: MapaEcosistemicoPageComponent;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [MapaEcosistemicoPageComponent],
      providers: [
        provideRouter([]),
        { provide: MapDataService, useValue: { fetchMapCountsBundle: () => of(bundle) } },
      ],
    });
    /*
     * SIN `detectChanges()`: se mira el modelo de la página, no Leaflet.
     *
     * Es la convención que ya siguen las otras cuatro pruebas de este mapa, y ahora se sabe por qué
     * importa: renderizar monta Leaflet de verdad, que deja escuchas sobre `document` y `window`.
     * Esas escuchas siguen vivas cuando corre la suite SIGUIENTE y disparan contra nodos
     * desmontados —«Cannot read properties of undefined (reading '_leaflet_pos')»— en pruebas que
     * no tienen nada que ver con el mapa. Costó dos corridas atribuirlo, porque la prueba que caía
     * iba cambiando.
     *
     * `fixture.destroy()` NO basta: el fallo aparecía igual. Lo que no hay que hacer es montarlo.
     */
    component = TestBed.createComponent(MapaEcosistemicoPageComponent).componentInstance;
    component.fetchMapData();
  });

  it('cuenta los eventos por departamento, como cuenta los Festivales', () => {
    const porDepartamento = component.agendaPorDepartamento();

    expect(porDepartamento['ANTIOQUIA']?.length).toBe(2);
    expect(porDepartamento['ATLANTICO']?.length).toBe(1);
    expect(component.totalDeEventosDelAmbito()).toBe(3);
  });

  it('la Agenda NO es una capa: no tiene botón entre los procesos', () => {
    // MUTANTE QUE MATA: devolverla a `capasConConteo`. Sería un filtro —«enséñame solo eventos»—
    // cuando lo que es, es contexto del territorio abierto.
    //
    // ESTA PRUEBA ESPERABA UNA SOLA CAPA, y tenía razón hasta: ese día
    // Mercados Musicales volvió con su modelo, su circuito de revisión y su ruta pública, que era
    // la condición escrita para encenderla. Lo que se fija sigue siendo lo mismo: que la Agenda no
    // esté entre ellas.
    const capas = component.capasConConteo().map(c => c.layerKey);

    expect(capas).toEqual(['Festivales', 'Mercados Musicales']);
    expect(capas).not.toContain('Agenda');
  });

  it('las capas que siguen retiradas se anuncian aparte y sin cifra', () => {
    // MUTANTE QUE MATA: devolverlas a `capasConConteo`. Con un 0 al lado de las que sí tienen datos
    // son indistinguibles de un territorio sin registros; sus tablas no existen.
    //
    // ERAN CUATRO Y AHORA SON TRES. Mercados salió de aquí, y es la
    // primera de las cuatro que se muda a la lista de arriba: volvió con su modelo, su revisión y
    // su ruta pública, que es exactamente la condición que este anuncio ponía.
    const enPreparacion = component.capasEnPreparacion.map(c => c.label);

    expect(enPreparacion).toEqual(['Escuelas', 'Redes Doc.', 'Lutieres']);
    expect(enPreparacion).not.toContain('Mercados');
    expect(component.capasConConteo().map(c => c.label)).not.toContain('Escuelas');
  });

  it('con un lente puesto la Agenda NO desaparece: cae en «Sin clasificar»', () => {
    expect(component.totalDeEventosDelAmbito()).toBe(3);

    component.lenteDeLectura.set('practicas');

    // ESTE ERA EL PRECIO DE TRATARLO COMO FILTRO: la pestaña entera había que apagarla, porque un
    // evento no se clasifica por práctica musical. Con un lente no hace falta: se sigue viendo, y
    // lo que no consta se dice que no consta.
    //
    // MUTANTE QUE MATA: volver a vaciar `agendaPorDepartamento` con el lente puesto.
    expect(component.totalDeEventosDelAmbito()).toBe(3);
    expect(component.agendaDisponible()).toBeTrue();
  });

  it('los eventos NO entran al directorio: el directorio lista procesos', () => {
    // MUTANTE QUE MATA: volver a mezclarlos. «6 registros» contaría cosas de dos naturalezas
    // distintas —lo que está registrado en un territorio y lo que ocurre en una fecha—.
    expect(component.fichasDelDirectorio().filter(f => f.type === 'Evento').length).toBe(0);
    expect(component.fichasDelDirectorio().filter(f => f.type === 'Festival').length).toBe(1);
  });

  it('los eventos del ámbito se ordenan por cercanía a hoy, no por fecha bruta', () => {
    const eventos = component.eventosDelAmbito();

    expect(eventos.length).toBe(3);
    // MUTANTE QUE MATA: ordenar ascendente. Pondría arriba el evento más antiguo de la base, que
    // es lo menos útil de una agenda.
    const hoy = Date.now();
    const distancias = eventos.map(e => Math.abs(Date.parse(e.fecha) - hoy));
    expect(distancias).toEqual([...distancias].sort((a, b) => a - b));
  });

  it('el lente agrupa lo que hay en vez de restarlo, y nombra lo que no consta', () => {
    component.lenteDeLectura.set('territorios-sonoros');
    const grupos = component.gruposDelLente();

    // Los Festivales del juego de datos no declaran territorio sonoro, así que caen todos ahí.
    // MUTANTE QUE MATA: devolver `[]` para lo no clasificado. Un registro sin clasificar
    // desaparecía del mapa al filtrar, indistinguible de uno que no existe.
    expect(grupos.map(g => g.nombre)).toContain('Sin clasificar');

    // «SIN CLASIFICAR» VA AL FINAL aunque sea el mayor: es la ausencia de dato, no un grupo más.
    expect(grupos[grupos.length - 1].nombre).toBe('Sin clasificar');
  });

  it('resaltar un grupo atenúa los demás, no los esconde', () => {
    component.lenteDeLectura.set('territorios-sonoros');
    const antes = component.fichasDelDirectorio().length;

    component.alternarGrupoResaltado('Sin clasificar');

    // MUTANTE QUE MATA: filtrar en vez de resaltar. Es la diferencia entre ponerse unas gafas y
    // taparse un ojo: con el filtro se pierde de vista todo lo demás.
    expect(component.fichasDelDirectorio().length).toBe(antes);
    expect(component.grupoResaltado()).toBe('Sin clasificar');

    component.alternarGrupoResaltado('Sin clasificar');
    expect(component.grupoResaltado()).toBe('');
  });

  it('la organización se lee DESDE los procesos Y los eventos, sin consultar su ficha', () => {
    component.abrirOrganizacion('Fundación Cauce');
    const org = component.resumenDeLaOrganizacion();

    expect(org?.nombre).toBe('Fundación Cauce');
    // UN FESTIVAL Y DOS EVENTOS, contados sobre lo que el mapa ya tiene cargado. No hay lectura
    // pública de organizaciones y este bloque no estrena ninguna.
    //
    // MUTANTE QUE MATA: leer solo `fichasDelDirectorio`. Desde que los eventos salieron de él, eso
    // daría cero eventos justo al pulsar la organización DESDE un evento.
    expect(org?.festivales).toBe(1);
    expect(org?.eventos).toBe(2);
    expect(org?.procesos.length).toBe(3);
  });

  it('no mezcla los procesos de dos organizaciones distintas', () => {
    component.abrirOrganizacion('Otra Fundación');
    const org = component.resumenDeLaOrganizacion();

    expect(org?.eventos).toBe(1);
    expect(org?.festivales).toBe(0);
    expect(org?.procesos[0].nombre).toBe('Encuentro de gaitas');
  });

  it('sin organización abierta no hay nada que enseñar', () => {
    expect(component.resumenDeLaOrganizacion()).toBeNull();

    component.abrirOrganizacion('   ');

    // Un nombre en blanco no abre un panel vacío: no abre nada.
    expect(component.resumenDeLaOrganizacion()).toBeNull();
  });

  it('el municipio solo ofrece los que tienen registros, no los 1.122 de DIVIPOLA', () => {
    // Sin departamento abierto no hay municipios que ofrecer: el desplegable ni se pinta.
    expect(component.municipiosConRegistros()).toEqual([]);

    component.departamentoElegido.set('ANTIOQUIA');

    // MUTANTE QUE MATA: listar todos los municipios del departamento. Un desplegable con
    // novecientas entradas que dan cero resultados no es un filtro, es un catálogo.
    expect(component.municipiosConRegistros()).toEqual(['Medellin']);
  });

  it('elegir municipio acota lo que se lista y lo que se dibuja', () => {
    component.departamentoElegido.set('ANTIOQUIA');
    expect(component.fichasDelDirectorio().length).toBe(1);

    component.actualizarMunicipio('Envigado');

    // `fichasDelDirectorio` es la fuente de la columna derecha Y de los marcadores: acotar aquí acota
    // las dos cosas, que es lo que hace que el mapa y la lista digan lo mismo.
    expect(component.fichasDelDirectorio().length).toBe(0);
    expect(component.filtrosActivos()).toContain('Municipio');
  });

  it('dice cuántos quedan de cuántos hay, no solo cuántos quedan', () => {
    expect(component.resultadoDeLaConsulta())
      .toEqual({ visibles: 1, total: 1, acotado: false, sinTerritorio: 0 });

    component.departamentoElegido.set('ATLANTICO');

    // MUTANTE QUE MATA: enseñar solo `visibles`. «0» no distingue «no hay» de «los filtros los
    // dejaron fuera», y esa es la confusión que un geovisor tiene que evitar por encima de todo.
    const resultado = component.resultadoDeLaConsulta();
    expect(resultado.total).toBe(1);
    expect(resultado.acotado).toBeTrue();
  });

  it('los tres modos de dibujo se pueden elegir, y el nombre de cada uno es el cartográfico', () => {
    // LOS NOMBRES SON LOS DE LA DISCIPLINA y no los internos. «Zonas» y «Puntos» describían el
    // trazo; «Coropletas» y «Símbolos proporcionales» nombran dos técnicas con una lectura y un
    // sesgo conocidos, y la descripción de cada botón dice cuál es.
    expect(component.modosDeDibujo.map(m => m.etiqueta))
      .toEqual(['Coropletas', 'Símbolos proporcionales', 'Mapa de calor']);

    // MUTANTE QUE MATA: dejar el calor sin elegir. Estuvo anunciado y sin pulsar mientras DIVIPOLA
    // no traía coordenadas; con el catálogo situado, los tres dibujan.
    for (const modo of component.modosDeDibujo) {
      component.elegirModoDeDibujo(modo.id);
      expect(component.modoDeDibujo()).toBe(modo.id);
    }
  });

  it('la leyenda del calor cuenta procesos contra un techo fijo, no contra el máximo observado', () => {
    const escalones = component.escalonesDelCalor();

    // LOS ESCALONES SE DERIVAN DEL TECHO, no son cinco fijos. Con el techo en cuatro son cuatro:
    // cinco bandas de enteros no caben en un recorrido de cuatro enteros, y forzarlas producía
    // rótulos invertidos —«3 a 2 procesos cerca»— y una banda repetida.
    expect(escalones.length).toBe(4);
    expect(escalones[0].desde).toBe(1);

    // MUTANTE QUE MATA: volver a cinco pasos fijos. Ninguna banda puede terminar antes de donde
    // empieza, ni repetir el primer valor de la anterior.
    for (const [indice, escalon] of escalones.entries()) {
      if (indice > 0) expect(escalon.desde).toBeGreaterThan(escalones[indice - 1].desde);
    }

    // MUTANTE QUE MATA: normalizar por el máximo observado, que es lo que hace `leaflet.heat`. El
    // último escalón tiene que decir «o más» sobre un techo que no se mueve al tocar un filtro: si
    // se moviera, dos capturas de la misma pantalla dejarían de ser comparables.
    expect(escalones[escalones.length - 1].etiqueta).toContain('o más');

    component.departamentoElegido.set('ATLANTICO');
    expect(component.escalonesDelCalor().map(e => e.etiqueta)).toEqual(escalones.map(e => e.etiqueta));
  });

  it('sin haber pintado todavía, el calor no inventa un alcance en kilómetros', () => {
    // MUTANTE QUE MATA: rotular «0 km» o un valor por omisión. El alcance lo mide la capa sobre la
    // vista real; antes del primer pintado no hay cifra que decir, y decir una sería inventarla.
    expect(component.alcanceDelCalor()).toBe('');

    component.radioDelCalorEnMetros.set(12_400);
    expect(component.alcanceDelCalor()).toBe('12 km');
  });

  it('la agenda acota por fecha, y si el tramo queda vacío enseña todos', () => {
    expect(component.rangoDeAgenda()).toBe('proximos');

    // Los tres eventos del juego de datos son de 2026-11; «este mes» no los alcanza.
    component.rangoDeAgenda.set('mes');

    // MUTANTE QUE MATA: devolver la lista vacía. Septiembre lo dejó escrito: «un panel vacío se lee
    // como una agenda rota». Se enseñan todos y el botón sigue marcando lo que se pidió.
    expect(component.eventosDelAmbito().length).toBe(3);
    expect(component.rangoDeAgenda()).toBe('mes');
  });

  it('limpiar devuelve también el municipio y el tramo de la agenda', () => {
    component.departamentoElegido.set('ANTIOQUIA');
    component.actualizarMunicipio('Medellin');
    component.rangoDeAgenda.set('todos');

    component.limpiarFiltros();

    // MUTANTE QUE MATA: olvidarse de uno. Es el defecto que este método ya tuvo con la vista: lo
    // que `filtrosActivos` sabe contar, `limpiarFiltros` tiene que saber quitarlo.
    expect(component.municipioElegido()).toBe('Todos');
    expect(component.rangoDeAgenda()).toBe('proximos');
    expect(component.filtrosActivos()).toEqual([]);
  });
});

/**
 * La Agenda se apaga cuando la pregunta deja de ser territorial.
 *
 * <b>ESTABA PEDIDO Y NO ESTABA HECHO.</b> `agendaDisponible` devolvía `true` fijo: se escribió
 * cuando la clasificación eran dos desplegables que filtraban, y la condición que iba a leer
 * desapareció al convertirlos en lente. Nadie la tradujo, así que la pestaña seguía encendida en el
 * único caso en que la dirección de producto había pedido apagarla: «al filtrar por territorio sonoro
 * o práctica no tiene sentido tener los eventos».
 */
describe('la Agenda se apaga cuando la pregunta deja de ser territorial', () => {
  const departmentFeature = (code: string, name: string) => ({
    type: 'Feature',
    properties: { departmentCode: code, departmentName: name },
    geometry: { type: 'Polygon', coordinates: [[[0, 0], [1, 0], [1, 1], [0, 0]]] },
  });

  const bundle = {
    geoJson: {
      type: 'FeatureCollection',
      features: [departmentFeature('05', 'ANTIOQUIA')],
      municipalities: { type: 'FeatureCollection', features: [] },
    },
    baseCounts: {},
    festivalRecords: [], agendaRecords: [],
    schoolRecords: [], marketRecords: [], redesRecords: [], luthierRecords: [],
    festivalCounts: {}, agendaCounts: {}, schoolCounts: {}, marketCounts: {},
  };

  let component: MapaEcosistemicoPageComponent;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [MapaEcosistemicoPageComponent],
      providers: [
        provideRouter([]),
        { provide: MapDataService, useValue: { fetchMapCountsBundle: () => of(bundle) } },
      ],
    });
    component = TestBed.createComponent(MapaEcosistemicoPageComponent).componentInstance;
    component.fetchMapData();
  });

  it('ponerse el lente NO la apaga: reagrupar no esconde nada', () => {
    component.lenteDeLectura.set('territorios-sonoros');

    // MUTANTE QUE MATA: apagarla con el lente puesto. Ponerse las gafas no filtra —se sigue viendo
    // todo, reagrupado—, así que la agenda del territorio sigue siendo una respuesta válida.
    expect(component.agendaDisponible()).toBeTrue();
  });

  it('resaltar un grupo sí la apaga: los eventos no se clasifican por práctica', () => {
    component.lenteDeLectura.set('practicas');
    component.alternarGrupoResaltado('Bambuco');

    expect(component.agendaDisponible()).toBeFalse();
  });

  it('al apagarse no deja la pestaña abierta y sin salida', () => {
    component.lenteDeLectura.set('practicas');
    component.pestanaDeLectura.set('agenda');

    component.alternarGrupoResaltado('Bambuco');

    // MUTANTE QUE MATA: deshabilitar la pestaña sin moverse. El panel derecho se quedaba enseñando
    // una lista que la propia pantalla acababa de declarar improcedente, y sin un control encendido
    // con el que salir de ahí.
    expect(component.pestanaDeLectura()).toBe('directorio');
  });

  it('quitar el resalte la vuelve a encender', () => {
    component.lenteDeLectura.set('practicas');
    component.alternarGrupoResaltado('Bambuco');
    component.alternarGrupoResaltado('Bambuco');

    expect(component.agendaDisponible()).toBeTrue();
  });
});
