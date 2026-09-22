/**
 * Las fotografías reales del acervo de galería que sirven de imagen por omisión.
 *
 * <b>SE LLAMABA `IMAGENES_DE_GALERIA` Y NO TIENE NADA DE ALEATORIO.</b> Es un catálogo fijo de
 * rutas que existen en <code>public/Galeria/</code>, y el registro de imágenes del CMS lo usa como
 * respaldo cuando una ranura todavía no tiene imagen propia. El nombre venía de cuando había una
 * función que elegía una al azar para rellenar huecos —la que hacía que Noticias nunca se viera
 * vacía—; esa función se retiró, y el nombre que quedó describía lo que ya no hace.
 *
 * <b>NO ES CONTENIDO INVENTADO:</b> son fotografías del Congreso Nacional de Música y de la Mesa
 * Nacional de Rock, que están en el repositorio y pertenecen al Programa.
 */
export const IMAGENES_DE_GALERIA = [
  '/Galeria/Congreso%20Nacional%20de%20M%C3%BAsica/Portada.jpg',
  '/Galeria/Congreso%20Nacional%20de%20M%C3%BAsica/1757622581114.jpg',
  '/Galeria/Congreso%20Nacional%20de%20M%C3%BAsica/1757622581128.jpg',
  '/Galeria/Congreso%20Nacional%20de%20M%C3%BAsica/1757622581143.jpg',
  '/Galeria/Congreso%20Nacional%20de%20M%C3%BAsica/1757622581161.jpg',
  '/Galeria/Congreso%20Nacional%20de%20M%C3%BAsica/1757622581179.jpg',
  '/Galeria/Congreso%20Nacional%20de%20M%C3%BAsica/1757622581216.jpg',
  '/Galeria/Congreso%20Nacional%20de%20M%C3%BAsica/1757622581255.jpg',
  '/Galeria/Congreso%20Nacional%20de%20M%C3%BAsica/1757622581278.jpg',
  '/Galeria/Congreso%20Nacional%20de%20M%C3%BAsica/1757622581320.jpg',
  '/Galeria/Congreso%20Nacional%20de%20M%C3%BAsica/1757622581357.jpg',
  '/Galeria/Congreso%20Nacional%20de%20M%C3%BAsica/1757622581384.jpg',
  '/Galeria/Congreso%20Nacional%20de%20M%C3%BAsica/1757624083659.jpg',
  '/Galeria/Congreso%20Nacional%20de%20M%C3%BAsica/1757631027664.jpg',
  '/Galeria/Congreso%20Nacional%20de%20M%C3%BAsica/1757631027922.jpg',
  '/Galeria/Congreso%20Nacional%20de%20M%C3%BAsica/1757776744366.jpg',
  '/Galeria/Congreso%20Nacional%20de%20M%C3%BAsica/1757776744860.jpg',
  '/Galeria/Congreso%20Nacional%20de%20M%C3%BAsica/1757776745025.jpg',
  '/Galeria/Congreso%20Nacional%20de%20M%C3%BAsica/1757778375972.jpg'
];

export const ORIGINAL_MEDIA_LIBRARY = {
  homeHero: 'https://images.unsplash.com/photo-1774557482533-76b2ed54afce?q=80&w=1015&auto=format&fit=crop',
  performanceWide: 'https://images.unsplash.com/photo-1774558396280-c14b21198674?q=80&w=1470&auto=format&fit=crop',
  fieldworkWide: 'https://images.unsplash.com/photo-1774558396253-be05d7a37d82?q=80&w=1470&auto=format&fit=crop',
  cultureWide: 'https://images.unsplash.com/photo-1774558396250-1571cdddc61c?q=80&w=687&auto=format&fit=crop',
};

export const MEDIA_LIBRARY = { ...ORIGINAL_MEDIA_LIBRARY };

/*
  HOME_HERO_IMAGES VIVIA AQUI Y SE RETIRO EL 29 DE AGOSTO DE 2026.

  Las cuatro portadas del Home pasaron a ser administrables, y sus URLs viven
  ahora en `core/cms/registro-de-imagenes-web.ts` como valor de fabrica de las claves
  `home_hero_1..4`. La lista se resuelve en `home.component.ts`, que si puede
  inyectar el servicio.

  NO SE PUEDE RESOLVER EN ESTE FICHERO, y por eso se fue: este modulo se evalua
  al cargarse, antes de que exista un inyector y antes de que llegue el
  manifiesto. Una llamada al servicio desde aqui capturaria un valor una sola vez
  y no lo actualizaria nunca; el sintoma seria «publique y no cambio».

  `MEDIA_LIBRARY` se queda porque la galeria lo usa como imagen de respaldo de un
  album sin portada (galeria-page.component.ts), y eso no es una ranura de
  diseno administrable.
*/

export const NEWS_GALLERY_IMAGES = [
  '/Galeria/Mesa%20Nacional%20de%20Rock%20-%20Instalaci%C3%B3n/WhatsApp%20Image%202026-04-07%20at%2011.32.36%20(1).jpeg',
  '/Galeria/Mesa%20Nacional%20de%20Rock%20-%20Instalaci%C3%B3n/WhatsApp%20Image%202026-04-07%20at%2011.32.36%20(2).jpeg',
  '/Galeria/Mesa%20Nacional%20de%20Rock%20-%20Instalaci%C3%B3n/WhatsApp%20Image%202026-04-07%20at%2011.32.36%20(3).jpeg',
  '/Galeria/Mesa%20Nacional%20de%20Rock%20-%20Instalaci%C3%B3n/WhatsApp%20Image%202026-04-07%20at%2011.32.36%20(4).jpeg',
  '/Galeria/Mesa%20Nacional%20de%20Rock%20-%20Instalaci%C3%B3n/WhatsApp%20Image%202026-04-07%20at%2011.32.36%20(5).jpeg',
  '/Galeria/Mesa%20Nacional%20de%20Rock%20-%20Instalaci%C3%B3n/WhatsApp%20Image%202026-04-07%20at%2011.32.36%20(6).jpeg',
  '/Galeria/Mesa%20Nacional%20de%20Rock%20-%20Instalaci%C3%B3n/WhatsApp%20Image%202026-04-07%20at%2011.32.36%20(7).jpeg',
  '/Galeria/Mesa%20Nacional%20de%20Rock%20-%20Instalaci%C3%B3n/WhatsApp%20Image%202026-04-07%20at%2011.32.36%20(8).jpeg',
  '/Galeria/Mesa%20Nacional%20de%20Rock%20-%20Instalaci%C3%B3n/WhatsApp%20Image%202026-04-07%20at%2011.32.36%20(9).jpeg',
  '/Galeria/Mesa%20Nacional%20de%20Rock%20-%20Instalaci%C3%B3n/WhatsApp%20Image%202026-04-07%20at%2011.32.36%20(10).jpeg',
  '/Galeria/Mesa%20Nacional%20de%20Rock%20-%20Instalaci%C3%B3n/WhatsApp%20Image%202026-04-07%20at%2011.32.36%20(11).jpeg',
  '/Galeria/Mesa%20Nacional%20de%20Rock%20-%20Instalaci%C3%B3n/WhatsApp%20Image%202026-04-07%20at%2011.32.36%20(12).jpeg',
  '/Galeria/Mesa%20Nacional%20de%20Rock%20-%20Instalaci%C3%B3n/WhatsApp%20Image%202026-04-07%20at%2011.32.36%20(13).jpeg',
  '/Galeria/Mesa%20Nacional%20de%20Rock%20-%20Instalaci%C3%B3n/WhatsApp%20Image%202026-04-07%20at%2011.32.36%20(14).jpeg',
  '/Galeria/Mesa%20Nacional%20de%20Rock%20-%20Instalaci%C3%B3n/WhatsApp%20Image%202026-04-07%20at%2011.32.36%20(15).jpeg',
  '/Galeria/Mesa%20Nacional%20de%20Rock%20-%20Instalaci%C3%B3n/WhatsApp%20Image%202026-04-07%20at%2011.32.36%20(16).jpeg',
  '/Galeria/Mesa%20Nacional%20de%20Rock%20-%20Instalaci%C3%B3n/WhatsApp%20Image%202026-04-07%20at%2011.32.36%20(17).jpeg',
  '/Galeria/Mesa%20Nacional%20de%20Rock%20-%20Instalaci%C3%B3n/WhatsApp%20Image%202026-04-07%20at%2011.32.36%20(18).jpeg',
  '/Galeria/Mesa%20Nacional%20de%20Rock%20-%20Instalaci%C3%B3n/WhatsApp%20Image%202026-04-07%20at%2011.32.36%20(19).jpeg',
  '/Galeria/Mesa%20Nacional%20de%20Rock%20-%20Instalaci%C3%B3n/WhatsApp%20Image%202026-04-07%20at%2011.32.36%20(20).jpeg',
  '/Galeria/Congreso%20Nacional%20de%20M%C3%BAsica/1757878939720.jpg',
  '/Galeria/Congreso%20Nacional%20de%20M%C3%BAsica/1757878939735.jpg',
  '/Galeria/Congreso%20Nacional%20de%20M%C3%BAsica/1757878939753.jpg',
  '/Galeria/Congreso%20Nacional%20de%20M%C3%BAsica/1757878939769.jpg',
  '/Galeria/Congreso%20Nacional%20de%20M%C3%BAsica/1757878939785.jpg',
  '/Galeria/Congreso%20Nacional%20de%20M%C3%BAsica/1757878939800.jpg',
  '/Galeria/Congreso%20Nacional%20de%20M%C3%BAsica/1757878939816.jpg',
  '/Galeria/Congreso%20Nacional%20de%20M%C3%BAsica/1757878939833.jpg',
  '/Galeria/Congreso%20Nacional%20de%20M%C3%BAsica/1757878939849.jpg',
  '/Galeria/Congreso%20Nacional%20de%20M%C3%BAsica/1757878939866.jpg'
];
