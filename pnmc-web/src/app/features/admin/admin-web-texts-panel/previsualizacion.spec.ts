import { ComponentFixture, TestBed } from '@angular/core/testing';
import { WEB_TEXT_GROUPS } from '../../../core/services/textos-web.service';
import {
  AdminWebTextsPanelComponent,
  ALTO_LOGICO,
  ANCHO_LOGICO,
  ENCUADRES,
  calcularEncuadre,
  rutaSegura,
} from './admin-web-texts-panel.component';

/*
  LA PREVISUALIZACION DEL PANEL, que desde carga la pagina
  real en un marco de 1440x810 en vez de dibujarla a mano.

  Lo que estas pruebas NO hacen: mirar dentro del marco. Un <iframe> con la
  aplicacion entera dentro no arranca en Karma —no hay servidor que sirva la ruta—
  y esperarlo convertiria estas pruebas en lentas y caprichosas. Lo que se afirma
  aqui es todo lo que se decide FUERA del marco: la tabla de encuadres, la
  aritmetica del encuadre, la escala, el formato apaisado y el aviso de borrador.
  Que el bloque aparezca de verdad dentro del marco lo cubre la prueba e2e.
*/
describe('AdminWebTextsPanelComponent · previsualización real', () => {
  let fixture: ComponentFixture<AdminWebTextsPanelComponent>;
  let component: AdminWebTextsPanelComponent;

  beforeEach(async () => {
    localStorage.removeItem('pnmc_web_texts');
    await TestBed.configureTestingModule({
      imports: [AdminWebTextsPanelComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(AdminWebTextsPanelComponent);
    component = fixture.componentInstance;
    component._enabled.set(true);
    fixture.detectChanges();
  });

  afterEach(() => localStorage.removeItem('pnmc_web_texts'));

  it('todos los grupos del registro declaran dónde encuadrarse', () => {
    // SIN VALOR DE RESERVA, y es lo que hace que esta prueba sirva. Si
    // `encuadresDelGrupo()` cayera a la pagina de la seccion cuando falta la
    // entrada, un grupo nuevo sin encuadre se veria «bien» —la pagina desde
    // arriba— y nadie se enteraria de que su seccion no se esta mirando.
    const sinEncuadre = WEB_TEXT_GROUPS
      .filter(grupo => !(ENCUADRES[grupo.id]?.length > 0))
      .map(grupo => grupo.id);

    expect(WEB_TEXT_GROUPS.length).toBeGreaterThan(0);
    expect(sinEncuadre).withContext('grupos del catálogo sin encuadre declarado').toEqual([]);

    const rutasVacias = Object.entries(ENCUADRES)
      .filter(([, lista]) => lista.some(e => !e.ruta || !e.ruta.startsWith('/')))
      .map(([id]) => id);
    expect(rutasVacias).toEqual([]);
  });

  it('ningún encuadre apunta a la consola', () => {
    // Un marco que cargue /admin monta un segundo panel dentro del primero, con
    // su propia sesión y sus propias peticiones al servidor.
    const haciaLaConsola = Object.entries(ENCUADRES)
      .filter(([, lista]) => lista.some(e => rutaSegura(e.ruta) === null))
      .map(([id]) => id);

    expect(haciaLaConsola).toEqual([]);
    expect(rutaSegura('/admin')).toBeNull();
    expect(rutaSegura('/admin/contenido')).toBeNull();
    expect(rutaSegura('/pnmc')).toBe('/pnmc');
    expect(rutaSegura('')).toBeNull();
    expect(rutaSegura('https://otro.sitio/pnmc')).toBeNull();
  });

  it('el primer encuadre coincide con la página del botón «Ir a la página»', () => {
    // Dos rutas para lo mismo son dos verdades: si el botón lleva a un sitio y
    // la vista encuadra otro, uno de los dos está mintiendo.
    const discrepancias: string[] = [];
    for (const grupo of WEB_TEXT_GROUPS) {
      component.selectedSection.set(grupo.section);
      component.selectedGroup.set(grupo.id);
      const primero = component.encuadresDelGrupo()[0];
      const boton = component.paginaPublica();
      if (primero && boton && primero.ruta !== boton) {
        discrepancias.push(`${grupo.id}: encuadre ${primero.ruta} ≠ botón ${boton}`);
      }
    }
    expect(discrepancias).toEqual([]);
  });

  it('el marco pide 1440 px de ancho lógico y sale apaisado', () => {
    component.anchoDisponible.set(945);

    expect(ANCHO_LOGICO).toBe(1440);
    expect(component.altoPintado()).toBe(Math.round(ALTO_LOGICO * component.escala()));
    expect(component.altoPintado())
      .withContext('la vista no es apaisada')
      .toBeLessThan(component.anchoPintado());
  });

  it('se reduce hasta el ancho disponible y nunca se amplía por encima de 1:1', () => {
    component.anchoDisponible.set(945);
    expect(component.escala()).toBeCloseTo(945 / 1440, 3);
    expect(component.anchoPintado()).toBe(945);

    // Un hueco mas ancho que la propia pagina no la estira: ampliar 1440 px no
    // ensena nada nuevo y desdibuja el texto.
    component.anchoDisponible.set(2000);
    expect(component.escala()).toBe(1);
    expect(component.anchoPintado()).toBe(1440);
  });

  it('calcularEncuadre centra lo que cabe y deja contexto de lo que no', () => {
    // El bloque cabe: se centra, y lo que sobra arriba y abajo es justamente el
    // trozo de la sección anterior y de la siguiente que se pidió ver.
    expect(calcularEncuadre({ top: 2000, alto: 400 }, 6000)).toBe(2000 - (810 - 400) / 2);

    // El bloque no cabe: no hay forma de ver las dos vecinas, y se prefiere
    // enseñar 120 px de la anterior para que se note dónde empieza.
    expect(calcularEncuadre({ top: 2000, alto: 1200 }, 6000)).toBe(1880);

    // Y no se desplaza por encima del principio ni por debajo del final.
    expect(calcularEncuadre({ top: 0, alto: 200 }, 6000)).toBe(0);
    expect(calcularEncuadre({ top: 5900, alto: 200 }, 6000)).toBe(6000 - 810);

    // UNA PÁGINA MÁS CORTA QUE EL MARCO no se desplaza nada. Este caso lo añadió
    // un mutante que sobrevivía: quitar el `Math.max(0, …)` del tope no rompía
    // ninguna de las cuatro afirmaciones de arriba, porque en todas la página
    // medía 6000 px y el tope salía positivo de todos modos. Con una página de
    // 400 px el tope se vuelve negativo y el marco se desplazaría a un scroll
    // negativo. No es un caso inventado: la página de error de `general_404` y
    // el acceso externo caben de sobra en 810 px.
    expect(calcularEncuadre({ top: 0, alto: 300 }, 400)).toBe(0);
    expect(calcularEncuadre({ top: 120, alto: 300 }, 400)).toBe(0);
  });

  it('dice cuántos campos ha tocado la editora y todavía no se publican', () => {
    component.selectedSection.set('Home');
    component.selectedGroup.set('home_hero');
    fixture.detectChanges();
    expect(component.camposFueraDeLaVista()).toEqual([]);

    component.handleInputChange('home_title', 'Un título distinto');
    fixture.detectChanges();

    expect(component.camposFueraDeLaVista()).toEqual(['home_title']);
    expect(component.estadoDeLaVista()).toBe('1 campo sin publicar');
    expect((fixture.nativeElement as HTMLElement).querySelector('[data-testid="aviso-borrador"]')).not.toBeNull();
  });

  it('cambiar de grupo dentro de la misma página no recarga el marco', () => {
    // Siete grupos comparten /pnmc y ocho /ecosistema. Recargar en cada cambio
    // tira la página entera para volver a montarla igual, y el desplazamiento
    // hasta el bloque nuevo pasa de inmediato a varios segundos.
    component.selectedSection.set('Sobre PNMC');
    component.selectedGroup.set('about_objectives');
    fixture.detectChanges();
    const version = component.versionDeLaVista();

    component.selectedGroup.set('about_actors');
    fixture.detectChanges();
    expect(component.versionDeLaVista()).toBe(version);

    // NI CAMBIAR DE ENCUADRE DENTRO DE LA MISMA PÁGINA. Este caso lo añadió un
    // mutante que sobrevivía: la comprobación de arriba no toca las dependencias
    // del efecto —la ruta no cambia y el encuadre vuelve a 0—, así que una
    // recarga metida dentro del efecto ni se disparaba. Cambiar de encuadre sí
    // las toca, y es además lo que hace la editora al pulsar la segunda pastilla.
    component.selectedGroup.set('about_hero_presentation');
    fixture.detectChanges();
    const antesDeCambiarEncuadre = component.versionDeLaVista();
    component.elegirEncuadre(1);
    fixture.detectChanges();
    expect(component.versionDeLaVista())
      .withContext('cambiar de encuadre recargó la página entera')
      .toBe(antesDeCambiarEncuadre);

    // «Actualizar» sí recarga: es su único trabajo.
    component.recargarLaVista();
    expect(component.versionDeLaVista()).toBe(antesDeCambiarEncuadre + 1);
  });

  it('al cambiar de grupo se vuelve al primer encuadre', () => {
    component.selectedSection.set('Sobre PNMC');
    component.selectedGroup.set('about_hero_presentation');
    fixture.detectChanges();
    component.elegirEncuadre(1);
    expect(component.encuadreElegido()).toBe(1);

    component.selectedGroup.set('about_objectives');
    fixture.detectChanges();

    // El índice 1 de un grupo de dos no significa nada en un grupo de uno: sin
    // esto, `encuadreActivo()` caería al primero y la pastilla marcada sería otra.
    expect(component.encuadreElegido()).toBe(0);
  });

  it('los grupos repartidos en dos sitios ofrecen los dos', () => {
    // Tres grupos del catálogo no caben en un solo encuadre. Se nombran uno a
    // uno: si alguno pierde su segundo encuadre, la mitad de sus claves deja de
    // verse y nada más lo diría.
    const conDos = ['about_hero_presentation', 'general_nav_footer'];
    for (const id of conDos) {
      expect(ENCUADRES[id].length).withContext(`${id} perdió su segundo encuadre`).toBe(2);
    }
    expect(ENCUADRES['eje1_details'].length).toBe(2);
  });

  it('el menú y el pie se encuadran sobre una página que los pinta', () => {
    // ESTE CASO SE FALLÓ PRIMERO Y LO ATRAPÓ LA PRUEBA DE ARRIBA. El razonamiento
    // equivocado era: `showGlobalFooter()` (app.component.ts) devuelve false
    // para la portada, luego en `/` no hay pie, luego hay que encuadrar en otra
    // página. Es falso: la portada pinta su propio `<app-footer>`
    // (home.component.html:227), y esa exclusión existe justamente para que no
    // salgan dos. La ruta correcta es la misma que ya usaba el botón.
    //
    // Se afirma que existe el elemento, y no solo la ruta, porque lo que estuvo
    // mal no fue la ruta sino la creencia sobre lo que esa ruta contiene.
    for (const encuadre of ENCUADRES['general_nav_footer']) {
      expect(encuadre.ruta).toBe('/');
    }
    expect(ENCUADRES['general_nav_footer'].map(e => e.ancla)).toEqual(['app-navigation', 'app-footer']);
  });
});
