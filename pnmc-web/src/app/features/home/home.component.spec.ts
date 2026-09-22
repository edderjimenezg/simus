import { Component, Directive, EventEmitter, Input, Output } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { EMPTY } from 'rxjs';
import { NavigationService } from '../../core/services/navigation.service';
import { TextosWebService } from '../../core/services/textos-web.service';
import { BoletinService } from '../../core/services/boletin.service';
import { BoletinFormComponent } from './components/boletin-form/boletin-form.component';
import { HomeComponent } from './home.component';

/*
  SE SUSTITUYEN LOS HIJOS, NO LOS SERVICIOS DE LOS HIJOS. La portada monta nueve componentes, y
  varios piden datos por HTTP en su ngOnInit. Levantarlos de verdad para medir dónde queda un
  bloque de la plantilla convertiría esta prueba en una que falla por motivos que no son el suyo:
  un cambio en el mapa la pondría roja sin que la maquetación se hubiera movido. Se reemplazan por
  cajas vacías con la misma interfaz y se mide la plantilla de la portada, que es lo que se afirma.
*/

@Component({ selector: 'app-page-hero', standalone: true, template: '<div class="flex flex-wrap gap-4"><ng-content select="[buttons]"></ng-content></div>' })
class HeroFalso {
  @Input() tag = '';
  @Input() title = '';
  @Input() titleAccent = '';
  @Input() description = '';
  @Input() bgImage = '';
  @Input() bgImageClassName = '';
  @Input() fullScreen = false;
}

@Component({ selector: 'app-tag', standalone: true, template: '{{ text }}' })
class EtiquetaFalsa {
  @Input() text = '';
}

@Component({ selector: 'app-footer', standalone: true, template: '' })
class PieFalso {}

@Component({ selector: 'app-pnmc-preview-section', standalone: true, template: '' })
class PreviewFalso {
  @Input() scrollTargetElement: HTMLElement | null = null;
  @Output() navegacionSolicitada = new EventEmitter<string>();
}

@Component({ selector: 'app-home-strategies-section', standalone: true, template: '' })
class EstrategiasFalsas {}

@Component({ selector: 'app-home-media-banner', standalone: true, template: '' })
class BannerFalso {}

@Component({ selector: 'app-mapa-ecosistemico-preview', standalone: true, template: '' })
class MapaFalso {
  @Output() navigateToMapLayer = new EventEmitter<string>();
}

@Component({ selector: 'app-noticias-agenda-preview', standalone: true, template: '' })
class NoticiasFalsas {
  @Output() navigate = new EventEmitter<string>();
  @Output() navigateToArticle = new EventEmitter<unknown>();
  @Output() navigateToAgendaEvent = new EventEmitter<string>();
}

/*
  EL FORMULARIO DEL BOLETIN SE MONTA DE VERDAD, y es la unica excepcion a la regla de arriba.

  Aqui habia un doble con `template: ''`. Con el, el host de `app-boletin-form` medía 0x0 y no
  existia ningun boton «Registrarme» en el arbol: cualquier afirmacion del tipo «los iconos estan
  al lado del boton» pasaba en verde sin medir nada, porque no habia boton contra el que medir.

  Montarlo es seguro y se comprobo: `BoletinFormComponent` no tiene `ngOnInit` ni constructor
  propio, y solo llama al servicio en `cargarPolitica()` (boletin-form.component.ts) y en
  `confirmar()` (:135), las dos por accion del visitante. Sin pulsar nada, el doble de
  `BoletinService` no recibe una sola llamada; esta ahi para que un dia que si la reciba no salga
  una peticion HTTP de una prueba de maquetacion.
*/
const BOLETIN_SERVICE_FALSO = {
  consultarPolitica: () => EMPTY,
  suscribir: () => EMPTY,
};

/*
  Escrito una vez y compartido por los dos `describe`. Estaba en linea dentro del `TestBed`, y al
  aparecer un segundo bloque de pruebas se habria copiado tal cual: cuatro avisos mas de
  `no-empty-function`, que es un error de lint y no un aviso. `() => undefined` devuelve algo, asi
  que la regla no salta, y de paso deja de saltar por las cinco que ya habia.
*/
const NAVEGACION_FALSA = {
  navigate: () => undefined,
  navigateToArticle: () => undefined,
  navigateToAgendaEvent: () => undefined,
  navigateToMapLayer: () => undefined,
};

@Directive({
  // Debe reproducir el selector de la directiva externa de Lucide para sustituirla en esta prueba.
  // eslint-disable-next-line @angular-eslint/directive-selector
  selector: '[lucideArrowRight]',
  standalone: true,
  host: {
    '[style.width.px]': 'size',
    '[style.height.px]': 'size',
  },
})
class FlechaFalsa {
  @Input() size = 0;
}

/*
  EL MARCO DE KARMA MIDE 747 px, Y ESE ES EL PROBLEMA QUE ESTE AYUDANTE RESUELVE.

  Medido, no supuesto: una sonda que imprimio `document.documentElement.clientWidth` dentro del
  navegador de las pruebas devolvio 747. A ese ancho NO aplica ni `md:` (768), ni `lg:` (1024), ni
  `xl:` (1280). Todo lo que la banda del boletin hace en escritorio —la fila horizontal, las redes
  al lado del boton, el relleno de 64 px— es invisible a esa anchura: el arbol se pinta apilado,
  que es justo la maquetacion que ESTE CAMBIO SUSTITUYE.

  Consecuencia incomoda para las pruebas que habia: la que afirmaba «las redes van debajo del
  boletin» medía pixeles de verdad, pero a 747 px, donde todo esta apilado de todas formas. Estaba
  en verde por el ancho del marco, no por la maquetacion.

  El ayudante ensancha el iframe y CIERRA COMPROBANDOSE A SI MISMO: si el ensanche no surte efecto,
  muere aqui en vez de dejar que la prueba mida 747 px creyendo que mide 1400. Los 20 px de holgura
  son la barra de desplazamiento.
*/
function anchoDeMarco(px: number): void {
  const marco = window.frameElement as HTMLElement | null;
  expect(marco).withContext('las pruebas no corren dentro de un iframe: el ayudante no sirve').not.toBeNull();

  marco!.style.width = `${px}px`;
  void document.body.offsetWidth;

  expect(document.documentElement.clientWidth)
    .withContext(`el marco no se ensancho a ${px}px, la medida siguiente seria falsa`)
    .toBeGreaterThanOrEqual(px - 20);
}

function restaurarMarco(): void {
  const marco = window.frameElement as HTMLElement | null;
  if (marco) marco.style.width = '';
}

/** La caja del boton «Registrarme», que es contra lo que se define medir «al lado». */
function cajaDelBoton(raiz: HTMLElement): DOMRect {
  const boton = raiz.querySelector<HTMLElement>('[data-testid="boletin-enviar"]');
  expect(boton).withContext('no se pinto el boton del boletin').not.toBeNull();
  return boton!.getBoundingClientRect();
}

function cajaDeLosIconos(raiz: HTMLElement): DOMRect {
  const iconos = raiz.querySelector<HTMLElement>('[data-testid="home-social-links"]');
  expect(iconos).withContext('no se pintaron los enlaces de redes').not.toBeNull();
  return iconos!.getBoundingClientRect();
}

function elementosQueDesbordan(): string {
  const ancho = document.documentElement.clientWidth;
  return Array.from(document.body.querySelectorAll<HTMLElement>('*'))
    .filter(elemento => elemento.getBoundingClientRect().right > ancho + 1)
    .slice(0, 6)
    .map(elemento => {
      const caja = elemento.getBoundingClientRect();
      return `${elemento.tagName.toLowerCase()}.${elemento.className}=${Math.round(caja.left)}+${Math.round(caja.width)}→${Math.round(caja.right)}`;
    })
    .join(' | ');
}

describe('HomeComponent', () => {
  let fixture: ComponentFixture<HomeComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [HomeComponent],
      providers: [
        { provide: NavigationService, useValue: NAVEGACION_FALSA },
        {
          provide: TextosWebService,
          useValue: {
            getWebText: (clave: string) => clave,
            // getWebImage, porque desde las cuatro portadas del
            // Home salen del CMS. Sin este metodo, `ngOnInit` revienta con
            // «getWebImage is not a function» y las diez pruebas de esta suite mueren
            // por el doble, no por la maqueta. Devuelve la clave: estas pruebas miden
            // geometria, no que imagen se pinta.
            getWebImage: (clave: string) => clave,
            getWebImageAlt: (clave: string) => clave,
          },
        },
        { provide: BoletinService, useValue: BOLETIN_SERVICE_FALSO },
      ],
    })
      .overrideComponent(HomeComponent, {
        set: {
          imports: [HeroFalso, EtiquetaFalsa, PieFalso, PreviewFalso, EstrategiasFalsas, BannerFalso, MapaFalso, NoticiasFalsas, BoletinFormComponent, FlechaFalsa],
        },
      })
      .compileComponents();

    fixture = TestBed.createComponent(HomeComponent);
    fixture.detectChanges();
  });

  afterEach(restaurarMarco);

  it('las redes van a la derecha del boton «Registrarme», en el mismo renglon', () => {
    // ESTO ES LO QUE PIDIO EL USUARIO, y son dos afirmaciones, no una. «A la derecha» sin «en el
    // mismo renglon» lo cumple tambien un bloque que este a la derecha pero mas abajo, que es
    // exactamente la maquetacion que este cambio sustituye.
    //
    // Se mide contra el BOTON y no contra el host del formulario: el usuario escribio «al lado de
    // registrarme», y el host incluye la linea de aviso `min-h-[1rem]` que va debajo del boton.
    anchoDeMarco(1400);
    const raiz = fixture.nativeElement as HTMLElement;

    const boton = cajaDelBoton(raiz);
    const iconos = cajaDeLosIconos(raiz);

    expect(iconos.left)
      .withContext('los iconos no quedaron a la derecha del boton')
      .toBeGreaterThanOrEqual(boton.right);
    expect(iconos.top < boton.bottom && boton.top < iconos.bottom)
      .withContext('los iconos y el boton no comparten renglon')
      .toBe(true);
  });

  it('el formulario y las redes son UN grupo, no dos hermanos de la fila', () => {
    // Lo que ningun getBoundingClientRect ve: que alguien saque las redes a un tercer bloque de
    // primer nivel y la maquetacion coincida por casualidad al ancho al que se mide. Eran tres
    // columnas peleando por el renglon, y esa es la maquetacion que se retiro el 28 de agosto en
    // el commit 1a50174. La diferencia no es estetica: con tres hermanos y `justify-between`, las
    // redes se clavan contra el borde derecho.
    const raiz = fixture.nativeElement as HTMLElement;
    const fila = raiz.querySelector<HTMLElement>('[data-testid="home-bulletin-row"]')!;
    const grupo = raiz.querySelector<HTMLElement>('[data-testid="home-bulletin-actions"]')!;
    const redes = raiz.querySelector<HTMLElement>('[data-testid="home-social-block"]')!;
    const formulario = raiz.querySelector<HTMLElement>('app-boletin-form')!;

    expect(grupo).withContext('desaparecio el grupo de acciones').not.toBeNull();
    expect(grupo.contains(formulario)).withContext('el formulario salio del grupo').toBe(true);
    expect(grupo.contains(redes)).withContext('las redes salieron del grupo').toBe(true);
    expect(fila.children.length)
      .withContext('la fila dejo de tener dos hijos: algo volvio a colarse en su nivel')
      .toBe(2);
  });

  it('por debajo de 768 px las redes vuelven debajo del boton y nada se sale', () => {
    // La otra mitad de la primera prueba. El mismo arbol tiene que dar renglon en escritorio y
    // apilado en movil; sin esto, nadie vigila que el arreglo de escritorio aplaste el telefono.
    anchoDeMarco(360);
    const raiz = fixture.nativeElement as HTMLElement;

    const boton = cajaDelBoton(raiz);
    const iconos = cajaDeLosIconos(raiz);

    expect(iconos.top)
      .withContext('a 360 px los iconos siguen al lado del boton')
      .toBeGreaterThanOrEqual(boton.bottom);
    expect(document.documentElement.scrollWidth)
      .withContext('la portada desborda a lo ancho en movil: ' + elementosQueDesbordan())
      .toBeLessThanOrEqual(document.documentElement.clientWidth + 1);
  });

  it('la banda no llega al borde: el relleno crece por tramos y es el unico margen que hay', () => {
    // LO SEGUNDO QUE PIDIO CRITERIO: «que no quede al limite de la pantalla, que tenga margen».
    //
    // La segunda mitad de cada vuelta demuestra POR QUE el relleno es la unica palanca: mientras el
    // ancho de pantalla no llega a 1600 px, `max-w-[100rem]` no recorta nada y el riel mide
    // exactamente lo que la banda. Sin esa comprobacion, alguien podria «arreglar» el margen
    // tocando el ancho maximo y no cambiaria nada en las pantallas reales.
    const tramos = [
      { ancho: 400, relleno: 24 },
      { ancho: 800, relleno: 40 },
      { ancho: 1100, relleno: 48 },
      { ancho: 1400, relleno: 64 },
    ];
    const raiz = fixture.nativeElement as HTMLElement;
    const riel = raiz.querySelector<HTMLElement>('[data-testid="home-bulletin-rail"]')!;
    const banda = raiz.querySelector<HTMLElement>('[data-testid="home-bulletin-banner"]')!;

    for (const tramo of tramos) {
      anchoDeMarco(tramo.ancho);
      const estilo = getComputedStyle(riel);

      expect(estilo.paddingLeft)
        .withContext(`relleno izquierdo equivocado a ${tramo.ancho}px`)
        .toBe(`${tramo.relleno}px`);
      expect(estilo.paddingRight)
        .withContext(`relleno derecho equivocado a ${tramo.ancho}px`)
        .toBe(`${tramo.relleno}px`);
      expect(Math.round(riel.getBoundingClientRect().width))
        .withContext(`a ${tramo.ancho}px el ancho maximo recorto: el relleno ya no es el unico margen`)
        .toBe(Math.round(banda.getBoundingClientRect().width));
    }
  });

  it('no desborda a lo ancho en ninguna anchura de escritorio', () => {
    // El riesgo real de meter tres cosas donde habia dos. Se recorren las anchuras en las que la
    // fila es horizontal, incluida 1024, que es la mas apretada: el grupo de acciones pide su
    // ancho fijo y quien cede es el bloque de texto de la izquierda.
    for (const ancho of [1024, 1280, 1920]) {
      anchoDeMarco(ancho);
      expect(document.documentElement.scrollWidth)
        .withContext(`la portada desborda a lo ancho a ${ancho}px`)
        .toBeLessThanOrEqual(document.documentElement.clientWidth + 1);
    }
  });

  it('el bloque de redes sigue pintando las dos claves del CMS', () => {
    // POR QUE EXISTE PUDIENDO HABER UNA PUERTA: `npm run cms:huerfanas` comprueba que alguien
    // NOMBRE la clave en `src/app`, no que la pinte. Un `getWebText('home_social_title')` dentro
    // de un bloque que nunca se cumple pasaria esa puerta en verde. Esta prueba es la mitad que
    // falta, y el doble de TextosWebService devuelve la clave como valor, asi que se busca la clave.
    const redes = (fixture.nativeElement as HTMLElement)
      .querySelector<HTMLElement>('[data-testid="home-social-block"]')!;

    expect(redes.textContent).toContain('home_social_title');
    expect(redes.textContent).toContain('home_social_desc');
  });

  it('los tres enlaces de redes conservan su nombre accesible', () => {
    // Dentro de cada enlace solo hay un `svg` sin texto: el `aria-label` es el unico nombre que
    // tienen. Se afirman el orden y la cantidad a proposito, para que un mutante que duplique o
    // reordene un enlace tambien se vea.
    const enlaces = Array.from(
      (fixture.nativeElement as HTMLElement)
        .querySelectorAll<HTMLAnchorElement>('[data-testid="home-social-links"] a'),
    );

    expect(enlaces.map(enlace => enlace.getAttribute('aria-label'))).toEqual([
      'Síguenos en Instagram',
      'Síguenos en Facebook',
      'Síguenos en YouTube',
    ]);
  });
});
/*
  EL PEOR TEXTO QUE EL PANEL PUEDE PUBLICAR, que es distinto del texto de fábrica.

  El resto del archivo usa un doble que devuelve la clave como valor: cadenas cortas y parecidas
  entre sí. Con eso, una maquetación que reviente cuando la editora escriba un rótulo largo pasa
  en verde. Los topes no son inventados, salen del registro (core/cms/registro-de-textos-web.ts):
  `home_bulletin_btn` admite 40 caracteres, `home_social_title` 80 y `home_social_desc` 120.
*/
const TOPES_DEL_CMS: Record<string, number> = {
  home_bulletin_btn: 40,
  home_social_title: 80,
  home_social_desc: 120,
  home_bulletin_title: 80,
  home_bulletin_desc: 200,
  home_bulletin_placeholder: 55,
};

describe('HomeComponent con los textos del CMS en su tope', () => {
  let fixture: ComponentFixture<HomeComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [HomeComponent],
      providers: [
        { provide: NavigationService, useValue: NAVEGACION_FALSA },
        {
          provide: TextosWebService,
          useValue: {
            // Palabras de 9 letras separadas por espacios: un texto que SÍ puede partirse en varias
            // líneas. Una sola palabra de 120 caracteres desbordaría por `overflow-wrap`, que es un
            // defecto distinto y no el que se está midiendo aquí.
            getWebText: (clave: string) => {
              const tope = TOPES_DEL_CMS[clave];
              if (!tope) return clave;
              return 'Registrar '.repeat(Math.ceil(tope / 10)).slice(0, tope).trim();
            },
            // Las portadas del Home salen del CMS desde el 29 ago 2026. Ver la nota del
            // doble de arriba.
            getWebImage: (clave: string) => clave,
            getWebImageAlt: (clave: string) => clave,
          },
        },
        { provide: BoletinService, useValue: BOLETIN_SERVICE_FALSO },
      ],
    })
      .overrideComponent(HomeComponent, {
        set: {
          imports: [HeroFalso, EtiquetaFalsa, PieFalso, PreviewFalso, EstrategiasFalsas, BannerFalso, MapaFalso, NoticiasFalsas, BoletinFormComponent, FlechaFalsa],
        },
      })
      .compileComponents();

    fixture = TestBed.createComponent(HomeComponent);
    fixture.detectChanges();
  });

  afterEach(restaurarMarco);

  it('con los rotulos mas largos que el panel admite, nada se sale de la banda', () => {
    // SE MIDE CONTENCION, NO DESPLAZAMIENTO, y esa distincion la destapo un mutante que sobrevivio.
    //
    // La primera version de esta prueba comparaba `scrollWidth` con `clientWidth`, y quitar
    // `md:flex-wrap` la dejaba en verde. El motivo: la banda lleva `overflow-hidden`
    // (home.component.html:64), asi que lo que se sale no ensancha el documento —se RECORTA—. Con
    // un rotulo largo el boton se cortaba por la mitad y ninguna medida del documento se enteraba.
    //
    // Comparar el borde derecho del grupo de acciones contra el del riel sí lo ve. Se deja tambien
    // la medida del documento, que atrapa el defecto contrario: algo que empuje la pagina entera.
    //
    // Se incluye 800 px a proposito: es la franja de 768 a 1023 en la que la fila exterior sigue
    // apilada pero el grupo de acciones ya es horizontal.
    const raiz = fixture.nativeElement as HTMLElement;
    const riel = raiz.querySelector<HTMLElement>('[data-testid="home-bulletin-rail"]')!;
    const acciones = raiz.querySelector<HTMLElement>('[data-testid="home-bulletin-actions"]')!;

    for (const ancho of [360, 800, 1024, 1280, 1920]) {
      anchoDeMarco(ancho);

      // SE COMPARA CONTRA LA CAJA DE CONTENIDO DEL RIEL, NO CONTRA SU BORDE, y la diferencia no es
      // teorica: la primera version comparaba contra `riel.right` a secas, que incluye el relleno.
      // Con ella, a 1024 px el grupo terminaba a 14 px del borde de la pantalla —se habia comido
      // los 48 px de relleno enteros— y la prueba seguia verde. Justo el margen que se pidio.
      const cajaRiel = riel.getBoundingClientRect();
      const bordeDelContenido = cajaRiel.right - parseFloat(getComputedStyle(riel).paddingRight);

      expect(acciones.getBoundingClientRect().right)
        .withContext(`a ${ancho}px el grupo del boletin invade el margen de la banda`)
        .toBeLessThanOrEqual(bordeDelContenido + 0.5);
      expect(document.documentElement.scrollWidth)
        .withContext(`desborda a lo ancho a ${ancho}px con los textos en su tope: ${elementosQueDesbordan()}`)
        .toBeLessThanOrEqual(document.documentElement.clientWidth + 1);
    }
  });

  it('el texto largo de redes no le come el ancho al campo de correo', () => {
    // La columna de redes tiene ancho fijo (`md:w-40`, 160 px) justamente para esto: sin el, un
    // subtexto de 120 caracteres se dimensiona a `max-content`, empuja al formulario y le quita
    // sitio al boton. El campo tiene sus 280 px escritos en boletin-form.component.html:19.
    anchoDeMarco(1400);
    const raiz = fixture.nativeElement as HTMLElement;
    const redes = raiz.querySelector<HTMLElement>('[data-testid="home-social-block"]')!;
    const campo = raiz.querySelector<HTMLElement>('[data-testid="boletin-correo"]')!;

    expect(Math.round(redes.getBoundingClientRect().width))
      .withContext('la columna de redes dejo de medir 160 px')
      .toBe(160);
    expect(Math.round(campo.getBoundingClientRect().width))
      .withContext('el campo de correo perdio sus 280 px')
      .toBe(280);
  });
});
