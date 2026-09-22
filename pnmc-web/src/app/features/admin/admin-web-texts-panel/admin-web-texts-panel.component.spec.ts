import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AdminWebTextsPanelComponent } from './admin-web-texts-panel.component';
import { WEB_TEXT_GROUPS, WEB_TEXT_SECTIONS } from '../../../core/services/textos-web.service';

describe('AdminWebTextsPanelComponent team editor', () => {
  let fixture: ComponentFixture<AdminWebTextsPanelComponent>;
  let component: AdminWebTextsPanelComponent;

  beforeEach(async () => {
    localStorage.removeItem('pnmc_web_team_members');
    localStorage.removeItem('pnmc_web_media');
    localStorage.removeItem('pnmc_web_texts');

    await TestBed.configureTestingModule({
      imports: [AdminWebTextsPanelComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(AdminWebTextsPanelComponent);
    component = fixture.componentInstance;
    component._enabled.set(true);
    // EL MARCO SE APAGA EN LAS PRUEBAS. Carga una ruta de la aplicacion, y en
    // Karma esas rutas las sirve el propio Karma con su pagina de contexto: el
    // marco montaba la bateria de pruebas dentro de la bateria, en bucle, hasta
    // que Chrome se desconectaba.
    component.vistaEncendida.set(false);

    component.selectPill('Sobre PNMC');
    // ABRIR EL BLOQUE, no solo elegirlo. Desde el panel es un acordeón y
    // el cuerpo del grupo solo se monta si su bloque está desplegado: poner `selectedGroup` a
    // mano movía la previsualización pero dejaba el formulario sin pintar. `alternarBloque` es lo
    // que hace el clic en la cabecera, y pone los dos valores a la vez.
    component.alternarBloque('about_team');
    fixture.detectChanges();
  });

  afterEach(() => {
    localStorage.removeItem('pnmc_web_team_members');
    localStorage.removeItem('pnmc_web_media');
    localStorage.removeItem('pnmc_web_texts');
  });

  it('el editor de fichas se pinta dentro de su bloque, y con vista al lado', () => {
    /*
      ESTA PRUEBA HA CAMBIADO DOS VECES, y las dos por el mismo motivo: afirmaba el MEDIO en vez
      del fin.

      Primero decía que «Equipo de Trabajo» no tenía previsualización —cierto entonces: se
      escondía para que el editor de fichas ocupara nueve columnas—. Luego pasó a exigir
      `lg:col-span-9`, que era la columna del formulario en la maquetación de tres columnas. El 30
      de agosto de 2026 esa maquetación se retiró: el estudio es de dos columnas y el formulario
      vive dentro del bloque del acordeón, así que no hay ninguna rejilla de doce donde ocupar
      nueve.

      Lo que se afirma ahora es el fin: que el editor esté, que se pinte dentro del cuerpo de su
      bloque —y no suelto en la página— y que la vista de al lado siga estando.
    */
    const root = fixture.nativeElement as HTMLElement;
    const editor = root.querySelector<HTMLElement>('[data-testid="team-editor"]');

    expect(editor).not.toBeNull();
    expect(root.textContent).toContain('Agregar persona');
    expect(editor!.closest('#cuerpo-about_team'))
      .withContext('el editor de fichas se salió del bloque del acordeón')
      .not.toBeNull();
    expect(root.querySelector('[data-testid="live-preview"]'))
      .withContext('«Equipo» se quedo sin vista')
      .not.toBeNull();
  });

  it('keeps general settings collapsed by default', () => {
    expect(component.teamSettingsOpen()).toBeFalse();
    const root = fixture.nativeElement as HTMLElement;
    expect(root.querySelector('[data-testid="team-settings-fields"]')).toBeNull();
  });

  it('adds, edits and removes a person as one record', () => {
    const initialCount = component.teamMembers().length;
    component.addTeamMember();
    const added = component.teamMembers().at(-1)!;

    component.updateTeamMember(added.id, 'name', 'Persona de Prueba');
    component.updateTeamMember(added.id, 'role', 'Liderazgo de prueba');
    component.updateTeamMember(added.id, 'email', 'persona@example.com');

    expect(component.teamMembers().length).toBe(initialCount + 1);
    expect(component.teamMembers().at(-1)).toEqual(jasmine.objectContaining({
      name: 'Persona de Prueba',
      role: 'Liderazgo de prueba',
      email: 'persona@example.com',
    }));

    component.removeTeamMember(added.id);
    expect(component.teamMembers().length).toBe(initialCount);
  });

  it('deja todas las secciones con al menos un grupo editable', () => {
    // El editor se navega por pestanas: una seccion sin grupos es una pestana
    // que se abre vacia y deja contenido sin forma de editarse.
    const emptySections = WEB_TEXT_SECTIONS
      .filter((section) => {
        component.selectPill(section.section);
        return component.filteredGroups().length === 0;
      })
      .map((section) => section.section);
    expect(emptySections).toEqual([]);
  });

  it('renderiza campos para cada grupo del registro', () => {
    const emptyGroups = WEB_TEXT_GROUPS
      .filter((group) => {
        component.selectPill(group.section);
        component.selectedGroup.set(group.id);
        return component.keysToRender().length === 0;
      })
      .map((group) => group.id);
    expect(emptyGroups).toEqual([]);
  });
});

describe('AdminWebTextsPanelComponent · ir a la página pública', () => {
  let fixture: ComponentFixture<AdminWebTextsPanelComponent>;
  let component: AdminWebTextsPanelComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AdminWebTextsPanelComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(AdminWebTextsPanelComponent);
    component = fixture.componentInstance;
    component._enabled.set(true);
    // EL MARCO SE APAGA EN LAS PRUEBAS. Carga una ruta de la aplicacion, y en
    // Karma esas rutas las sirve el propio Karma con su pagina de contexto: el
    // marco montaba la bateria de pruebas dentro de la bateria, en bucle, hasta
    // que Chrome se desconectaba.
    component.vistaEncendida.set(false);

    fixture.detectChanges();
  });

  it('sabe a qué página lleva cada grupo del registro', () => {
    // El boton solo se dibuja si hay ruta. Sin esta prueba, agregar una seccion
    // al registro y olvidar su ruta hace que el boton desaparezca en silencio en
    // esa pestana: no falla nada, simplemente deja de estar.
    const sinRuta = WEB_TEXT_GROUPS
      .filter((group) => {
        component.selectPill(group.section);
        component.selectedGroup.set(group.id);
        return component.paginaPublica() === null;
      })
      .map((group) => `${group.id} (${group.section})`);
    expect(sinRuta).toEqual([]);
  });

  it('manda cada sección a una página distinta y no todas a la portada', () => {
    // Una tabla de rutas mal copiada —todas a '/'— pasaria la prueba anterior
    // entera y dejaria el boton inutil en once de las doce pestanas.
    const rutas = WEB_TEXT_SECTIONS.map((section) => {
      component.selectPill(section.section);
      return component.paginaPublica();
    });
    expect(new Set(rutas).size).toBeGreaterThan(WEB_TEXT_SECTIONS.length - 3);
  });

  it('abre la página en otra pestaña, para no perder el borrador sin guardar', () => {
    component.selectPill('Home');
    component.selectedGroup.set('home_hero');
    fixture.detectChanges();

    const enlace = (fixture.nativeElement as HTMLElement)
      .querySelector<HTMLAnchorElement>('[data-testid="ir-a-la-pagina"]');

    expect(enlace).not.toBeNull();
    expect(enlace!.getAttribute('href')).toBe('/');
    expect(enlace!.target).toBe('_blank');
    expect(enlace!.rel).toContain('noopener');
  });

  it('avisa de que la página muestra lo publicado y no el borrador', () => {
    // El editor esta mirando su borrador en la columna de la izquierda; si el
    // enlace no lo dice, el texto sin publicar que no aparezca en la pagina se
    // lee como «no se guardo» y alguien vuelve a escribirlo.
    component.selectPill('Ecosistema');
    fixture.detectChanges();

    const enlace = (fixture.nativeElement as HTMLElement)
      .querySelector<HTMLAnchorElement>('[data-testid="ir-a-la-pagina"]');

    expect(enlace!.getAttribute('href')).toBe('/ecosistema');
    expect(enlace!.title).toContain('PUBLICADO');
    expect(enlace!.title).toContain('borrador');
  });
});
