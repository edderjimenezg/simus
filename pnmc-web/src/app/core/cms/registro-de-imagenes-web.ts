/**
 * Registro unico de las imagenes administrables del sitio publico.
 *
 * Gemelo de registro-de-textos-web.ts y con su misma disciplina: datos puros, sin
 * importar Angular y sin tocar almacenamiento, para que el exportador pueda
 * evaluarlo y sembrar la tabla dbo.MediosWeb desde aqui.
 *
 * UNA FILA POR RANURA, NO POR ARCHIVO. Los tres heroes comparten hoy la misma
 * URL de unsplash y aun asi llevan claves distintas: son decisiones editoriales
 * distintas, y quien cambie la portada de Ejes no quiere cambiar la de
 * Ecosistema. Son 16 ranuras sobre 10 archivos.
 *
 * `defaultUrl` es EXACTAMENTE lo que la plantilla tiene escrito hoy. Ese es el
 * mecanismo por el que el sitio se ve igual el dia que esto entre: mientras no
 * haya nada publicado en el CMS, `getWebImage` devuelve esta URL.
 *
 * LO QUE NO ENTRA, y no es un olvido:
 *  - La consola de administracion (admin-login.component.html) y las etiquetas
 *    Open Graph de index.html usan los mismos ficheros de marca, pero no son el
 *    sitio publico. Si entraran, el logotipo de la pantalla de inicio de sesion
 *    dependeria de lo que alguien publique.
 *  - NEWS_GALLERY_IMAGES: son las fotos de noticias y agenda, y las elige EL DATO
 *    —`buildNewsItemFromRecord` reparte por el id del registro—, no un editor.
 *
 * LO QUE SI ENTRO, y aqui estaba escrito lo contrario hasta. Este
 * comentario decia que las listas de `/Galeria/` eran «material de galeria, no ranuras de
 * diseno». El usuario lo revoco el 29 de agosto con un dato: una de ellas pintaba un archivo
 * de 725 KB y 1369x2048 dentro de un hueco apaisado de 438x318. Son decisiones editoriales de
 * portada elegidas a mano con un indice fijo, no galeria. Las dieciocho entraron el 30 de
 * agosto: la foto de «Huella y evolucion», las ocho tarjetas de Rutas, las tres diapositivas
 * del banner y las seis tarjetas del Ecosistema.
 */

export type UsoDeImagenWeb = 'fondo' | 'logotipo';

export interface DefinicionDeCampoDeImagen {
  key: string;
  label: string;
  /** fondo se recorta con object-cover; logotipo se encaja con object-contain. */
  use: UsoDeImagenWeb;
  /**
   * Falso para la marca institucional ajena. Que el panel deje reemplazar el
   * escudo nacional no es una funcion. La base lo impone ademas por CHECK
   * (CK_MediosWeb_NoEditableSinBytes), asi que no depende de que el API se
   * acuerde de comprobarlo.
   */
  editable: boolean;
  /** Texto alternativo de partida. El panel lo puede cambiar. */
  alt: string;
  /**
   * EL MARCO REAL, medido con el navegador sobre el sitio a 1440 px, no una medida deseable.
   *
   * Deja de ser «una guia para quien sube»: es el rectangulo al que el recortador ajusta la
   * imagen, asi que un numero mal puesto recorta al marco equivocado. Ocho de las catorce ranuras
   * lo tenian mal —las tres portadas de pagina declaraban 1600x900,
   * relacion 1,78, para un hueco cuya relacion real va de 4,00 a 4,44— y por eso este comentario
   * dice ahora de donde sale el numero.
   */
  suggestedWidth: number;
  suggestedHeight: number;
  /** Lo que el sitio muestra mientras no haya nada publicado en el CMS. */
  defaultUrl: string;
}

export interface DefinicionDeGrupoDeImagenes {
  id: string;
  label: string;
  section: string;
  images: DefinicionDeCampoDeImagen[];
}

const image = (
  key: string,
  label: string,
  use: UsoDeImagenWeb,
  editable: boolean,
  alt: string,
  suggestedWidth: number,
  suggestedHeight: number,
  defaultUrl: string,
): DefinicionDeCampoDeImagen => ({ key, label, use, editable, alt, suggestedWidth, suggestedHeight, defaultUrl });

// Las seis URLs de unsplash que el sitio usa hoy. Se nombran una vez para que
// las ranuras que comparten archivo se vea que lo comparten.
const UNSPLASH_HOME = 'https://images.unsplash.com/photo-1774557482533-76b2ed54afce?q=80&w=1015&auto=format&fit=crop';
const UNSPLASH_ESCENA = 'https://images.unsplash.com/photo-1774558396280-c14b21198674?q=80&w=1470&auto=format&fit=crop';
const UNSPLASH_CAMPO = 'https://images.unsplash.com/photo-1774558396253-be05d7a37d82?q=80&w=1470&auto=format&fit=crop';
const UNSPLASH_CULTURA = 'https://images.unsplash.com/photo-1774558396250-1571cdddc61c?q=80&w=687&auto=format&fit=crop';
const UNSPLASH_CELEBRA = 'https://images.unsplash.com/photo-1501386761578-eac5c94b800a?q=80&w=1600&auto=format&fit=crop';
const UNSPLASH_TERRITORIOS = 'https://images.unsplash.com/photo-1511379938547-c1f69419868d?q=80&w=1600&auto=format&fit=crop';

/*
  Las fotos de `/Galeria/Congreso Nacional de Musica/` que la portada usa hoy se declaran
  directamente en cada ranura. No se importan porque `tools/export-content-catalog.mjs` evalua
  este trozo de fichero suelto: un `import` lo rompe.

  CINCO SE REPITEN entre bloques —el banner 2 y «Mercados» comparten la [1]; el banner 3 y
  «Mesas» la [3]; «Congreso» y «Escuelas» la [6]; «Tempos» y «Festivales» la [8]; «Voces» y
  «Redes» la [10]—. Cada ranura lleva clave propia igual: quien cambie el fondo del banner no
  quiere cambiar de paso una tarjeta del carrusel.
*/

export const WEB_IMAGE_GROUPS: DefinicionDeGrupoDeImagenes[] = [
  {
    id: 'home_media',
    label: 'Portadas rotatorias del Home',
    section: 'Home',
    images: [
      // Las cuatro de ORIGINAL_MEDIA_LIBRARY (catalogo-de-imagenes-de-galeria.ts-32). El
      // Home las alterna: la primera es la que se ve al entrar.
      image('home_hero_1', 'Portada 1 (la primera que se ve)', 'fondo', true, 'Musicos en escena', 1670, 1044, UNSPLASH_HOME),
      image('home_hero_2', 'Portada 2', 'fondo', true, 'Presentacion musical', 1670, 1044, UNSPLASH_ESCENA),
      image('home_hero_3', 'Portada 3', 'fondo', true, 'Trabajo de campo con musicos', 1670, 1044, UNSPLASH_CAMPO),
      image('home_hero_4', 'Portada 4', 'fondo', true, 'Practica musical comunitaria', 1670, 1044, UNSPLASH_CULTURA),
    ],
  },
  {
    id: 'home_identidad',
    label: 'Fotografía de «Huella y evolución»',
    section: 'Home',
    images: [
      // pnmc-preview-section.component.ts (`IMAGENES_DE_GALERIA[5]`). Hueco medido con el
      // navegador: 676x275 en 1280, 1440, 1600 y 1920 —cuelga de `content-wrapper`, que satura
      // en 1280—, asi que es el unico marco de la portada que no se mueve nunca.
      image('home_identidad_media', 'Fotografía de «Huella y evolución»', 'fondo', true,
        'Músicos del Plan Nacional de Música', 676, 275, '/assets/home/identidad.webp'), // copia de G[5],
    ],
  },
  {
    id: 'home_rutas',
    label: 'Fotos de «Rutas de Acción Territorial»',
    section: 'Home',
    images: [
      // strategies-data.config.ts,30,40,50,60,70,80,90. El orden es el de las tarjetas en
      // pantalla, no el de los indices de la galeria.
      image('home_ruta_1', 'Ruta 1 · Celebra la Música', 'fondo', true, 'Celebra la Música', 491, 318, '/assets/home/ruta-1.webp'), // copia de G[2],
      image('home_ruta_2', 'Ruta 2 · Territorios Sonoros', 'fondo', true, 'Territorios Sonoros', 491, 318, '/assets/home/ruta-2.webp'), // copia de G[4],
      image('home_ruta_3', 'Ruta 3 · Congreso Nacional de Música', 'fondo', true, 'Congreso Nacional de Música', 491, 318, '/assets/home/ruta-3.webp'), // copia de G[6],
      image('home_ruta_4', 'Ruta 4 · Tempos de Memorias', 'fondo', true, 'Tempos de Memorias', 491, 318, '/assets/home/ruta-4.webp'), // copia de G[8],
      image('home_ruta_5', 'Ruta 5 · Voces y Saberes', 'fondo', true, 'Voces y Saberes', 491, 318, '/assets/home/ruta-5.webp'), // copia de G[10],
      image('home_ruta_6', 'Ruta 6 · Red Nacional de Jazz', 'fondo', true, 'Red Nacional de Jazz', 491, 318, '/assets/home/ruta-6.webp'), // copia de G[12],
      image('home_ruta_7', 'Ruta 7 · Mercados Musicales', 'fondo', true, 'Mercados Musicales de Colombia', 491, 318, '/assets/home/ruta-7.webp'), // copia de G[1],
      image('home_ruta_8', 'Ruta 8 · Mesas de Participación', 'fondo', true, 'Mesas de Participación', 491, 318, '/assets/home/ruta-8.webp'), // copia de G[3],
    ],
  },
  {
    id: 'home_banner',
    label: 'Fondos del banner deslizante',
    section: 'Home',
    images: [
      // home-media-banner.component.ts,41,49. El marco declarado es el MAS ANCHO del rango de
      // escritorio —1920x520, relacion 3,69—: el banner ocupa el ancho entero de la ventana y su
      // alto es fijo, de modo que la relacion va de 2,46 a 1280 hasta 3,69 a 1920.
      image('home_banner_1', 'Banner 1 · Sé parte del ecosistema', 'fondo', true, 'Público en un auditorio', 1920, 520, '/assets/home/banner-1.webp'), // copia de G[0],
      image('home_banner_2', 'Banner 2 · Celebra la Música', 'fondo', true, 'Celebra la Música', 1920, 520, '/assets/home/banner-2.webp'), // copia de G[1],
      image('home_banner_3', 'Banner 3 · Territorios Sonoros', 'fondo', true, 'Territorios Sonoros', 1920, 520, '/assets/home/banner-3.webp'), // copia de G[3],
    ],
  },
  {
    id: 'home_ecosistema',
    label: 'Fotos de los procesos del Ecosistema',
    section: 'Home',
    images: [
      // mapa-ecosistemico-preview.component.ts (`IMAGENES_DE_GALERIA[6 + index]`), en el
      // orden de `CATEGORIAS_ECOSISTEMA` filtrado por `proceso`. Se pintan al 30 % de opacidad
      // sobre #0f172b, asi que una foto clara se ve casi blanca.
      image('home_eco_1', 'Proceso 1 · Escuelas de música', 'fondo', true, 'Escuelas de música', 405, 208, '/assets/home/eco-1.webp'), // copia de G[6],
      image('home_eco_2', 'Proceso 2 · Escenarios', 'fondo', true, 'Escenarios', 405, 208, '/assets/home/eco-2.webp'), // copia de G[7],
      image('home_eco_3', 'Proceso 3 · Festivales', 'fondo', true, 'Festivales', 405, 208, '/assets/home/eco-3.webp'), // copia de G[8],
      image('home_eco_4', 'Proceso 4 · Mercados musicales', 'fondo', true, 'Mercados musicales', 405, 208, '/assets/home/eco-4.webp'), // copia de G[9],
      image('home_eco_5', 'Proceso 5 · Redes y documentación', 'fondo', true, 'Redes y documentación', 405, 208, '/assets/home/eco-5.webp'), // copia de G[10],
      image('home_eco_6', 'Proceso 6 · Lutería', 'fondo', true, 'Lutería', 405, 208, '/assets/home/eco-6.webp'), // copia de G[11],
    ],
  },
  /*
    LAS TRES PORTADAS DE PAGINA, UNA POR SECCION.

    Hasta eran un solo grupo, `page_heroes`, declarado entero en la
    seccion «Sobre PNMC». Con el panel de imagenes aparte daba igual —era una lista—, pero al
    editarse dentro del bloque de texto que las acompana, la portada de /ejes se cambiaba desde
    la pestana de Sobre PNMC. La seccion de una ranura tiene que ser la de la pagina donde se ve.

    El sembrador refresca `GroupId`, `GroupLabel` y `Section` de las filas que ya existen
    (MediosWebSeeder.cs), asi que partir el grupo no pierde ninguna imagen publicada.
  */
  {
    id: 'about_hitos',
    label: 'Fotos de los cinco hitos',
    section: 'Sobre PNMC',
    images: [
      /*
        LAS ONCE ULTIMAS RANURAS DE LAS 45 MEDIDAS EL 29 DE AGOSTO DE 2026 — cinco aqui y seis en
        el marco normativo. Salian de `IMAGENES_DE_GALERIA` con el indice a mano en
        `sobre-el-pnmc-page.component.ts` y `:70`: [11..15] y [16, 17, 18, 0, 1, 3].

        EL MARCO ES EL DE LA TARJETA ABIERTA. Medido con el navegador en 1280, 1440, 1600 y 1920:
        700x398 siempre, porque la fila satura en su contenedor. Plegada mide 117x398, relacion
        0,29, y por eso estas ranuras son de `dos-marcos`: el recortador ensena las dos.
      */
      image('about_hito_1', 'Hito 1 · 2003-2006', 'fondo', true, 'Creacion e institucionalizacion del PNMC', 700, 398, '/assets/pnmc/hito-1.webp'),
      image('about_hito_2', 'Hito 2 · 2007-2014', 'fondo', true, 'Territorializacion y saberes', 700, 398, '/assets/pnmc/hito-2.webp'),
      image('about_hito_3', 'Hito 3 · 2015-2018', 'fondo', true, 'Profesionalizacion y SIMUS', 700, 398, '/assets/pnmc/hito-3.webp'),
      image('about_hito_4', 'Hito 4 · 2018-2022', 'fondo', true, 'Evaluacion y consolidacion', 700, 398, '/assets/pnmc/hito-4.webp'),
      image('about_hito_5', 'Hito 5 · 2023-2025', 'fondo', true, 'Actualizacion y proyeccion 2035', 700, 398, '/assets/pnmc/hito-5.webp'),
    ],
  },
  {
    id: 'about_normas',
    label: 'Fotos del marco normativo',
    section: 'Sobre PNMC',
    images: [
      // Seis tarjetas en la misma fila que los cinco hitos, asi que la abierta mide menos:
      // 629x398 medido, y la plegada 105x398 (relacion 0,26).
      image('about_norma_1', 'Norma 1 · Ley 397 de 1997', 'fondo', true, 'Ley General de Cultura', 629, 398, '/assets/pnmc/norma-1.webp'),
      image('about_norma_2', 'Norma 2 · CONPES 3409 de 2006', 'fondo', true, 'CONPES 3409', 629, 398, '/assets/pnmc/norma-2.webp'),
      image('about_norma_3', 'Norma 3 · Ley 1493 de 2011', 'fondo', true, 'Ley de Espectaculos Publicos', 629, 398, '/assets/pnmc/norma-3.webp'),
      image('about_norma_4', 'Norma 4 · Decreto 2120 de 2018', 'fondo', true, 'Decreto 2120 de 2018', 629, 398, '/assets/pnmc/norma-4.webp'),
      image('about_norma_5', 'Norma 5 · Plan Nacional de Cultura', 'fondo', true, 'Plan Nacional de Cultura 2024-2038', 629, 398, '/assets/pnmc/norma-5.webp'),
      image('about_norma_6', 'Norma 6 · Ley 2555 de 2025', 'fondo', true, 'Ley 2555 de 2025', 629, 398, '/assets/pnmc/norma-6.webp'),
    ],
  },
  {
    id: 'about_portada',
    label: 'Portada de la pagina',
    section: 'Sobre PNMC',
    images: [
      image('hero_sobre_pnmc', 'Portada de «Sobre el PNMC»', 'fondo', true, 'Portada de Sobre el PNMC', 1440, 324, UNSPLASH_ESCENA),
    ],
  },
  {
    id: 'ejes_portada',
    label: 'Portada de la pagina',
    section: 'Ejes',
    images: [
      image('hero_ejes', 'Portada de «Ejes»', 'fondo', true, 'Portada de los ejes del Plan', 1440, 324, UNSPLASH_CAMPO),
    ],
  },
  {
    id: 'ecosistema_portada',
    label: 'Portada de la pagina',
    section: 'Ecosistema',
    images: [
      image('hero_ecosistema', 'Portada de «Ecosistema»', 'fondo', true, 'Portada del ecosistema musical', 1440, 324, UNSPLASH_CAMPO),
    ],
  },
  {
    id: 'ejes_media',
    label: 'Imagenes de los ejes',
    section: 'Ejes',
    images: [
      // Los tres videoImg de ejes-data.config.ts, :63 y :132.
      image('eje_01_media', 'Imagen del eje 1', 'fondo', true, 'Imagen del primer eje', 1280, 720, UNSPLASH_CAMPO),
      image('eje_02_media', 'Imagen del eje 2', 'fondo', true, 'Imagen del segundo eje', 1280, 720, UNSPLASH_ESCENA),
      image('eje_03_media', 'Imagen del eje 3', 'fondo', true, 'Imagen del tercer eje', 1280, 720, UNSPLASH_CAMPO),
    ],
  },
  {
    id: 'estrategias_media',
    label: 'Imagenes de las estrategias',
    section: 'Estrategias',
    images: [
      // resolve-strategy.ts y :72.
      image('estrategia_celebra_media', 'Imagen de «Celebra la Musica»', 'fondo', true, 'Celebra la Musica', 1440, 702, UNSPLASH_CELEBRA),
      image('estrategia_territorios_media', 'Imagen de «Territorios Sonoros»', 'fondo', true, 'Territorios Sonoros', 1440, 702, UNSPLASH_TERRITORIOS),
    ],
  },
  {
    id: 'marca',
    label: 'Identidad institucional',
    section: 'Navegación y Footer',
    images: [
      // La unica marca editable de las cuatro: es la del propio PNMC.
      image('marca_pnmc_blanco', 'Logotipo del PNMC (fondo oscuro)', 'logotipo', true, 'PNMC', 206, 128, '/assets/branding/pnmc-blanco.png'),
      // LAS DOS SIGUIENTES NO SON EDITABLES. Son marca institucional ajena:
      // GOV.CO y el sello de Colombia. Se
      // declaran para que esten inventariadas y para que nadie las busque,
      // no para que se puedan reemplazar.
      image('marca_gov_co', 'Marca GOV.CO', 'logotipo', false, 'GOV.CO', 200, 40, '/assets/branding/logo-gov-co.png'),
      image('marca_gov_co_sello', 'Sello de Colombia', 'logotipo', false, 'Colombia', 71, 70, '/assets/branding/gov-co-footer.png'),
    ],
  },
];

/** Todas las claves aplanadas, con la seccion y el grupo de su bloque. */
export const WEB_IMAGE_KEYS = WEB_IMAGE_GROUPS.flatMap((grupo) =>
  grupo.images.map((imagen) => ({
    ...imagen,
    groupId: grupo.id,
    groupLabel: grupo.label,
    section: grupo.section,
  })),
);

/** La URL compilada de una clave, o cadena vacia si la clave no existe. */
export const DEFAULT_IMAGE_URLS: Record<string, string> = Object.fromEntries(
  WEB_IMAGE_KEYS.map((imagen) => [imagen.key, imagen.defaultUrl]),
);

/** El texto alternativo de partida de cada clave. */
export const DEFAULT_IMAGE_ALTS: Record<string, string> = Object.fromEntries(
  WEB_IMAGE_KEYS.map((imagen) => [imagen.key, imagen.alt]),
);

/**
 * COMO PINTA EL SITIO CADA RANURA. Es lo que el recortador tiene que reproducir.
 *
 * NO ES DECORACION DEL PANEL. La portada del Home sale con `grayscale(1)` al 30 % sobre un
 * fondo `#291242`: una foto elegida a todo color se ve en el sitio como otra cosa. Los diez
 * tratamientos del sitio se midieron con el navegador el 29 y y
 * ninguno es neutro.
 *
 * VA DESPUES DE `WEB_IMAGE_KEYS` A PROPOSITO. `tools/export-content-catalog.mjs` recorta este
 * fichero entre `const image =` y el comentario de `WEB_IMAGE_KEYS`, y evalua lo que hay en
 * medio. Todo lo que se declare aqui abajo queda fuera de ese `eval` y no puede romper la
 * siembra de la base.
 */
export interface PresentacionDeLaRanura {
  /**
   * fija: la relacion no cambia nunca. variable: cambia con la ventana del visitante.
   * dos-marcos: el bloque se pinta abierto y plegado. encaje: el archivo se ve entero.
   */
  clase: 'fija' | 'variable' | 'dos-marcos' | 'encaje';
  /** El filtro CSS con el que el sitio pinta la imagen. */
  filtro: string;
  opacidad: number;
  /** El color que hay DEBAJO. Con opacidad menor que 1, el resultado depende de el. */
  fondo: string;
  /** La segunda relacion, en las ranuras de dos marcos. */
  relacionPlegada?: number;
  /** Donde el sitio pinta texto encima, en porcentaje del marco. Medido, no estimado. */
  zonaDeTexto?: { x: number; y: number; w: number; h: number };
}

/**
 * El tratamiento de la portada del Home, medido sobre el sitio.
 *
 * `clase: 'variable'` y es la ranura mas variable del catalogo: el hero va con
 * `[fullScreen]="true"` y `page-hero` le aplica `h-[100svh]`, asi que su relacion ES la de la
 * ventana. Medido en cinco tamanos: 1440x810 da 1,78; 1440x900 da 1,60; 1920x950 da 2,02;
 * 1280x1024 da 1,25; un telefono de 390x844 da 0,46. El marco declarado —1670x1044, relacion
 * 1,60— es el del portatil mas comun, no una promesa.
 *
 * La zona de texto se midio con `Range.getClientRects()` sobre los nodos de texto reales, no
 * con la caja del `h1`: la caja ocupa el ancho entero y las letras solo llegan al 53 %.
 */
const PORTADA_DEL_HOME: PresentacionDeLaRanura = {
  clase: 'variable',
  filtro: 'grayscale(1)',
  opacidad: 0.3,
  fondo: '#291242',
  zonaDeTexto: { x: 7.8, y: 31.9, w: 45.6, h: 44.3 },
};

/**
 * Los otros cuatro tratamientos de la portada, medidos con el navegador
 * a 1280, 1440, 1600 y 1920 de ancho.
 *
 * El `fondo` es el color que queda DEBAJO de la imagen, subiendo por los ancestros hasta el
 * primero que pinte algo opaco. Importa donde la opacidad no es 1: las tarjetas del Ecosistema
 * van al 30 % sobre `#0f172b`, asi que una foto clara ahi se ve casi blanca y una oscura casi
 * desaparece. Sin el fondo, la previsualizacion enseñaria la foto tal cual y la persona
 * elegiria a ciegas.
 */
const FOTO_DE_IDENTIDAD: PresentacionDeLaRanura = {
  // Unico marco de la portada que no cambia nunca: `content-wrapper` satura en 1280.
  clase: 'fija',
  filtro: 'brightness(0.9) grayscale(1)',
  opacidad: 1,
  fondo: '#0f172b',
  // SIN zona de texto, y se comprobo: ningun nodo de texto del documento cruza este rectangulo.
  // El titulo y la marca de agua «HUELLA» van encima en el flujo, no encima de la foto.
};

const TARJETA_DE_RUTA: PresentacionDeLaRanura = {
  // Alto fijo (320 px menos 2 de borde) y ancho que crece hasta que la pista topa en 100rem:
  // relacion 1,21 a 1280, 1,38 a 1440 y 1,55 de 1600 en adelante.
  clase: 'variable',
  filtro: 'none',
  opacidad: 1,
  fondo: '#ffffff',
  // El rotulo arriba y el titulo abajo, los dos a la izquierda. Medido sobre los nodos de texto.
  zonaDeTexto: { x: 4.6, y: 8.2, w: 43.9, h: 80.8 },
};

const DIAPOSITIVA_DEL_BANNER: PresentacionDeLaRanura = {
  // El que mas cambia de los cuatro: ocupa el ancho entero de la ventana con alto fijo, asi que
  // va de 2,46 a 1280 hasta 3,69 a 1920.
  clase: 'variable',
  filtro: 'brightness(0.5) saturate(0.7)',
  opacidad: 1,
  fondo: '#291242',
  // El texto va pegado al borde DERECHO (`justify-end` + `text-right`): el sujeto de la foto
  // tiene que quedar a la izquierda.
  zonaDeTexto: { x: 67.6, y: 31.9, w: 29.1, h: 47 },
};

const TARJETA_DEL_ECOSISTEMA: PresentacionDeLaRanura = {
  clase: 'fija',
  filtro: 'none',
  // AL 30 %, que es lo que hace imprescindible declarar el fondo.
  opacidad: 0.3,
  fondo: '#0f172b',
  zonaDeTexto: { x: 5.9, y: 8.7, w: 34.3, h: 81.3 },
};

/**
 * La portada de una pagina interior. Medida en cinco ventanas: el alto es
 * `40vh` y el ancho, el de la ventana, asi que la relacion la manda el visitante — 1280x900 da
 * 3,56; 1440x810 da 4,44; 1440x900 da 4,00; 1600x900 da 4,44; 1920x1080 da 4,44; un telefono de
 * 390x844 da 1,02. El marco declarado, 1440x324, es el de un portatil de 1440x810.
 *
 * `grayscale(1)` al 28 % sobre `#291242`: una foto elegida a todo color se ve en el sitio como
 * una textura morada. Es el mismo tratamiento de la portada del Home, con dos puntos menos de
 * opacidad.
 */
const PORTADA_DE_PAGINA: PresentacionDeLaRanura = {
  clase: 'variable',
  filtro: 'grayscale(1)',
  opacidad: 0.28,
  fondo: '#291242',
};

/**
 * Las tarjetas de Hitos y de Marco normativo de `/pnmc`: la unica familia de DOS MARCOS del
 * catalogo junto con las que ya lo eran.
 *
 * La fila se pinta con la tarjeta elegida en `flex-[6]` y las demas en `flex-1`, asi que cada
 * foto se ve en dos rectangulos muy distintos. Medido, y constante de 1280 a 1920 porque la fila
 * satura en su contenedor: hitos 700x398 abierta y 117x398 plegada; normas 629x398 y 105x398.
 *
 * El filtro es el mismo en los dos estados —`brightness(0.95) grayscale(0.4)`—, pero la opacidad
 * y el color de debajo NO: abierta va al 22 % sobre `#0f172a`, y plegada al 10 % sobre blanco con
 * un velo `#291242` al 30 % encima. Se declara la ABIERTA porque es la que alguien lee; la
 * plegada es un lomo de 117 px y ahi no se juzga una fotografia.
 */
const TARJETA_DE_HITO: PresentacionDeLaRanura = {
  clase: 'dos-marcos',
  filtro: 'brightness(0.95) grayscale(0.4)',
  opacidad: 0.22,
  fondo: '#0f172a',
  relacionPlegada: 0.29,
};

/** Igual que la anterior, con seis tarjetas en la fila en vez de cinco. */
const TARJETA_DE_NORMA: PresentacionDeLaRanura = {
  clase: 'dos-marcos',
  filtro: 'brightness(0.95) grayscale(0.4)',
  opacidad: 0.22,
  fondo: '#0f172a',
  relacionPlegada: 0.26,
};

/**
 * La imagen de cada eje en `/ejes`. Medida 660x371 —relacion 1,78— en 1280, 1440, 1600 y 1920:
 * es fija, la columna satura. Va al 80 % sobre `#f8fafc` con `brightness(0.95) grayscale(0.4)`:
 * casi a todo color, que es la unica del catalogo que se acerca a lo neutro y aun asi no lo es.
 */
const IMAGEN_DE_EJE: PresentacionDeLaRanura = {
  clase: 'fija',
  filtro: 'brightness(0.95) grayscale(0.4)',
  opacidad: 0.8,
  fondo: '#f8fafc',
};

/**
 * El fondo de la portada de una estrategia. Alto fijo de 702 px y ancho de ventana: 1440 da 2,05;
 * 1600 da 2,28; 1920 da 2,74. Sin filtro, pero al 30 % sobre `#291242`, que es lo que convierte
 * cualquier foto en una textura oscura.
 */
const PORTADA_DE_ESTRATEGIA: PresentacionDeLaRanura = {
  clase: 'variable',
  filtro: 'none',
  opacidad: 0.3,
  fondo: '#291242',
};

/**
 * Los cuatro logotipos. Se pintan con `object-contain` —se ven enteros, no se recortan— y por eso
 * son de clase `encaje`. Medidos: el del PNMC en 103x64 sobre el menu translucido, GOV.CO en
 * 160x32, el sello en 68x67 y el del Ministerio en la pantalla de acceso a la consola.
 *
 * Fondo `#291242` porque los cuatro se pintan sobre el morado de marca: un logotipo con fondo
 * blanco opaco se ve como un recorte, y en el recortador tiene que verse igual que en el sitio.
 */
const LOGOTIPO_SOBRE_MORADO: PresentacionDeLaRanura = {
  clase: 'encaje',
  filtro: 'none',
  opacidad: 1,
  fondo: '#291242',
};

export const PRESENTACION_DE_LA_RANURA: Record<string, PresentacionDeLaRanura> = {
  home_hero_1: PORTADA_DEL_HOME,
  home_hero_2: PORTADA_DEL_HOME,
  home_hero_3: PORTADA_DEL_HOME,
  home_hero_4: PORTADA_DEL_HOME,

  home_identidad_media: FOTO_DE_IDENTIDAD,

  home_ruta_1: TARJETA_DE_RUTA,
  home_ruta_2: TARJETA_DE_RUTA,
  home_ruta_3: TARJETA_DE_RUTA,
  home_ruta_4: TARJETA_DE_RUTA,
  home_ruta_5: TARJETA_DE_RUTA,
  home_ruta_6: TARJETA_DE_RUTA,
  home_ruta_7: TARJETA_DE_RUTA,
  home_ruta_8: TARJETA_DE_RUTA,

  home_banner_1: DIAPOSITIVA_DEL_BANNER,
  home_banner_2: DIAPOSITIVA_DEL_BANNER,
  home_banner_3: DIAPOSITIVA_DEL_BANNER,

  home_eco_1: TARJETA_DEL_ECOSISTEMA,
  home_eco_2: TARJETA_DEL_ECOSISTEMA,
  home_eco_3: TARJETA_DEL_ECOSISTEMA,
  home_eco_4: TARJETA_DEL_ECOSISTEMA,
  home_eco_5: TARJETA_DEL_ECOSISTEMA,
  home_eco_6: TARJETA_DEL_ECOSISTEMA,

  // --- Fuera del Home. Las veintitres entraron: hasta ese dia caian en
  // `PRESENTACION_NEUTRA` y el recortador ensenaba a todo color lo que el sitio pinta en gris al
  // 28 %. Todas medidas con el navegador, ninguna estimada.
  hero_sobre_pnmc: PORTADA_DE_PAGINA,
  hero_ejes: PORTADA_DE_PAGINA,
  hero_ecosistema: PORTADA_DE_PAGINA,

  about_hito_1: TARJETA_DE_HITO,
  about_hito_2: TARJETA_DE_HITO,
  about_hito_3: TARJETA_DE_HITO,
  about_hito_4: TARJETA_DE_HITO,
  about_hito_5: TARJETA_DE_HITO,

  about_norma_1: TARJETA_DE_NORMA,
  about_norma_2: TARJETA_DE_NORMA,
  about_norma_3: TARJETA_DE_NORMA,
  about_norma_4: TARJETA_DE_NORMA,
  about_norma_5: TARJETA_DE_NORMA,
  about_norma_6: TARJETA_DE_NORMA,

  eje_01_media: IMAGEN_DE_EJE,
  eje_02_media: IMAGEN_DE_EJE,
  eje_03_media: IMAGEN_DE_EJE,

  estrategia_celebra_media: PORTADA_DE_ESTRATEGIA,
  estrategia_territorios_media: PORTADA_DE_ESTRATEGIA,

  marca_pnmc_blanco: LOGOTIPO_SOBRE_MORADO,
  marca_gov_co: LOGOTIPO_SOBRE_MORADO,
  marca_gov_co_sello: LOGOTIPO_SOBRE_MORADO,
};

/** Lo que se usa cuando una ranura todavia no declara su tratamiento. */
export const PRESENTACION_NEUTRA: PresentacionDeLaRanura = {
  clase: 'fija',
  filtro: 'none',
  opacidad: 1,
  fondo: '#0f172a',
};

/**
 * QUE RANURAS ENSENA EL SITIO EN EL MISMO HUECO, ALTERNANDOSE.
 *
 * Se declara y no se deduce. Hasta `rotanCon` lo deducia comparando la
 * identidad del objeto de presentacion —`PRESENTACION_DE_LA_RANURA[a] === PRESENTACION_DE_LA_RANURA[b]`—
 * y eso daba la respuesta correcta solo por casualidad: las cuatro portadas comparten
 * `PORTADA_DEL_HOME` Y ADEMAS rotan, pero las ocho Rutas comparten `TARJETA_DE_RUTA` y NO rotan,
 * cada tarjeta tiene su foto. Subir la foto de la Ruta 3 la pintaba en las ocho tarjetas de la
 * previsualizacion: una imagen valida en siete sitios equivocados.
 *
 * El tratamiento dice COMO SE PINTA una ranura; esta lista dice CUANDO SE VE. Son dos preguntas
 * distintas y por eso son dos datos distintos.
 *
 * HAY UN SOLO GRUPO, y el banner no esta en el. Las tres diapositivas se turnan solas cada pocos
 * segundos, asi que la que se acaba de cambiar aparece sin espejar nada: espejarla taparia las
 * otras dos y haria imposible mirar el banner entero. La portada del Home si entra porque el
 * sorteo es UNA VEZ POR VISITA (home.component.ts): sin espejar, recargar el marco para ver la
 * portada 2 sale a cara o cruz.
 */
export const ROTAN_JUNTAS: readonly (readonly string[])[] = [
  ['home_hero_1', 'home_hero_2', 'home_hero_3', 'home_hero_4'],
];

/** Las claves que se alternan con esta en el mismo hueco. Vacio cuando la ranura no rota. */
export function ranurasQueRotanCon(clave: string): readonly string[] {
  return ROTAN_JUNTAS.find(grupo => grupo.includes(clave)) ?? [];
}

/**
 * QUE RANURAS DE IMAGEN SE EDITAN DENTRO DE QUE BLOQUE DEL PANEL DE TEXTOS.
 *
 * La clave es el id de un grupo de `registro-de-textos-web.ts`; el valor, las claves de imagen que
 * se pintan en ese mismo trozo de pagina. Es una tabla explicita y no una regla derivada del
 * nombre por el mismo motivo que `ENCUADRES`: adivinar produce parejas plausibles que ponen el
 * editor de una imagen debajo de unos textos que no la acompanan.
 *
 * LAS 34 RANURAS ESTAN AQUI, y las cuatro de marca tambien: tres no son editables y aun asi se
 * pintan, porque una persona que abre «Navegacion y Footer» necesita ver el escudo que hay puesto
 * para entender por que no se puede cambiar. El editor no ofrece el boton; la fila existe.
 *
 * SI UNA RANURA NO APARECE AQUI, no hay forma de editarla desde el estudio. Hay una prueba que
 * compara esta tabla contra `WEB_IMAGE_KEYS` y falla nombrando la clave que se quedo fuera.
 */
export const IMAGENES_DEL_BLOQUE: Record<string, string[]> = {
  // --- Home
  home_hero: ['home_hero_1', 'home_hero_2', 'home_hero_3', 'home_hero_4'],
  home_about: ['home_identidad_media'],
  home_strategies_cards: [
    'home_ruta_1', 'home_ruta_2', 'home_ruta_3', 'home_ruta_4',
    'home_ruta_5', 'home_ruta_6', 'home_ruta_7', 'home_ruta_8',
  ],
  home_banner: ['home_banner_1', 'home_banner_2', 'home_banner_3'],
  home_ecosistema: ['home_eco_1', 'home_eco_2', 'home_eco_3', 'home_eco_4', 'home_eco_5', 'home_eco_6'],

  // --- Sobre PNMC
  about_hero_presentation: ['hero_sobre_pnmc'],
  about_timeline: ['about_hito_1', 'about_hito_2', 'about_hito_3', 'about_hito_4', 'about_hito_5'],
  about_normative: [
    'about_norma_1', 'about_norma_2', 'about_norma_3',
    'about_norma_4', 'about_norma_5', 'about_norma_6',
  ],

  // --- Ejes. La portada va con los textos del encabezado; cada imagen de eje, con su eje.
  ejes_hero: ['hero_ejes'],
  eje1_details: ['eje_01_media'],
  eje2_details: ['eje_02_media'],
  eje3_details: ['eje_03_media'],

  // --- Estrategias
  strategy_celebra_details: ['estrategia_celebra_media'],
  strategy_territorios_details: ['estrategia_territorios_media'],

  // --- Ecosistema
  ecosistema_hero: ['hero_ecosistema'],

  // --- Navegacion y Footer. La clave es el id del grupo de TEXTO, no el del grupo de imagen:
  // `marca` es como se llama el grupo en `WEB_IMAGE_GROUPS`, y ponerlo aqui habria dejado las
  // cuatro ranuras sin bloque sin que nada fallara.
  general_nav_footer: ['marca_pnmc_blanco', 'marca_gov_co', 'marca_gov_co_sello'],
};
