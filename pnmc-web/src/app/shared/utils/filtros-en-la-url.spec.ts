import { Component, signal } from '@angular/core';
import { TestBed, ComponentFixture } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import {
  contarFiltrosPuestos,
  enlazarFiltrosConLaUrl,
  filtroDePagina,
  filtroDeTexto,
  filtroDeVista,
  limpiarFiltros,
} from './filtros-en-la-url';

/**
 * Los filtros de un listado, guardados en la URL.
 *
 * <b>LO QUE ESTAS PRUEBAS IMPIDEN.</b> Que vuelva a haber cuatro implementaciones de lo mismo, y
 * que las tres consecuencias que se notan usando el sitio reaparezcan: que una búsqueda filtrada no
 * se pueda compartir, que recargar la pierda, y que «Atrás» no la deshaga.
 */
@Component({ selector: 'app-listado-de-prueba', standalone: true, template: '' })
class ListadoDePrueba {
  readonly busqueda = signal('');
  readonly categoria = signal('todas');
  readonly pagina = signal(1);
  readonly vista = signal<'mosaico' | 'tabla'>('mosaico');

  readonly filtros = [
    filtroDeTexto('q', this.busqueda),
    filtroDeTexto('categoria', this.categoria, 'todas'),
    filtroDeVista('vista', this.vista, 'mosaico'),
    filtroDePagina(this.pagina),
  ];

  constructor() {
    enlazarFiltrosConLaUrl(this.filtros);
  }
}

describe('los filtros de un listado viven en la URL', () => {
  let fixture: ComponentFixture<ListadoDePrueba>;
  let componente: ListadoDePrueba;
  let navegaciones: { queryParams: Record<string, string | null> }[];

  function montar(urlDeEntrada: Record<string, string> = {}): void {
    navegaciones = [];
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [ListadoDePrueba],
      providers: [
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap(urlDeEntrada) } } },
      ],
    });
    const router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.callFake((_comandos: unknown[], extras?: { queryParams?: Record<string, string | null> }) => {
      navegaciones.push({ queryParams: extras?.queryParams ?? {} });
      return Promise.resolve(true);
    });
    fixture = TestBed.createComponent(ListadoDePrueba);
    componente = fixture.componentInstance;
    fixture.detectChanges();
  }

  const ultima = () => navegaciones[navegaciones.length - 1].queryParams;

  it('lee de la URL al entrar: es lo que sostiene recargar y abrir un enlace compartido', () => {
    montar({ q: 'bambuco', categoria: 'Convocatorias', pagina: '3', vista: 'tabla' });

    expect(componente.busqueda()).toBe('bambuco');
    expect(componente.categoria()).toBe('Convocatorias');
    expect(componente.pagina()).toBe(3);
    expect(componente.vista()).toBe('tabla');
  });

  it('un parámetro ausente no machaca el valor con el que arranca la pantalla', () => {
    // MUTANTE QUE MATA: poner `vacio` cuando la URL no trae el parámetro. El orden de Noticias
    // arranca en «newest», que no es su valor vacío: machacarlo cambiaría lo que se ve al entrar.
    montar({ q: 'bambuco' });

    expect(componente.categoria()).toBe('todas');
    expect(componente.vista()).toBe('mosaico');
    expect(componente.pagina()).toBe(1);
  });

  it('escribe en la URL lo que se filtra, y solo eso', () => {
    montar();
    componente.categoria.set('Convocatorias');
    fixture.detectChanges();

    expect(ultima()['categoria']).toBe('Convocatorias');
    // LO VACIO NO SE ESCRIBE: una URL con `?q=&pagina=1` no aporta nada y estorba al compartirla.
    expect(ultima()['q']).toBeNull();
    expect(ultima()['pagina']).toBeNull();
    expect(ultima()['vista']).toBeNull();
  });

  it('no apila una entrada de historial por cada tecla', () => {
    montar();
    componente.busqueda.set('ba');
    fixture.detectChanges();

    // «Atrás» tiene que volver a la pantalla anterior, no deshacer letra a letra lo escrito.
    const extras = navegaciones.length;
    expect(extras).toBeGreaterThan(0);
    const llamada = (TestBed.inject(Router).navigate as jasmine.Spy).calls.mostRecent();
    expect(llamada.args[1].replaceUrl).toBeTrue();
    expect(llamada.args[1].queryParamsHandling).toBe('merge');
  });

  it('una página que no es un entero positivo cae en la primera, no deja la lista en blanco', () => {
    // `?pagina=abc` llega de un enlace mal copiado, no de un ataque.
    montar({ pagina: 'abc' });
    expect(componente.pagina()).toBe(1);

    montar({ pagina: '-2' });
    expect(componente.pagina()).toBe(1);

    montar({ pagina: '2.5' });
    expect(componente.pagina()).toBe(1);
  });

  it('cuenta los filtros puestos con el vacío que cada uno declara', () => {
    montar();
    expect(contarFiltrosPuestos(componente.filtros)).toBe(0);

    componente.categoria.set('Convocatorias');
    expect(contarFiltrosPuestos(componente.filtros)).toBe(1);

    // MUTANTE QUE MATA: comparar contra `''` en vez de contra el vacío declarado. «todas» es el
    // vacío de esta categoría, y contarlo daría «1 filtro» sobre una lista sin filtrar.
    componente.categoria.set('todas');
    componente.busqueda.set('bambuco');
    expect(contarFiltrosPuestos(componente.filtros)).toBe(1);
  });

  it('limpiar devuelve cada filtro a SU vacío, la página incluida', () => {
    montar({ q: 'bambuco', categoria: 'Convocatorias', pagina: '4' });

    limpiarFiltros(componente.filtros);

    expect(componente.busqueda()).toBe('');
    // MUTANTE QUE MATA: dejar la página donde estaba. Quitar los filtros en la página 4 deja la
    // lista en blanco si la consulta sin filtrar no llega a cuatro páginas.
    expect(componente.pagina()).toBe(1);
    expect(componente.categoria()).toBe('todas');
    expect(contarFiltrosPuestos(componente.filtros)).toBe(0);
  });
});
