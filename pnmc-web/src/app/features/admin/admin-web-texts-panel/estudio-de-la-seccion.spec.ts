import { ComponentFixture, TestBed } from '@angular/core/testing';
import {
  AdminWebTextsPanelComponent,
  SECCION_INICIAL,
  TAMANOS_DE_VISTA,
  calcularEncuadre,
  primerGrupoDe,
} from './admin-web-texts-panel.component';
import { IMAGENES_DEL_BLOQUE } from '../../../core/cms/registro-de-imagenes-web';

/*
  EL ESTUDIO: el bloque que se edita y la página real, en la misma pantalla.

  El estudio es el único diseño del panel y cubre todas sus secciones.

  QUÉ SE MIRA. El formulario ocupaba nueve columnas y la
  previsualización iba en una banda DEBAJO. Medido con el navegador en una ventana de 1600×1000:
  el marco empezaba en y=1011, es decir siempre fuera de pantalla. Para ver el efecto de una
  frase había que dejar de verla, y las imágenes se administraban en otra pantalla distinta.

  LO QUE ESTAS PRUEBAS NO HACEN: mirar dentro del marco, ni medir píxeles de la maqueta. Lo
  primero no arranca en Karma; lo segundo lo comprueba el guion de visión con el navegador real.
  Aquí se afirma lo que decide el componente: qué diseño se pinta, qué se monta, qué se manda al
  marco y cuándo hay que recargarlo.
*/
describe('AdminWebTextsPanelComponent · el estudio, en todas las secciones', () => {
  let fixture: ComponentFixture<AdminWebTextsPanelComponent>;
  let componente: AdminWebTextsPanelComponent;

  function raiz(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  beforeEach(async () => {
    localStorage.removeItem('pnmc_web_texts');
    await TestBed.configureTestingModule({
      imports: [AdminWebTextsPanelComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(AdminWebTextsPanelComponent);
    componente = fixture.componentInstance;
    componente._enabled.set(true);
    // Sin marco: este fichero no mira dentro de la previsualización y montar la aplicación
    // entera dentro de Karma la haría cargar la batería de pruebas dentro de sí misma.
    componente.vistaEncendida.set(false);
    fixture.detectChanges();
  });

  afterEach(() => localStorage.removeItem('pnmc_web_texts'));

  it('todas las secciones abren en el estudio, con su primer bloque desplegado', () => {
    // Es la prueba del punto 2 del brief y sustituye a la que fijaba lo contrario: hasta el 30
    // de agosto de 2026 el estudio estaba detrás de `SECCION_DE_ESTUDIO = 'Home'` y las otras
    // demás seguían con la maquetación de tres columnas, que ya no existe.
    //
    // SE RECORREN TODAS Y NO UNA MUESTRA. Lo que se está afirmando es que no queda ninguna
    // sección con el diseño viejo, y una muestra no puede afirmar eso.
    expect(componente.sections.length).toBeGreaterThan(0);

    for (const { section } of componente.sections) {
      componente.selectPill(section);
      fixture.detectChanges();

      expect(raiz().querySelector('[data-testid="estudio"]')).withContext(section).not.toBeNull();
      expect(raiz().querySelector('[data-testid="group-list"]'))
        .withContext(section + ' siguió pintando la lista de grupos del diseño viejo')
        .toBeNull();

      // El bloque abierto es el primero de la sección, y coincide con el que gobierna el marco.
      // Son dos señales distintas: dejarlas discrepar deja la previsualización encuadrada en un
      // bloque que en pantalla está plegado.
      const primero = componente.filteredGroups()[0];
      expect(componente.bloqueAbierto()).withContext(section).toBe(primero.id);
      expect(componente.selectedGroup()).withContext(section).toBe(primero.id);
      expect(raiz().querySelector('#cuerpo-' + primero.id)).withContext(section).not.toBeNull();
    }
  });

  it('NINGÚN grupo pierde campos en el formulario, salvo los tres ejes que se parten a propósito', () => {
    /*
      LA PRUEBA QUE FALTABA, y el defecto que la trajo.

      El panel decidía si repartir un grupo en subpestañas mirando `section === 'Ejes'`. Era
      cierto mientras esa sección tuviera exactamente los tres grupos de eje. El 30 de agosto de
      2026 entraron dos más —el encabezado de la página y la ficha de componente— y el filtro los
      alcanzó: de las claves acabadas en `_title` conserva solo `eje01_title`, así que
      `ejes_hero_title` y las tres `component_*_title` desaparecieron del formulario.

      El campo seguía declarado, seguía en el catálogo y seguía sembrado en la base. Simplemente
      no había ningún sitio donde escribirlo, y nada fallaba: `cms:huerfanas` lo ve en la página,
      `cms:reservas` no mira formularios y la instantánea no cambia porque el valor de fábrica
      sigue saliendo. Se encontró mirando una captura de pantalla.

      Lo que se afirma es lo mínimo que un panel de edición tiene que cumplir: que se pueda
      escribir todo lo que ofrece.
    */
    const PARTIDOS = ['eje1_details', 'eje2_details', 'eje3_details'];
    const perdidos: string[] = [];

    for (const grupo of componente.GROUPS) {
      componente.selectPill(grupo.section);
      componente.alternarBloque(grupo.id);
      fixture.detectChanges();

      const pintadas = new Set(componente.keysToRender());
      if (PARTIDOS.includes(grupo.id)) {
        // Estos tres SÍ reparten, y por eso se comprueba lo contrario: que la primera pestaña
        // enseñe algo y que la unión de todas las pestañas cubra el grupo entero.
        expect(pintadas.size).withContext(grupo.id).toBeGreaterThan(0);
        const deTodasLasPestanas = new Set<string>();
        for (const pestana of componente.ejeSubTabsList()) {
          componente.activeEjeSubTab.set(pestana.id);
          fixture.detectChanges();
          for (const k of componente.keysToRender()) { deTodasLasPestanas.add(k); }
        }
        for (const k of grupo.keys) {
          if (!deTodasLasPestanas.has(k)) { perdidos.push(`${grupo.id}/${k} (ninguna pestaña)`); }
        }
        continue;
      }

      for (const k of grupo.keys) {
        if (!pintadas.has(k)) { perdidos.push(`${grupo.id}/${k}`); }
      }
    }

    expect(perdidos)
      .withContext('claves que el panel ofrece en el catálogo y no dibuja en ningún formulario')
      .toEqual([]);
  });

  it('solo los tres grupos de eje muestran subpestañas', () => {
    // El otro lado del mismo defecto: la pastilla «Información General» aparecía sobre el
    // encabezado de /ejes y sobre la ficha de componente, que no tienen componentes dentro.
    for (const grupo of componente.GROUPS.filter((g) => g.section === 'Ejes')) {
      componente.selectPill('Ejes');
      componente.alternarBloque(grupo.id);
      fixture.detectChanges();

      const esperado = ['eje1_details', 'eje2_details', 'eje3_details'].includes(grupo.id);
      expect(componente.ejeSubTabsList().length > 0).withContext(grupo.id).toBe(esperado);
    }
  });

  it('al entrar por primera vez, sin tocar nada, el primer bloque ya está abierto', () => {
    // El valor INICIAL, que es otro caso que el de `selectPill`: nadie ha pulsado una pestaña
    // todavía. Eran tres literales escritos en tres sitios —sección, grupo y bloque abierto— y
    // ya se rompió una vez, con el acordeón estrenándose plegado. Ahora los tres salen de
    // `primerGrupoDe(SECCION_INICIAL)`.
    expect(componente.selectedSection()).toBe(SECCION_INICIAL);

    // SE COMPARA CONTRA `filteredGroups`, NO contra `primerGrupoDe`. Comparar el valor inicial
    // con la misma función que lo produjo es una tautología: un mutante que devolviera el
    // ÚLTIMO grupo de la sección la pasaba entera. Se detectó sembrándolo el 30 de agosto de
    // 2026, y por eso la aserción va contra la lista que el panel pinta.
    const primero = componente.filteredGroups()[0];
    expect(componente.selectedGroup()).toBe(primero.id);
    expect(componente.bloqueAbierto()).toBe(primero.id);
    expect(raiz().querySelector('#cuerpo-' + primero.id)).not.toBeNull();

    // Y que la función pública diga lo mismo: es la que ata los tres valores iniciales.
    expect(primerGrupoDe(SECCION_INICIAL)).toBe(primero.id);
  });

  it('se listan todos los bloques de la sección y solo se monta el cuerpo del abierto', () => {
    // Se comprueba en la sección MÁS CARGADA del panel, no en el Home: «Sobre PNMC» suma 94
    // campos en siete bloques, y es donde montar los siete de golpe se nota al teclear.
    componente.selectPill('Sobre PNMC');
    fixture.detectChanges();

    const cabeceras = raiz().querySelectorAll('[data-testid^="bloque-"]');
    expect(cabeceras.length).toBe(7);
    expect(cabeceras.length).toBe(componente.filteredGroups().length);

    const cuerpos = raiz().querySelectorAll('[id^="cuerpo-"]');
    expect(cuerpos.length).toBe(1);
    expect(cuerpos[0].id).toBe('cuerpo-' + componente.bloqueAbierto());
  });

  it('abrir un bloque es lo que mueve la previsualización', () => {
    // `selectedGroup` gobierna `encuadreActivo`, y `encuadreActivo` es lo que desplaza el marco
    // hasta el bloque. Si abrir un bloque no lo cambiara, el acordeón y la página real irían
    // por separado, que es justo lo que este diseño viene a arreglar.
    componente.selectPill('Home');
    fixture.detectChanges();

    componente.alternarBloque('home_bulletin');
    fixture.detectChanges();

    expect(componente.selectedGroup()).toBe('home_bulletin');
    expect(componente.encuadreActivo()?.etiqueta).toBe('Boletín');
    expect(raiz().querySelector('#cuerpo-home_bulletin')).not.toBeNull();
  });

  it('volver a pulsar el bloque abierto lo pliega sin apagar la previsualización', () => {
    componente.selectPill('Home');
    componente.alternarBloque('home_about');
    fixture.detectChanges();

    componente.alternarBloque('home_about');
    fixture.detectChanges();

    expect(componente.bloqueAbierto()).toBeNull();
    expect(raiz().querySelectorAll('[id^="cuerpo-"]').length).toBe(0);
    // Y EL MARCO SE QUEDA DONDE ESTABA. Dejar `selectedGroup` en null al plegar apagaría la
    // previsualización justo cuando la persona la está mirando para decidir a dónde ir.
    expect(componente.selectedGroup()).toBe('home_about');
    expect(componente.encuadreActivo()).not.toBeNull();
  });

  it('cambiar de sección abre el primer bloque de la nueva', () => {
    componente.selectPill('Home');
    componente.alternarBloque('home_bulletin');
    componente.selectPill('Home');
    fixture.detectChanges();

    expect(componente.bloqueAbierto()).toBe(componente.filteredGroups()[0].id);
  });

  it('cinco de los ocho bloques del Home declaran imágenes, y cada uno las suyas', () => {
    // Eran cuatro ranuras en un solo bloque hasta. Ese día entraron las
    // otras dieciocho, que es lo que hace que la portada se pueda editar entera: la foto de
    // «Huella y evolución», las ocho de Rutas, las tres del banner y las seis del Ecosistema.
    componente.selectPill('Home');
    const conImagenes = componente.filteredGroups()
      .filter(g => componente.imagenesDelBloque(g.id).length > 0)
      .map(g => g.id);

    expect(conImagenes.sort()).toEqual(
      ['home_about', 'home_banner', 'home_ecosistema', 'home_hero', 'home_strategies_cards']);

    expect(componente.imagenesDelBloque('home_hero').length).toBe(4);
    expect(componente.imagenesDelBloque('home_about').length).toBe(1);
    expect(componente.imagenesDelBloque('home_strategies_cards').length).toBe(8);
    expect(componente.imagenesDelBloque('home_banner').length).toBe(3);
    expect(componente.imagenesDelBloque('home_ecosistema').length).toBe(6);

    // El registro y el panel tienen que decir lo mismo: una tabla con una clave que el panel no
    // consulta es una ranura que nadie puede editar y que nada delata.
    for (const id of conImagenes) {
      expect(componente.imagenesDelBloque(id)).toEqual(IMAGENES_DEL_BLOQUE[id]);
    }
  });

  it('una imagen sin publicar se espeja sobre las cuatro portadas que rotan', () => {
    // La portada del Home elige UNA DE CUATRO AL AZAR en cada visita (home.component.ts).
    // Sin espejar, cambiar la portada 1 y mirar la previsualización enseña la que le toque, y
    // tres de cada cuatro veces no es la que se está cambiando.
    componente.selectPill('Home');
    componente.recibirImagenEnVivo({
      clave: 'home_hero_1',
      url: 'blob:recorte-nuevo',
      rotanConElla: ['home_hero_1', 'home_hero_2', 'home_hero_3', 'home_hero_4'],
      recargarElSitio: false,
    });
    fixture.detectChanges();

    expect(componente.borradorParaElMarco().imagenes).toEqual({
      home_hero_1: 'blob:recorte-nuevo',
      home_hero_2: 'blob:recorte-nuevo',
      home_hero_3: 'blob:recorte-nuevo',
      home_hero_4: 'blob:recorte-nuevo',
    });
  });

  it('una ranura que no rota solo se pinta a sí misma', () => {
    componente.selectPill('Home');
    componente.recibirImagenEnVivo({
      clave: 'hero_ejes', url: 'blob:otra', rotanConElla: [], recargarElSitio: false,
    });
    fixture.detectChanges();

    expect(componente.borradorParaElMarco().imagenes).toEqual({ hero_ejes: 'blob:otra' });
  });

  it('retirar una imagen la quita del mensaje en vez de mandarla vacía', () => {
    // Mandar cadena vacía no sirve: el puente descarta las URLs vacías a propósito, porque un
    // `<img src="">` hace que el navegador vuelva a pedir la página. Hay que quitar la clave.
    componente.selectPill('Home');
    componente.recibirImagenEnVivo({
      clave: 'home_hero_1', url: 'blob:x',
      rotanConElla: ['home_hero_1', 'home_hero_2'], recargarElSitio: false,
    });
    componente.recibirImagenEnVivo({
      clave: 'home_hero_1', url: null,
      rotanConElla: ['home_hero_1', 'home_hero_2'], recargarElSitio: true,
    });
    fixture.detectChanges();

    expect(componente.borradorParaElMarco().imagenes).toEqual({});
  });

  it('publicar recarga el marco y subir no', () => {
    componente.selectPill('Home');
    fixture.detectChanges();
    const antes = componente.versionDeLaVista();

    // Subir: el borrador viaja por mensaje y se ve al instante. Recargar aquí tiraría la página
    // entera para pintar exactamente lo mismo.
    componente.recibirImagenEnVivo({
      clave: 'home_hero_1', url: 'blob:x', rotanConElla: [], recargarElSitio: false,
    });
    expect(componente.versionDeLaVista()).toBe(antes);

    // Publicar: cambia el manifiesto que sirve el servidor, y eso el marco solo lo relee
    // volviendo a cargar la página.
    componente.recibirImagenEnVivo({
      clave: 'home_hero_1', url: null, rotanConElla: [], recargarElSitio: true,
    });
    expect(componente.versionDeLaVista()).toBe(antes + 1);
  });

  it('la pastilla cuenta las imágenes sin publicar, no solo los campos', () => {
    // Sin esto, subir una portada y no tocar ningún texto dejaba la pastilla en «Igual a lo que
    // estás editando» mientras el marco pintaba una imagen que el visitante no ve todavía.
    componente.selectPill('Home');
    expect(componente.estadoDeLaVista()).toBe('Igual a lo que estás editando');

    componente.recibirImagenEnVivo({
      clave: 'home_hero_1', url: 'blob:x',
      rotanConElla: ['home_hero_1', 'home_hero_2', 'home_hero_3', 'home_hero_4'],
      recargarElSitio: false,
    });
    // UNA, no cuatro: el espejo pinta la misma imagen en las cuatro claves, pero sin publicar
    // hay una sola ranura.
    expect(componente.imagenesSinPublicar()).toBe(1);
    expect(componente.estadoDeLaVista()).toBe('1 imagen sin publicar');

    componente.handleInputChange('home_title', 'Otro titular');
    expect(componente.estadoDeLaVista()).toBe('1 campo y 1 imagen sin publicar');

    componente.recibirImagenEnVivo({
      clave: 'home_hero_1', url: null,
      rotanConElla: ['home_hero_1', 'home_hero_2', 'home_hero_3', 'home_hero_4'],
      recargarElSitio: true,
    });
    expect(componente.estadoDeLaVista()).toBe('1 campo sin publicar');
  });

  it('el tamaño elegido manda sobre el marco y sobre la escala', () => {
    // Medido en una pantalla de 1440 con el estudio a dos columnas: el
    // hueco de la previsualización da 613 px, y 1440 lógicos ahí quedan en escala 0,40 —un texto
    // de 16 px se pinta a 6,4—. Con «Teléfono» los mismos 613 px dan escala 1,00.
    componente.selectPill('Home');
    componente.anchoDisponible.set(613);
    fixture.detectChanges();

    expect(componente.tamanoDeVista().id).toBe('escritorio');
    expect(componente.ANCHO_LOGICO()).toBe(1440);
    expect(componente.escala()).toBeCloseTo(613 / 1440, 3);

    const telefono = TAMANOS_DE_VISTA.find(t => t.id === 'telefono')!;
    componente.elegirTamano(telefono);
    fixture.detectChanges();

    expect(componente.ANCHO_LOGICO()).toBe(390);
    expect(componente.ALTO_LOGICO()).toBe(844);
    // NUNCA PASA DE 1: ampliar una página de 390 px por encima de su tamaño no enseña nada
    // nuevo y desdibuja el texto.
    expect(componente.escala()).toBe(1);
    expect(componente.anchoPintado()).toBe(390);
  });

  it('cambiar de tamaño no recarga la página del marco', () => {
    // Recargar tiraría la página entera para volver a montar la misma: lo único que cambia es
    // el tamaño del hueco por el que se mira.
    componente.selectPill('Home');
    fixture.detectChanges();
    const version = componente.versionDeLaVista();

    componente.elegirTamano(TAMANOS_DE_VISTA[1]);
    fixture.detectChanges();

    expect(componente.versionDeLaVista()).toBe(version);
  });

  it('el encuadre se calcula contra el alto del marco elegido', () => {
    // Con el alto por defecto un bloque de 400 px se centra en 810; en un teléfono de 844 el
    // centro es otro, y en una tableta de 768 también. Sin pasar el alto, el marco del teléfono
    // dejaría el bloque descolocado y nadie sabría por qué.
    expect(calcularEncuadre({ top: 2000, alto: 400 }, 6000)).toBe(2000 - (810 - 400) / 2);
    expect(calcularEncuadre({ top: 2000, alto: 400 }, 6000, 844)).toBe(2000 - (844 - 400) / 2);
    expect(calcularEncuadre({ top: 2000, alto: 400 }, 6000, 768)).toBe(2000 - (768 - 400) / 2);
  });

  it('los textos que viajan al marco son los del bloque abierto', () => {
    componente.selectPill('Home');
    componente.alternarBloque('home_hero');
    componente.handleInputChange('home_title', 'Otro titular');
    fixture.detectChanges();

    const textos = componente.borradorParaElMarco().textos;
    expect(textos['home_title']).toBe('Otro titular');
    expect(Object.keys(textos).sort())
      .toEqual([...componente.activeGroupObj()!.keys].sort());
  });
});
