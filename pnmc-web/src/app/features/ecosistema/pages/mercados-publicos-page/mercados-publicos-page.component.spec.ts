import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';

import { ApiClientService } from '../../../../core/http/api-client.service';
import { MercadoPublico } from '../../../../core/services/mercados-publicos.service';
import { NavigationService } from '../../../../core/services/navigation.service';
import { PrevisualizacionEnListadoService } from '../../../../core/services/previsualizacion-en-listado.service';
import { MercadosPublicosPageComponent } from './mercados-publicos-page.component';

/**
 * El directorio público de Mercados Musicales.
 *
 * <b>LO QUE ESTAS PRUEBAS IMPIDEN.</b> Que vuelva a ser una tabla de tres columnas con un buscador
 * suelto. El directorio de Festivales tiene columna de filtros, cifras que responden a la consulta,
 * conmutador de vista y paginación, y el criterio pide el de Mercados igual. Lo que se
 * pinza es lo que de verdad distingue un directorio de una lista: que los filtros ACOTEN, que las
 * cifras hablen de lo filtrado y no del total, que solo se ofrezcan territorios que existen, y que
 * un enlace de una ficha llegue con su filtro puesto.
 */
describe('MercadosPublicosPageComponent', () => {
  let fixture: ComponentFixture<MercadosPublicosPageComponent>;
  let componente: MercadosPublicosPageComponent;

  function unMercado(parcial: Partial<MercadoPublico> & { id: number; nombre: string }): MercadoPublico {
    return {
      descripcion: null, alcance: null, modalidad: null, periodicidad: null, periodicidadDetalle: null,
      sitioWebMercado: null, instagramMercado: null, facebookMercado: null, otroEnlaceMercado: null,
      nivelCobertura: 'municipal', codigoDepartamento: null, nombreDepartamento: null,
      codigoMunicipio: null, nombreMunicipio: null, lugarEspecifico: null,
      seRealizaEnElMarcoDeUnFestival: false, festivalId: null, festivalNombre: null,
      organizacionNombre: null, numeroDeEdiciones: 0, fechaPublicacion: null,
      practicasMusicales: [], territoriosSonoros: [],
      ...parcial,
    };
  }

  const MERCADOS: MercadoPublico[] = [
    unMercado({
      id: 1, nombre: 'Mercado del Pacífico', nombreDepartamento: 'Valle del Cauca', nombreMunicipio: 'Cali',
      numeroDeEdiciones: 4, practicasMusicales: [{ id: 3, nombre: 'Marimba de chonta' }],
      territoriosSonoros: [{ id: 7, nombre: 'Pacífico sur' }],
      sitioWebMercado: 'mercado.example.com',
    }),
    unMercado({
      id: 2, nombre: 'Mercado del Amazonas', nombreDepartamento: 'Amazonas', nombreMunicipio: 'Leticia',
      numeroDeEdiciones: 1, practicasMusicales: [{ id: 9, nombre: 'Músicas de pueblos originarios' }],
    }),
    unMercado({ id: 3, nombre: 'Encuentro nacional', nivelCobertura: 'nacional', numeroDeEdiciones: 2 }),
  ];

  function montar(parametros: Record<string, string> = {}): void {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [MercadosPublicosPageComponent],
      providers: [
        provideRouter([]),
        { provide: ApiClientService, useValue: { get: () => of({ items: MERCADOS }) } },
        { provide: NavigationService, useValue: { navigateToMapLayer: () => undefined } },
        // LA PREVISUALIZACION DEVUELVE LA LISTA TAL CUAL: su papel —insertar el registro que la
        // consola está previsualizando— tiene sus propias pruebas y aquí solo estorbaría.
        { provide: PrevisualizacionEnListadoService, useValue: { conListado: (_m: string, lista: MercadoPublico[]) => of(lista) } },
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap(parametros) } } },
      ],
    });
    fixture = TestBed.createComponent(MercadosPublicosPageComponent);
    componente = fixture.componentInstance;
    fixture.detectChanges();
  }

  it('las cifras hablan de la consulta, no del total publicado', () => {
    // MUTANTE QUE MATA: contar siempre sobre todo lo publicado. Una franja que dice «3 resultados»
    // mientras la lista enseña uno convierte la cifra en decoración.
    montar();
    const antes = componente.metricas();
    expect(antes[0].value).toBe(3);
    expect(antes[0].detail).toBe('de 3 publicados');

    componente.actualizarDepartamento('Amazonas');
    const despues = componente.metricas();
    expect(despues[0].value).toBe(1);
    // Y EL DETALLE SIGUE DICIENDO EL TOTAL: es lo que permite saber cuánto se está dejando fuera.
    expect(despues[0].detail).toBe('de 3 publicados');
    expect(componente.filtrados().map(m => m.nombre)).toEqual(['Mercado del Amazonas']);
  });

  it('solo se ofrecen los territorios y las prácticas que existen', () => {
    // OFRECER LOS 33 DEPARTAMENTOS sobre un directorio con tres mercados hace que casi cada
    // elección devuelva una lista vacía, y quien elige no sabe si se equivocó o si no hay nada.
    montar();

    expect(componente.departamentos()).toEqual(['Amazonas', 'Valle del Cauca']);
    expect(componente.practicas()).toEqual(['Marimba de chonta', 'Músicas de pueblos originarios']);
    // SIN DEPARTAMENTO NO HAY MUNICIPIOS: una lista de todos los municipios del país no dice dónde
    // buscar y obliga a recorrerla entera.
    expect(componente.municipios()).toEqual([]);
    componente.actualizarDepartamento('Valle del Cauca');
    expect(componente.municipios()).toEqual(['Cali']);
  });

  it('cambiar de departamento limpia el municipio que había', () => {
    // MUTANTE QUE MATA: conservarlo. Un municipio de Cali filtrando sobre Amazonas no devuelve
    // nada, y quien lo ve no sabe que el filtro anterior sigue puesto.
    montar();
    componente.actualizarDepartamento('Valle del Cauca');
    componente.actualizarMunicipio('Cali');
    expect(componente.filtrados().length).toBe(1);

    componente.actualizarDepartamento('Amazonas');

    expect(componente.municipio()).toBe('');
    expect(componente.filtrados().map(m => m.nombre)).toEqual(['Mercado del Amazonas']);
  });

  it('los filtros llegan desde la URL: es lo que hace que la ficha sea una puerta', () => {
    // La ficha de un mercado enlaza su territorio y sus prácticas. Sin leer los parámetros, ese
    // enlace abriría el directorio entero y quien lo pinchó tendría que elegir a mano.
    montar({ departamento: 'Amazonas' });

    expect(componente.departamento()).toBe('Amazonas');
    expect(componente.filtrados().map(m => m.nombre)).toEqual(['Mercado del Amazonas']);
  });

  it('el orden actúa sobre todos los registros, no sobre lo que se está pintando', () => {
    montar();

    componente.actualizarOrden('ediciones');
    expect(componente.filtrados().map(m => m.numeroDeEdiciones)).toEqual([4, 2, 1]);

    componente.actualizarOrden('nombre');
    expect(componente.filtrados().map(m => m.nombre)).toEqual(['Encuentro nacional', 'Mercado del Amazonas', 'Mercado del Pacífico']);
  });

  it('limpiar devuelve el directorio entero y apaga el contador de filtros', () => {
    montar();
    componente.actualizarBusqueda('Pacífico');
    componente.actualizarDepartamento('Valle del Cauca');
    expect(componente.hayFiltros()).toBeTrue();

    componente.limpiarFiltros();

    expect(componente.hayFiltros()).toBeFalse();
    expect(componente.filtrosActivos()).toBe(0);
    expect(componente.filtrados().length).toBe(3);
  });

  it('un enlace que no lleva a ningún sitio no se pinta', () => {
    // SOLO LOS QUE DE VERDAD NAVEGAN: un icono que no hace nada es peor que no tener el icono. Y el
    // saneado lo hace la pieza compartida, que descarta cualquier esquema que no sea http/https.
    montar();

    expect(componente.enlaces(MERCADOS[0]).map(e => e.href)).toEqual(['https://mercado.example.com']);
    expect(componente.enlaces(MERCADOS[1])).toEqual([]);
  });

  it('una consulta sin resultados dice cuántos hay y ofrece deshacerla', () => {
    // MUTANTE QUE MATA: un «no hay resultados» a secas. Quien busca no sabe si el directorio está
    // vacío o si es su consulta la que no encuentra nada.
    montar();
    componente.actualizarBusqueda('zzzzz');
    fixture.detectChanges();

    const vacio = (fixture.nativeElement as HTMLElement).querySelector('[data-sin-resultados]');
    expect(vacio).not.toBeNull();
    expect(vacio!.textContent).toContain('3 mercados publicados');
    expect(vacio!.textContent).toContain('menos filtros');
  });
});
