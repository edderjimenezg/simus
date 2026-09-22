import { TestBed } from '@angular/core/testing';
import { fichaDeLaDireccion, mensajeAceptable, TextosWebConVistaPrevia } from './vista-previa-puente';
import {
  PublicWebContent,
  PublicWebImages,
  PublicWebTeam,
  ContenidoWebApiService,
} from '../services/contenido-web-api.service';
import { DEFAULT_IMAGE_URLS } from './registro-de-imagenes-web';

/*
  EL PUENTE DE LA VISTA PREVIA, guarda por guarda.

  Lo que se protege: que una pagina del sitio publico solo acepte textos sin
  publicar cuando esta dentro del marco del panel, abierta a proposito como vista
  previa, y el mensaje viene de ese panel y no de otro sitio. Cada guarda tiene su
  prueba, porque una guarda sin prueba es una guarda que alguien puede quitar
  «simplificando» sin que nada se ponga rojo.
*/
describe('vista previa · qué mensajes puede aceptar el sitio público', () => {
  const PADRE = { nombre: 'el panel' } as unknown;
  const OTRO = { nombre: 'otro marco' } as unknown;

  const contexto = {
    origenPropio: 'http://localhost:4300',
    ventanaPadre: PADRE,
    ficha: 'v123',
  };

  const mensajeBueno = {
    origin: 'http://localhost:4300',
    source: PADRE,
    data: { tipo: 'pnmc:borrador', ficha: 'v123', textos: { home_tag: 'Hola' } },
  };

  it('acepta el mensaje del panel que lo abrió', () => {
    expect(mensajeAceptable(mensajeBueno, contexto)).toBeTrue();
  });

  it('rechaza un mensaje de otro origen', () => {
    // Sin esta guarda, cualquier página que consiga incrustar la nuestra podría
    // reescribir sus textos.
    expect(mensajeAceptable({ ...mensajeBueno, origin: 'https://otro.sitio' }, contexto)).toBeFalse();
  });

  it('rechaza un mensaje que no viene de la ventana padre', () => {
    expect(mensajeAceptable({ ...mensajeBueno, source: OTRO }, contexto)).toBeFalse();
  });

  it('rechaza el mensaje de otra pestaña del panel', () => {
    // Dos pestañas del panel abiertas a la vez: sin la ficha, la de Home movería
    // los textos del marco de la de Ecosistema.
    const deOtraPestana = { ...mensajeBueno, data: { ...(mensajeBueno.data as object), ficha: 'v999' } };
    expect(mensajeAceptable(deOtraPestana, contexto)).toBeFalse();
  });

  it('rechaza cualquier mensaje si esta página no es una vista previa', () => {
    expect(mensajeAceptable(mensajeBueno, { ...contexto, ficha: null })).toBeFalse();

    // Y EL CASO QUE HACE FALTA LA GUARDA, que un mutante destapó. Sin la primera
    // línea de `mensajeAceptable`, la comprobación de ficha es una comparación
    // entre dos valores: un mensaje que trae `ficha: null` en una página cuya
    // ficha también es null casa consigo mismo y pasa. Es decir, una página del
    // sitio abierta sin `?pnmcVista` aceptaría textos sin publicar.
    const conFichaNula = { ...mensajeBueno, data: { tipo: 'pnmc:borrador', ficha: null, textos: { home_tag: 'x' } } };
    expect(mensajeAceptable(conFichaNula, { ...contexto, ficha: null }))
      .withContext('una página que no es vista previa aceptó un borrador')
      .toBeFalse();
  });

  it('rechaza lo que no sea un borrador con textos', () => {
    expect(mensajeAceptable({ ...mensajeBueno, data: { tipo: 'otra-cosa', ficha: 'v123', textos: {} } }, contexto)).toBeFalse();
    expect(mensajeAceptable({ ...mensajeBueno, data: { tipo: 'pnmc:borrador', ficha: 'v123' } }, contexto)).toBeFalse();
    expect(mensajeAceptable({ ...mensajeBueno, data: null }, contexto)).toBeFalse();
  });

  it('la ficha sale de la dirección, y sin parámetro no hay vista previa', () => {
    expect(fichaDeLaDireccion('?pnmcVista=v123&v=2')).toBe('v123');
    expect(fichaDeLaDireccion('?v=2')).toBeNull();
    expect(fichaDeLaDireccion('')).toBeNull();
    expect(fichaDeLaDireccion('?pnmcVista=')).toBeNull();
  });
});

/*
  EL EFECTO DE UN MENSAJE ACEPTADO.

  Las pruebas de arriba miden QUE mensajes pasan; estas miden QUE HACEN los que pasan.
  Se empuja el mensaje por `aplicar` y no por un `postMessage` real porque las guardas 1
  y 2 dependen de `window.top` y de `location.search`, que en Karma no los fija la prueba.

  La propiedad que defienden, y que el 29 de agosto no existia: el panel podia subir una
  portada nueva y la previsualizacion seguia pintando la anterior, porque `serverImages`
  solo trae lo publicado. Previsualizar servia para todo menos para lo unico que hacia
  falta mirar antes de publicar una imagen.
*/
describe('vista previa · qué hace un mensaje aceptado', () => {
  class ApiDoble {
    async getPublicTexts(): Promise<PublicWebContent | null> { return null; }
    async getPublicTeam(): Promise<PublicWebTeam | null> { return null; }
    async getPublicImages(): Promise<PublicWebImages | null> { return null; }
  }

  function crear(): TextosWebConVistaPrevia {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        TextosWebConVistaPrevia,
        { provide: ContenidoWebApiService, useValue: new ApiDoble() },
      ],
    });
    return TestBed.inject(TextosWebConVistaPrevia);
  }

  const mensaje = (extra: Record<string, unknown>) =>
    ({ tipo: 'pnmc:borrador' as const, ficha: 'v1', textos: {}, ...extra });

  it('una imagen sin publicar tapa a la compilada', () => {
    const servicio = crear();
    expect(servicio.getWebImage('home_hero_1')).toBe(DEFAULT_IMAGE_URLS['home_hero_1']);

    servicio.aplicar(mensaje({ imagenes: { home_hero_1: 'blob:nueva' } }));
    expect(servicio.getWebImage('home_hero_1')).toBe('blob:nueva');
  });

  it('solo tapa las claves que llegaron', () => {
    const servicio = crear();
    servicio.aplicar(mensaje({ imagenes: { home_hero_1: 'blob:nueva' } }));
    expect(servicio.getWebImage('home_hero_2')).toBe(DEFAULT_IMAGE_URLS['home_hero_2']);
  });

  it('un mensaje sin imágenes sigue moviendo los textos', () => {
    // El panel envio solo textos hasta. Un `imagenes` obligatorio
    // habria dejado la previsualizacion de textos muda sin que nada lo dijera.
    const servicio = crear();
    servicio.aplicar(mensaje({ textos: { home_tag: 'Nueva etiqueta' } }));
    expect(servicio.getWebText('home_tag')).toBe('Nueva etiqueta');
  });

  it('el segundo mensaje se suma al primero, no lo reemplaza', () => {
    // El panel manda solo las claves del bloque abierto. Sustituir en vez de fusionar
    // borraria del marco lo que se escribio en el bloque anterior al cambiar de bloque.
    const servicio = crear();
    servicio.aplicar(mensaje({ imagenes: { home_hero_1: 'blob:uno' } }));
    servicio.aplicar(mensaje({ imagenes: { home_hero_2: 'blob:dos' } }));
    expect(servicio.getWebImage('home_hero_1')).toBe('blob:uno');
    expect(servicio.getWebImage('home_hero_2')).toBe('blob:dos');
  });

  it('una URL vacía NO tapa a la compilada', () => {
    // Al reves que en los textos, y a proposito: `<img src="">` no deja un hueco, hace
    // que el navegador vuelva a pedir la URL de la pagina. `textos-web.service.ts`
    // protege esa propiedad para el visitante y la previsualizacion no la puede romper.
    const servicio = crear();
    servicio.aplicar(mensaje({ imagenes: { home_hero_1: '' } }));
    expect(servicio.getWebImage('home_hero_1')).toBe(DEFAULT_IMAGE_URLS['home_hero_1']);
  });

  it('un texto vacío SÍ tapa al compilado', () => {
    // La otra mitad del contraste anterior. Vaciar un rotulo es una decision editorial
    // legitima y la previsualizacion tiene que ensenarla vacia: es el defecto PNMC-040.
    const servicio = crear();
    servicio.aplicar(mensaje({ textos: { home_tag: '' } }));
    expect(servicio.getWebText('home_tag')).toBe('');
  });
});
