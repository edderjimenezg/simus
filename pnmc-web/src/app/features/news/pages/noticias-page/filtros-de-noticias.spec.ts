import { TestBed, ComponentFixture } from '@angular/core/testing';
import { of } from 'rxjs';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { NoticiasPageComponent } from './noticias-page.component';
import { NoticiasService } from '../../../../core/services/noticias.service';
import { TextosWebService } from '../../../../core/services/textos-web.service';
import { BoletinService } from '../../../../core/services/boletin.service';

/**
 * Los filtros de Noticias, y la categoría que los pone desde una tarjeta.
 *
 * <b>LO QUE ESTAS PRUEBAS IMPIDEN.</b> Dos cosas que el §10 nombra.
 *
 * <ul>
 *   <li>Que los filtros vuelvan a vivir solo en señales. Antes, filtrar por «Convocatorias» y
 *       pasarle el enlace a alguien le abría la lista entera; recargar lo perdía; y volver de una
 *       noticia al listado también —siendo que la noticia abierta SÍ era enlazable desde hacía
 *       cortes, porque vive en la ruta—.</li>
 *   <li>Que la categoría de una tarjeta vuelva a ser texto muerto habiendo un filtro por categoría
 *       tres párrafos más arriba.</li>
 * </ul>
 */
describe('las Noticias se filtran y la consulta se puede compartir', () => {
  let fixture: ComponentFixture<NoticiasPageComponent>;
  let componente: NoticiasPageComponent;

  const noticia = (id: number, category: string) => ({
    id, title: `Noticia ${id}`, desc: 'Resumen', content: '<p>Cuerpo</p>',
    category, date: `2026-09-0${id}`, img: '', slug: `noticia-${id}`,
  });

  // Las tres primeras alimentan el destacado y el par secundario; el listado empieza en la cuarta.
  const NOTICIAS = [
    noticia(1, 'Convocatorias'), noticia(2, 'Convocatorias'), noticia(3, 'Formación'),
    noticia(4, 'Convocatorias'), noticia(5, 'Formación'), noticia(6, 'Convocatorias'),
  ];

  function montar(urlDeEntrada: Record<string, string> = {}) {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [NoticiasPageComponent],
      providers: [
        provideRouter([]), provideHttpClient(), provideHttpClientTesting(),
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap(urlDeEntrada) }, paramMap: of(convertToParamMap({})) } },
        { provide: NoticiasService, useValue: { listarPublicas: () => Promise.resolve({ ok: true, data: { items: [] } }) } },
        { provide: TextosWebService, useValue: { getWebText: () => '' } },
        { provide: BoletinService, useValue: { suscribir: () => of({}) } },
      ],
    });
    const router = TestBed.inject(Router);
    const navegar = spyOn(router, 'navigate').and.returnValue(Promise.resolve(true));
    fixture = TestBed.createComponent(NoticiasPageComponent);
    componente = fixture.componentInstance;
    componente.newsData.set(NOTICIAS as never);
    fixture.detectChanges();
    return { navegar };
  }

  it('arranca con el estado que trae la URL', () => {
    montar({ q: 'bambuco', categoria: 'Convocatorias', orden: 'oldest', vista: 'list', pagina: '2' });

    expect(componente.newsSearchTerm()).toBe('bambuco');
    expect(componente.newsCategoryFilter()).toBe('Convocatorias');
    expect(componente.newsSortOrder()).toBe('oldest');
    expect(componente.viewLayout()).toBe('list');
    expect(componente.currentPage()).toBe(2);
  });

  it('sin parámetros conserva los valores con los que abre la pantalla', () => {
    // MUTANTE QUE MATA: poner el «vacío» cuando la URL no trae el parámetro. El orden arranca en
    // «newest», que no es su vacío: machacarlo cambiaría lo que se ve al entrar.
    montar();

    expect(componente.newsSortOrder()).toBe('newest');
    expect(componente.newsCategoryFilter()).toBe('all');
    expect(componente.viewLayout()).toBe('grid');
  });

  it('lo que se filtra se escribe en la URL, y lo vacío no', () => {
    const { navegar } = montar();

    componente.newsCategoryFilter.set('Convocatorias');
    fixture.detectChanges();

    const extras = navegar.calls.mostRecent().args[1] as { queryParams: Record<string, string | null>; replaceUrl: boolean };
    expect(extras.queryParams['categoria']).toBe('Convocatorias');
    expect(extras.queryParams['q']).toBeNull();
    expect(extras.queryParams['pagina']).toBeNull();
    // Sin apilar una entrada de historial por cada tecla escrita en el buscador.
    expect(extras.replaceUrl).toBeTrue();
  });

  it('la categoría de una tarjeta acota el listado y no abre la noticia', () => {
    montar();
    const evento = new MouseEvent('click');
    spyOn(evento, 'stopPropagation');

    componente.filtrarPorCategoria('Convocatorias', evento);

    // MUTANTE QUE MATA: no detener la propagación. La tarjeta entera es pulsable y abre la
    // noticia: sin esto, pulsar la categoría filtraría Y abriría la noticia a la vez.
    expect(evento.stopPropagation).toHaveBeenCalled();
    expect(componente.newsCategoryFilter()).toBe('Convocatorias');
    expect(componente.filteredListNews().every(item => item.category === 'Convocatorias')).toBeTrue();
  });

  it('pulsar la categoría que ya filtra la quita', () => {
    montar();
    componente.filtrarPorCategoria('Convocatorias');
    expect(componente.categoriaActiva('Convocatorias')).toBeTrue();

    componente.filtrarPorCategoria('Convocatorias');

    // Quien llegó pulsando una etiqueta no tiene por qué saber que existe un desplegable arriba
    // para deshacerlo.
    expect(componente.newsCategoryFilter()).toBe('all');
    expect(componente.categoriaActiva('Convocatorias')).toBeFalse();
  });

  it('el rótulo accesible dice qué va a pasar y cambia según el estado', () => {
    montar();
    expect(componente.rotuloDeCategoria('Convocatorias')).toBe('Ver solo las noticias de Convocatorias');

    componente.filtrarPorCategoria('Convocatorias');

    expect(componente.rotuloDeCategoria('Convocatorias')).toBe('Quitar el filtro por la categoría Convocatorias');
  });

  it('limpiar los filtros los devuelve a su vacío, la página incluida', () => {
    montar({ q: 'bambuco', categoria: 'Convocatorias', pagina: '2' });

    componente.resetAllFilters();

    expect(componente.newsSearchTerm()).toBe('');
    expect(componente.newsCategoryFilter()).toBe('all');
    expect(componente.newsSortOrder()).toBe('newest');
    // MUTANTE QUE MATA: dejar la página donde estaba. Quitar los filtros en la página 2 deja la
    // lista en blanco si la consulta sin filtrar no llega a dos páginas.
    expect(componente.currentPage()).toBe(1);
    expect(componente.filtrosPuestos()).toBe(0);
  });

  it('una categoría que solo existe en una destacada no se ofrece como filtro', () => {
    montar();

    // EL LISTADO EMPIEZA EN LA CUARTA NOTICIA: las tres primeras son el destacado y el par
    // secundario. «Formación» sí está en el listado —la 5—, así que se puede filtrar por ella.
    expect(componente.categoriaFiltrable('Formación')).toBeTrue();
    expect(componente.categoriaFiltrable('Convocatorias')).toBeTrue();

    // Pero una categoría que SOLO viva en una destacada daría cero resultados siempre.
    componente.newsData.set([
      { ...noticia(1, 'Solo en destacada'), id: 1 },
      noticia(2, 'Formación'), noticia(3, 'Formación'),
      noticia(4, 'Formación'),
    ] as never);
    fixture.detectChanges();

    // MUTANTE QUE MATA: ofrecer la etiqueta siempre. Se vio en navegador: pulsar «Convocatorias»
    // en la tarjeta destacada dejaba la pantalla en «ninguna noticia coincide con tus filtros».
    expect(componente.categoriaFiltrable('Solo en destacada')).toBeFalse();
    expect(componente.categoriaFiltrable('Formación')).toBeTrue();
  });
});
