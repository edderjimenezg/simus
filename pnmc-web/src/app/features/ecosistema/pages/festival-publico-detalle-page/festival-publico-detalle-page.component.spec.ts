import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { EdicionFestivalPublica, FestivalPublico, FestivalesPublicosService } from '../../../../core/services/festivales-publicos.service';
import { FestivalPublicoDetallePageComponent } from './festival-publico-detalle-page.component';

describe('FestivalPublicoDetallePageComponent · ediciones anuales públicas', () => {
  const festival: FestivalPublico = {
    id: '107', nombre: 'Festival de prueba', descripcion: 'Descripción pública.', organizacionResponsable: 'Organización de prueba',
    territorioPrincipal: { departamento: 'Cesar', municipio: 'Valledupar', nivelCobertura: 'municipal' }, periodicidad: 'anual',
    practicasMusicales: [], territoriosSonoros: [], instagram: null, facebook: null, sitioWeb: null, otroEnlace: null,
  };
  const edicion: EdicionFestivalPublica = {
    id: 20, anio: 2026, numeroEdicion: null, nombre: 'Edición 2026', descripcion: 'La edición publicada.', fechaInicio: '2026-04-26', fechaFin: '2026-04-30',
    estado: 'programada', estadoEtiqueta: 'Programada',
  };

  function montar(ediciones: EdicionFestivalPublica[] | 'error' = [edicion]) {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [FestivalPublicoDetallePageComponent], providers: [provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: new Map([['festivalId', '107']]) } } },
        { provide: FestivalesPublicosService, useValue: { consultarFestival: () => of(festival), consultarEdiciones: () => ediciones === 'error' ? throwError(() => new Error('falló')) : of(ediciones) } },
      ],
    });
    const fixture = TestBed.createComponent(FestivalPublicoDetallePageComponent); fixture.detectChanges();
    return { fixture, componente: fixture.componentInstance, texto: () => fixture.nativeElement.textContent as string };
  }

  it('presenta la Edición, no una versión del perfil ni datos personales', () => {
    const { componente, texto } = montar();
    expect(componente.edicionMasReciente()?.anio).toBe(2026);
    expect(texto()).toContain('Edición 2026');
    expect(texto()).not.toContain('Director o directora');
    expect(texto()).not.toContain('Vigente');
  });

  it('declara el estado vacío y no inventa datos de ediciones', () => {
    const { texto, componente } = montar([]);
    expect(componente.edicionesCargadas()).toBeTrue();
    expect(texto()).toContain('Sin ediciones publicadas');
  });

  it('mantiene la ficha del Festival si la lectura de ediciones falla', () => {
    const { texto, componente } = montar('error');
    expect(componente.festival()?.nombre).toBe('Festival de prueba');
    expect(componente.edicionesError()).toBeTruthy();
    expect(texto()).toContain('Festival de prueba');
  });

  it('formatea fechas sin desplazarlas por zona horaria', () => {
    expect(montar().componente.rangoDeFechas(edicion)).toBe('26 de abril de 2026 — 30 de abril de 2026');
  });

  /**
   * §8: el territorio de la ficha lleva al listado acotado, y lo que no puede llevar a ninguna
   * parte se queda como texto.
   */
  describe('el territorio de la ficha explora el listado', () => {
    it('departamento y municipio abren el listado ya filtrado', () => {
      const { fixture } = montar();
      const enlaces = Array.from(
        fixture.nativeElement.querySelectorAll('[data-explorar-territorio]') as NodeListOf<HTMLAnchorElement>);

      expect(enlaces.map(e => e.textContent!.trim())).toEqual(['Cesar', 'Valledupar']);
      expect(enlaces[0].getAttribute('href')).toContain('departamento=Cesar');
      expect(enlaces[1].getAttribute('href')).toContain('municipio=Valledupar');
      // «Cesar» a secas no dice qué va a pasar al pulsarlo.
      expect(enlaces[0].getAttribute('aria-label')).toBe('Ver los Festivales de Cesar');
    });

    it('la organización, la periodicidad y el nivel de cobertura NO se vuelven enlaces', () => {
      const { fixture, componente } = montar();
      const textos = Array.from(
        fixture.nativeElement.querySelectorAll('[data-explorar-territorio]') as NodeListOf<HTMLElement>)
        .map(e => (e.textContent || '').trim());

      // MUTANTE QUE MATA: marcar los cinco datos con `filtro`. La organización no tiene listado
      // público al que llevar, y la periodicidad y la cobertura no viajan en la URL del listado:
      // el enlace aterrizaría en la lista sin filtrar y parecería roto.
      expect(textos).not.toContain('Organización de prueba');
      expect(textos).not.toContain('anual');
      expect(textos).not.toContain('municipal');

      const conFiltro = componente.datosGenerales(festival).filter(d => d.filtro);
      expect(conFiltro.map(d => d.etiqueta)).toEqual(['Departamento', 'Municipio']);
    });

    it('un territorio sin dato no se vuelve un enlace vacío', () => {
      const { componente } = montar();
      const sinTerritorio = { ...festival, territorioPrincipal: { departamento: null, municipio: null, nivelCobertura: null } };
      const datos = componente.datosGenerales(sinTerritorio as never);

      // El dato sigue declarando su filtro, pero la plantilla no lo enlaza porque no hay valor:
      // un enlace a `?departamento=Sin información` es peor que no tener enlace.
      expect(datos.find(d => d.etiqueta === 'Departamento')?.valor).toBe(componente.sinDato);
      expect(componente.exploracionDe(datos.find(d => d.etiqueta === 'Periodicidad')!)).toEqual({});
    });
  });
});
