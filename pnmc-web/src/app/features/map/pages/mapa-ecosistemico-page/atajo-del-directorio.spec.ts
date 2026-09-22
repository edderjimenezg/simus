import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { CatalogService } from '../../../../core/services/catalog.service';
import { MapDataService } from '../../../../core/services/map-data.service';
import { MapaEcosistemicoPageComponent } from './mapa-ecosistemico-page.component';

/**
 * EL DIRECTORIO COMO ATAJO DEL MAPA (29 de agosto de 2026).
 *
 * Cada tarjeta del panel derecho ejecutaba `fichaEnDetalle.set(item)`, que abre
 * un modal `fixed inset-0` con velo encima del mapa. Medido en el navegador a 1280x639
 * antes del cambio: pulsar la primera tarjeta abría un diálogo de 1280x639 —«Detalle de
 * Redes de Documentación»— y el mapa quedaba entero detrás.
 *
 * El criterio es este: «que se vaya al mapa y se ubique en el municipio, y se abra la
 * pestaña que se usa dentro del mapa para ubicar».
 *
 * QUÉ SE MIDE AQUÍ Y QUÉ NO. Aquí se mide lo que decide el componente: qué señales
 * quedan puestas al pulsar, y quién las borra. Que la cámara viaje de verdad, que el
 * rótulo del municipio aparezca y que el modal no vuelva son cosas de navegador y las
 * vigila `npm run mapa:atajo`; ninguna prueba de este fichero se pondría en rojo si la
 * plantilla volviera a llamar al modal.
 *
 * EL JUEGO DE DATOS. Cinco redes en Antioquia (una sin municipio) y dos lutieres en
 * Bolívar, que es el mismo de `tablero-del-geovisor.spec.ts`. Todos los registros con
 * municipio traen `divipola: '05001'` y `municipio: 'Medellin'`.
 */
describe('El directorio del panel es un atajo del mapa', () => {
  const departmentFeature = (code: string, name: string) => ({
    type: 'Feature',
    properties: { departmentCode: code, departmentName: name },
    geometry: { type: 'Polygon', coordinates: [[[0, 0], [1, 0], [1, 1], [0, 0]]] },
  });

  const rawRecord = (id: string, name: string, deptCode: string, deptName: string, extra: Record<string, unknown> = {}) => ({
    id,
    fields: {
      name,
      departamento: deptName,
      deptCode,
      municipio: 'Medellin',
      divipola: '05001',
      descripcion: 'Registro de prueba',
      ...extra,
    },
  });

  const bundle = {
    geoJson: {
      type: 'FeatureCollection',
      features: [
        departmentFeature('05', 'ANTIOQUIA'),
        departmentFeature('13', 'BOLIVAR'),
        departmentFeature('76', 'VALLE DEL CAUCA'),
      ],
      municipalities: { type: 'FeatureCollection', features: [] },
    },
    baseCounts: {},
    festivalRecords: [],
    schoolRecords: [],
    marketRecords: [],
    redesRecords: [
      rawRecord('r1', 'Centro Uno', '05', 'Antioquia', { centerType: 'Archivo' }),
      rawRecord('r2', 'Centro Dos', '05', 'Antioquia', { centerType: 'Archivo' }),
      // Sin municipio y sin código: es el único registro que no se puede situar.
      rawRecord('r5', 'Centro Sin Sede', '05', 'Antioquia', { centerType: 'Fonoteca', municipio: '', divipola: '' }),
    ],
    luthierRecords: [
      rawRecord('l1', 'Taller Uno', '13', 'Bolivar', { oficio: 'Cuerdas', municipio: 'Cartagena', divipola: '13001' }),
    ],
    festivalCounts: {},
    schoolCounts: {},
    marketCounts: {},
  };

  let component: MapaEcosistemicoPageComponent;
  let pedidosDePuntos: number;

  /**
   * La ficha del directorio que se llama asi, o un error.
   *
   * REVIENTA SI NO ESTA, Y NO DEVUELVE `undefined`. Con `find` a secas, una prueba que
   * pide un registro que el directorio ya no lista le pasa `undefined` al metodo, este
   * cae en la rama de repuesto y la prueba sigue verde midiendo otra cosa. Paso el 29
   * de agosto de 2026 con `Taller Uno` despues de abrir Antioquia: el directorio queda
   * acotado al departamento abierto y el lutier de Bolivar ya no estaba en la lista.
   */
  const fichaLlamada = (nombre: string) => {
    const ficha = component.fichasFiltradasDelDirectorio().find((f: { name?: string }) => f.name === nombre);
    if (!ficha) throw new Error(`El directorio no lista «${nombre}» ahora mismo`);
    return ficha;
  };

  beforeEach(() => {
    pedidosDePuntos = 0;

    TestBed.configureTestingModule({
      imports: [MapaEcosistemicoPageComponent],
      providers: [
        provideRouter([]),
        { provide: MapDataService, useValue: { fetchMapCountsBundle: () => of(bundle) } },
        {
          provide: CatalogService,
          useValue: {
            fetchPuntosMunicipales: () => {
              pedidosDePuntos += 1;
              return Promise.resolve(new Map());
            },
            cargarDivipolaPorDepartamento: () => of({}),
          },
        },
      ],
    });

    // Sin `detectChanges()`: no hay Leaflet, se mira el modelo de la página.
    component = TestBed.createComponent(MapaEcosistemicoPageComponent).componentInstance;
    component.fetchMapData();
  });

  describe('pulsar un registro lleva a su municipio', () => {
    it('deja el municipio del registro apuntado, con su código a cinco dígitos', () => {
      component.irAlMunicipioDelRegistro(fichaLlamada('Centro Uno')!);

      expect(component.municipioEnfocado()?.codigo).toBe('05001');
      expect(component.municipioEnfocado()?.nombre).toBe('Medellin');
    });

    it('no abre el modal de la ficha, que es justo lo que tapaba el mapa', () => {
      component.irAlMunicipioDelRegistro(fichaLlamada('Centro Uno')!);

      expect(component.fichaEnDetalle()).toBeNull();
    });

    it('devuelve el centro al mapa cuando se estaba mirando la tabla', () => {
      // Desde «Tabla» o «Gráfico» el centro no muestra cartografía: la tarjeta del
      // municipio saldría pegada a un territorio que no está en pantalla.
      component.vistaCentral.set('tabla');

      component.irAlMunicipioDelRegistro(fichaLlamada('Centro Uno')!);

      expect(component.vistaCentral()).toBe('mapa');
    });

    it('abre el departamento del registro, que es lo que dibuja sus municipios', () => {
      // La capa de municipios solo se monta con un departamento abierto
      // (`municipiosDelDepartamento`). Sin esto se viajaría a la coordenada correcta
      // sobre un mapa que no dibuja ni el contorno ni el nombre del municipio.
      component.seleccionarCapa('Lutieres');
      expect(component.departamentoElegido()).toBe('Nacional');

      component.irAlMunicipioDelRegistro(fichaLlamada('Taller Uno')!);

      expect(component.departamentoNormalizado()).toBe('BOLIVAR');
    });

    it('pide la tabla de puntos municipales, sin la cual no hay adónde volar', () => {
      // Esa tabla solo se pedía al entrar al Modo de Prácticas e Influencia. Desde el
      // directorio hay que pedirla también, o el atajo se queda sin coordenada.
      expect(component.estadoPuntosMunicipales()).toBe('sin_pedir');

      component.irAlMunicipioDelRegistro(fichaLlamada('Centro Uno')!);

      expect(pedidosDePuntos).toBe(1);
    });

    it('recuerda de qué registro se vino, para que la tarjeta lo marque', () => {
      // Medellín tiene dos redes en este juego de datos: sin el identificador, la
      // tarjeta lista las dos y no dice cuál se pulsó.
      const ficha = fichaLlamada('Centro Dos');
      component.irAlMunicipioDelRegistro(ficha);

      expect(component.municipioEnfocado()?.registroId).toBe(component.claveDeFicha(ficha));
    });

    it('lleva a municipios distintos según el registro, y no siempre al mismo', () => {
      component.irAlMunicipioDelRegistro(fichaLlamada('Centro Uno')!);
      expect(component.municipioEnfocado()?.codigo).toBe('05001');

      // El primer viaje deja Antioquia abierto y el directorio acotado a el. Para
      // llegar a un registro de Bolivar hay que volver a la vista nacional, que es lo
      // que hacen el boton «Nacional» y «Limpiar».
      component.volverAVistaNacional();
      component.seleccionarCapa('Lutieres');
      component.irAlMunicipioDelRegistro(fichaLlamada('Taller Uno')!);

      expect(component.municipioEnfocado()?.codigo).toBe('13001');
      expect(component.municipioEnfocado()?.nombre).toBe('Cartagena');
    });
  });

  describe('un registro que no se puede situar', () => {
    it('abre la ficha en vez de dejar el clic muerto', () => {
      // Es la única rama de repuesto. Medido contra el API: los 155 registros
      // publicados traen `municipalityCode`, así que hoy no se pisa.
      component.irAlMunicipioDelRegistro(fichaLlamada('Centro Sin Sede')!);

      expect(component.municipioEnfocado()).toBeNull();
      expect(component.fichaEnDetalle()).not.toBeNull();
    });

    it('no mueve el mapa ni pide la tabla de puntos para un viaje que no puede hacer', () => {
      component.vistaCentral.set('tabla');

      component.irAlMunicipioDelRegistro(fichaLlamada('Centro Sin Sede')!);

      expect(component.vistaCentral()).toBe('tabla');
      expect(pedidosDePuntos).toBe(0);
    });
  });

  describe('quién olvida el municipio enfocado', () => {
    it('volver a la vista nacional', () => {
      component.irAlMunicipioDelRegistro(fichaLlamada('Centro Uno')!);
      expect(component.municipioEnfocado()).not.toBeNull();

      component.volverAVistaNacional();

      expect(component.municipioEnfocado()).toBeNull();
    });

    it('entrar a otro departamento desde el mapa', () => {
      // Si no se olvidara, el efecto de cámara devolvería la vista al municipio del
      // clic anterior en cuanto `departamentoElegido` cambiara.
      component.irAlMunicipioDelRegistro(fichaLlamada('Centro Uno')!);

      component.abrirDepartamento('Bolívar');

      expect(component.municipioEnfocado()).toBeNull();
      expect(component.departamentoNormalizado()).toBe('BOLIVAR');
    });

    it('y limpiar los filtros, que pasa por la vuelta a nacional', () => {
      component.irAlMunicipioDelRegistro(fichaLlamada('Centro Uno')!);

      component.limpiarFiltros();

      expect(component.municipioEnfocado()).toBeNull();
    });
  });

  describe('el identificador del registro no puede repetirse entre capas', () => {
    it('la clave lleva la capa, porque el «id» del API se repite', () => {
      // Comprobado contra el API: `/festivals`,
      // `/music-schools`, `/music-markets`, `/redes-documentacion` y `/lutieres`
      // devuelven los tres primeros como `1`, `2` y `3` cada uno. «1» nombra a cinco
      // registros distintos.
      const red = { id: '1', type: 'Redes de Documentación' };
      const escuela = { id: '1', type: 'Escuela' };

      expect(component.claveDeFicha(red)).not.toBe(component.claveDeFicha(escuela));
    });

    it('dos fichas de la misma capa con el mismo id sí comparten clave', () => {
      // La clave identifica al registro, no al objeto: la misma ficha leída dos veces
      // tiene que dar lo mismo, o la marca no encontraría nunca su proceso.
      expect(component.claveDeFicha({ id: '7', type: 'Festival' }))
        .toBe(component.claveDeFicha({ id: '7', type: 'Festival' }));
    });

    it('el atajo guarda la clave con capa, no el id pelado', () => {
      // Sin esto la tarjeta marcaba los CINCO procesos de un municipio con un registro
      // de cada capa, visto en pantalla en ABEJORRAL.
      const ficha = fichaLlamada('Centro Dos');

      component.irAlMunicipioDelRegistro(ficha);

      expect(component.municipioEnfocado()?.registroId).toBe(component.claveDeFicha(ficha));
      expect(component.municipioEnfocado()?.registroId).not.toBe('r2');
    });
  });

  describe('la tarjeta del municipio marca el registro del que se vino', () => {
    it('guarda el resaltado que se le pasa', () => {
      component.abrirMunicipio('Medellin', '05001', undefined, undefined, 'r2');

      expect(component.municipioSeleccionado()?.resaltado).toBe('r2');
    });

    it('no marca nada cuando la tarjeta se abre desde el mapa', () => {
      // El clic sobre el polígono no viene de ningún registro: marcar uno cualquiera
      // sería inventarse de dónde vino el usuario.
      component.abrirMunicipio('Medellin', '05001');

      expect(component.municipioSeleccionado()?.resaltado).toBeUndefined();
    });

    it('lista los procesos del municipio, que es para lo que se abre', () => {
      component.seleccionarCapa('Redes de Documentación');
      component.abrirMunicipio('Medellin', '05001', undefined, undefined, 'r2');

      const fichas = component.municipioSeleccionado()?.fichas || [];
      expect(fichas.length).toBe(2);
      expect(fichas.some((f: { id?: string }) => f.id === 'r2')).toBeTrue();
    });
  });
});
