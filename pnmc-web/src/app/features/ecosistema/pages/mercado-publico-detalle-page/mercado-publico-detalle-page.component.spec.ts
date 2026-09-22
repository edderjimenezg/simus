import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { EdicionDeMercadoPublica, MercadoPublico, MercadosPublicosService } from '../../../../core/services/mercados-publicos.service';
import { MercadoPublicoDetallePageComponent } from './mercado-publico-detalle-page.component';

/**
 * La ficha pública de un mercado: la pareja de la del Festival, con las mismas partes.
 *
 * <b>LO QUE SE PINZA AQUI</b> es lo que distingue una ficha pública de la de la consola: que la
 * relación con el festival se recorra en este sentido también, que las ediciones publicadas se
 * lean, y que un fallo de ediciones no borre la ficha ni afirme que no hay ninguna.
 */
describe('MercadoPublicoDetallePageComponent', () => {
  const mercado: MercadoPublico = {
    id: 12, nombre: 'Mercado del Pacífico', descripcion: 'Rueda de negocios de músicas del Pacífico.',
    alcance: 'Internacional', modalidad: 'Mixta', periodicidad: 'anual', periodicidadDetalle: null,
    sitioWebMercado: 'mercado.example.com', instagramMercado: null, facebookMercado: null, otroEnlaceMercado: null,
    nivelCobertura: 'municipal', codigoDepartamento: '76', nombreDepartamento: 'Valle del Cauca',
    codigoMunicipio: '76001', nombreMunicipio: 'Cali', lugarEspecifico: 'Teatro al aire libre',
    seRealizaEnElMarcoDeUnFestival: true, festivalId: 44, festivalNombre: 'Festival Petronio',
    organizacionNombre: 'Fundación de prueba', numeroDeEdiciones: 1, fechaPublicacion: '2026-09-01T10:00:00Z',
    practicasMusicales: [{ id: 3, nombre: 'Marimba de chonta' }], territoriosSonoros: [{ id: 7, nombre: 'PACÍFICO SUR' }],
  };
  const edicion: EdicionDeMercadoPublica = {
    id: 90, mercadoId: 12, anio: 2026, numeroEdicion: 5, nombre: 'Edición 2026', descripcion: 'La quinta rueda.',
    fechaInicio: '2026-08-14', fechaFin: '2026-08-18', nombreDepartamento: 'Valle del Cauca', nombreMunicipio: 'Cali',
    lugarEspecifico: 'Bulevar del río', estado: 'programada',
  };

  function montar(ediciones: EdicionDeMercadoPublica[] | 'error' = [edicion], ficha: MercadoPublico | null = mercado) {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [MercadoPublicoDetallePageComponent],
      providers: [provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: new Map([['mercadoId', '12']]) } } },
        { provide: MercadosPublicosService, useValue: {
          consultarMercado: () => ficha ? of(ficha) : throwError(() => new Error('no existe')),
          consultarEdiciones: () => ediciones === 'error' ? throwError(() => new Error('falló')) : of(ediciones),
        } },
      ],
    });
    const fixture = TestBed.createComponent(MercadoPublicoDetallePageComponent);
    fixture.detectChanges();
    return { fixture, componente: fixture.componentInstance, texto: () => fixture.nativeElement.textContent as string };
  }

  it('enseña el perfil del mercado con sus prácticas y territorios', () => {
    const { texto, fixture } = montar();
    expect(texto()).toContain('Mercado del Pacífico');
    expect(texto()).toContain('Rueda de negocios');
    expect(fixture.nativeElement.querySelector('[data-practicas-del-mercado]').textContent).toContain('Marimba de chonta');
    // El territorio sonoro llega en mayúsculas del catálogo y se escribe como nombre propio.
    expect(texto()).toContain('Pacífico Sur');
  });

  it('la relación con el festival se recorre también desde aquí', () => {
    // MUTANTE QUE MATA: enseñar el nombre sin enlazar. La relación se puede recorrer en los dos
    // sentidos o es la mitad de una relación: la ficha del festival ya enlaza sus mercados.
    const { fixture } = montar();
    const enlace = fixture.nativeElement.querySelector('[data-festival-del-mercado] a') as HTMLAnchorElement;
    expect(enlace.textContent!.trim()).toBe('Festival Petronio');
    expect(enlace.getAttribute('href')).toContain('/ecosistema/festivales/44');
  });

  it('lista las ediciones publicadas y abre la primera', () => {
    const { componente, texto } = montar();
    expect(componente.edicionAbierta()).toBe(90);
    expect(texto()).toContain('Edición 2026');
    expect(componente.rangoDeFechas(edicion)).toBe('14 de agosto de 2026 — 18 de agosto de 2026');
    expect(componente.estadoDeLaEdicion(edicion)).toBe('Programada');
  });

  it('declara el estado vacío y no inventa ediciones', () => {
    const { texto, componente } = montar([]);
    expect(componente.edicionesCargadas()).toBeTrue();
    expect(texto()).toContain('Sin ediciones publicadas');
  });

  it('mantiene la ficha si la lectura de ediciones falla', () => {
    const { texto, componente } = montar('error');
    expect(componente.mercado()?.nombre).toBe('Mercado del Pacífico');
    expect(componente.edicionesError()).toBeTruthy();
    expect(texto()).toContain('Mercado del Pacífico');
  });

  it('solo enlaza la presencia pública que existe, y nunca datos de contacto', () => {
    const { componente } = montar();
    // El DTO público no trae correo ni teléfono; la ficha tampoco los pide.
    expect(componente.enlacesDelMercado(mercado).map(e => e.clave)).toEqual(['sitioWeb']);
    expect(componente.enlacesDelMercado(mercado)[0].href).toBe('https://mercado.example.com');
  });
});
