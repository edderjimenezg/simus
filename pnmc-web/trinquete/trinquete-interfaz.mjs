#!/usr/bin/env node
/**
 * TRINQUETE DE DEUDA DE INTERFAZ
 * ==============================
 *
 * Hermano del trinquete de tipado (`trinquete-any.mjs`) y con sus mismas
 * reglas. Lo que vigila es distinto: la deuda de la CAPA DE PRESENTACION que la
 * auditoria de front del 21 ago 2026 midio y dejo cuantificada.
 *
 * POR QUE ES ESTATICO. El barrido de dispositivos (`playwright.audit.config.ts`)
 * mide mucho mejor —contraste real sobre el DOM, recorte, area tactil—, pero
 * necesita frontend, API y base de datos en pie. Eso no corre en una corrida de
 * CI limpia, asi que ese barrido es una herramienta LOCAL. Este trinquete se
 * queda con el subconjunto que se puede medir leyendo ficheros, que es el que
 * puede vigilar en cada cambio sin infraestructura. Son cosas distintas y no se
 * sustituyen: este atrapa la regresion barata; aquel encuentra el defecto.
 *
 * DOS SENTIDOS. A diferencia del trinquete de `any`, aqui hay metricas de las
 * dos clases:
 *   - "maximo": deuda. Solo puede BAJAR.  (botones sin type, colores literales)
 *   - "minimo": practica saldada. Solo puede SUBIR. (routerLink, role=dialog)
 * Una practica que se gana y luego se pierde en un refactor es exactamente el
 * caso que nadie mira, y es la razon de que existan los minimos.
 *
 * SE MIDE SOBRE EL FICHERO ENTERO, NO LINEA A LINEA. Las etiquetas de Angular
 * se parten en varias lineas constantemente:
 *
 *     <img
 *       [src]="foto"
 *       alt="..." />
 *
 * Un `grep -oE '<img[^>]*>'` no ve esa etiqueta y la deja fuera de la cuenta.
 * Este contador lee el fichero completo y aprovecha que en JavaScript una clase
 * negada `[^>]` SI cruza saltos de linea. Por eso las cifras de aqui son
 * MAYORES que las de un grep equivalente, y la cifra buena es esta.
 *
 * TAMBIEN LAS PLANTILLAS EN LINEA. Hasta solo se
 * leian ficheros `.html`; las plantillas escritas dentro de un `.ts` quedaban
 * fuera «porque eran pocas». Dejaron de serlo: las piezas compartidas de la
 * consola —boton, dato en lectura, estado de lista, cajon lateral, cabecera de
 * pagina— viven en linea, y al mover un `role="dialog"` o un `role="alert"` de
 * una plantilla .html a una de esas piezas el trinquete veia BAJAR la practica
 * cuando en realidad se habia centralizado. Se lee solo el literal de
 * `template:` con acento grave, que es lo unico que es plantilla; las demas
 * cadenas del .ts siguen fuera.
 *
 * FALLA CERRADO. Si falta el techo, si esta corrupto, o si el barrido no
 * encuentra NI UN fichero .html, sale 1. Una puerta que pasa porque no midio
 * nada es peor que no tener puerta, porque ademas tranquiliza.
 *
 * USO:
 *   node trinquete/trinquete-interfaz.mjs              verificar (lo que corre el CI)
 *   node trinquete/trinquete-interfaz.mjs --desglose   verificar + top de ficheros
 *   node trinquete/trinquete-interfaz.mjs --sellar     apretar tras pagar deuda
 *   node trinquete/trinquete-interfaz.mjs --json       salida para maquinas
 *   node trinquete/trinquete-interfaz.mjs --raiz DIR   apuntar a otra carpeta (pruebas)
 */

import fs from "node:fs";
import path from "node:path";
import { execFileSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const AQUI = path.dirname(fileURLToPath(import.meta.url));
const FICHERO_TECHO = path.join(AQUI, "techo-interfaz.json");
const RAIZ_POR_DEFECTO = path.resolve(AQUI, "..", "src");

/**
 * Las metricas. Cada una lleva el POR QUE, porque un numero sin motivo se
 * termina subiendo "para que pase el CI" y entonces no servia de nada.
 *
 *   clave    identificador estable; es la que viaja al fichero de techo
 *   sentido  "maximo" = deuda que solo baja | "minimo" = practica que solo sube
 *   patron   funcion que devuelve un RegExp nuevo (para no compartir lastIndex)
 *   filtro   opcional: de cada coincidencia, decide si cuenta
 */
export const METRICAS = [
  {
    clave: "botones_de_accion_sin_tipo",
    sentido: "maximo",
    titulo: "<button (click)> sin atributo type",
    porque:
      "Un <button> sin type vale type=submit. Dentro de un <form>, pulsar " +
      "'Limpiar' envia el formulario. Es un defecto de comportamiento, no de estilo.",
    // Se exige (click) A PROPOSITO, y no se cuenta todo <button> sin type.
    // Comprobado el 22 ago 2026: de los 14 <button> sin type, TRES son el boton de
    // envio legitimo de su formulario (external-access:198,
    // admin-festival-review-panel:15 y :16) y sus hermanos si llevan
    // type="button" explicito. O sea: la convencion del repositorio ya es
    // correcta. Un trinquete que exigiera cero empujaria a ponerles
    // type="button" y romperia los tres formularios.
    // La regla que si distingue: un boton que hace algo al pulsarlo es un boton
    // de accion y debe declararlo; el de envio no lleva (click), lleva el
    // (submit) del <form>.
    patron: () => /<button\b[^>]*>/g,
    // `[type]="…"` tambien declara el tipo: es como lo hace `app-boton`, que lo
    // recibe por entrada. Antes solo se aceptaba el atributo literal.
    filtro: (m) => !/\[?type\]?\s*=/.test(m) && /\(click\)/.test(m),
  },
  {
    clave: "imagenes_sin_dimension",
    sentido: "maximo",
    titulo: "<img> sin width declarado",
    porque:
      "Sin dimension el navegador no reserva el hueco: la pagina salta cuando " +
      "carga la imagen y el usuario pulsa lo que no queria. Es CLS medible.",
    patron: () => /<img\b[^>]*>/g,
    // Una anchura ligada —`[width]`, `[style.width.%]`— tambien reserva el
    // hueco: el recortador de imagenes la calcula a partir del recorte.
    filtro: (m) => !/\[?(?:style\.)?width(?:\.[a-z%]+)?\]?\s*=/.test(m),
  },
  {
    clave: "pildoras_de_estado",
    sentido: "maximo",
    titulo: "estados pintados como pildora de color en vez de con su indicador",
    porque:
      "Lo pidio la direccion de producto y para todo el " +
      "diseno: «no me gustan este tipo de pildoras en general». El motivo no es de " +
      "gusto: una pildora dice el estado SOLO con el color, asi que quien no " +
      "distingue esos colores no lo lee; el indicador lleva icono y texto. Ademas " +
      "cada pantalla elegia su propio verde, y el mismo estado se veia distinto " +
      "segun donde se mirara. Llego a cero y se sella " +
      "aqui para que no vuelva de una en una.",
    // LA FORMA QUE SE BUSCA es un `<span>` redondeado del todo cuyo CONTENIDO es un
    // estado: o una interpolacion que lo nombra, o una de las palabras del ciclo de
    // vida escrita a mano. No basta con buscar `rounded-full`: esa clase tambien pinta
    // avatares, contadores, pasos y etiquetas de contenido, que no son estados y no
    // sobran.
    //
    // LAS DOS FORMAS HACEN FALTA. La primera version solo miraba interpolaciones, y por
    // ahi se colaron «Resuelto» en gobernanza y «Cerrado» en categorias: la misma
    // pildora, escrita con el texto dentro.
    patron: () =>
      /<span[^>]*\brounded-full\b[^>]*>\s*(?:\{\{[^}]*?(?:status|estado|Estado|resultado)[^}]*?\}\}|(?:Activo|Inactivo|Activa|Inactiva|Pendiente|Aprobad\w*|Rechazad\w*|Publicad\w*|Borrador|Archivad\w*|Resuelto|Cerrado|Sin publicar|En el sitio|Vigente))\s*<\/span>/g,
  },
  {
    clave: "colores_literales",
    sentido: "maximo",
    titulo: "colores #RRGGBB escritos a mano en plantillas",
    porque:
      "Mientras el color viva literal en la plantilla, cambiar el morado de " +
      "marca exige tocar cientos de sitios y el contraste no se puede auditar " +
      "sin renderizar. Baja segun avance la migracion a los tokens de @theme.",
    // El corte se hace con lookahead de digito hexadecimal, NO con \b. Se probo
    // con \b y perdia cinco colores reales por dos motivos distintos:
    //   · `#00da5e80` (verde con alfa): \b no separa `e` de `8`, asi que la
    //     alternativa de 6 fallaba y la de 8 no existia.
    //   · `#291242_55%` dentro de un degradado arbitrario de Tailwind: el `_`
    //     es un caracter de palabra, asi que tampoco habia frontera.
    // Se prueba la forma de 8 antes que la de 6: al reves, `#00da5e80` contaria
    // como el color `#00da5e` y se perderia el canal alfa.
    patron: () => /#[0-9A-Fa-f]{8}(?![0-9A-Fa-f])|#[0-9A-Fa-f]{6}(?![0-9A-Fa-f])/g,
  },
  {
    clave: "formatos_de_fecha_sueltos",
    sentido: "maximo",
    titulo: "fechas del panel administrativo fuera de los dos formatos acordados",
    porque:
      "El 13 de septiembre de 2026 convivian DIEZ formas distintas de escribir " +
      "una fecha dentro del Espacio de Gestion Administrativa, tres de ellas en " +
      "pantallas contiguas: «13 sept 2026, 16:29» en Festivales, «13/09/26, " +
      "5:16 p. m.» en Auditoria y, en la bandeja de Solicitudes, la marca cruda " +
      "del servidor «2026-09-12T21:43:17». Quien revisa compara fechas ENTRE " +
      "pantallas —cuando se envio, cuando se decidio— y con cuatro formatos esa " +
      "comparacion deja de ser inmediata. Quedan dos, en " +
      "`features/admin/domain/formatos-de-fecha.ts`, mas la hora suelta del " +
      "autoguardado, que se refiere a algo que acaba de pasar.",
    // LA RUTA QUE LLEGA ES RELATIVA A `src`, asi que empieza en `app/`. Escribirla con
    // `^src/` no casa nunca y la metrica quedaria muda: contaria 0 siempre y pareceria
    // saldada. Comprobado poniendo a proposito un formato suelto y viendo que sube a 1.
    soloEn: /^app\/features\/admin\//,
    patron: () => /\|\s*date\s*:\s*'[^']*'/g,
    filtro: (m) => !/'(?:d MMM y, HH:mm|d MMM y|HH:mm)'$/.test(m.trim()),
  },
  {
    clave: "tamanos_fuera_de_escala",
    sentido: "maximo",
    titulo: "tamanos de letra arbitrarios en plantillas del panel administrativo",
    porque:
      "La escala tipografica del proyecto tiene cinco peldanos y su suelo esta " +
      "en 12 px (`--text-dato`). El censo encontro " +
      "316 tamanos escritos a mano con la sintaxis arbitraria de Tailwind, casi " +
      "todos por DEBAJO de ese suelo: en el Catalogo Editorial se midieron " +
      "etiquetas a 9,6 px. Lo que esta por debajo del suelo no se lee sin " +
      "acercarse, y cada valor suelto -0,55; 0,56; 0,58; 0,6; 0,62; 0,64; " +
      "0,65; 0,66; 0,68; 0,7; 0,72 rem- es un peldano mas de una escala que " +
      "entonces ya no es una escala. Baja segun cada grupo de secciones adopta " +
      "los tokens; el grupo de Publicaciones lo salda entero.",
    // EL ESPACIO DE LA ORGANIZACION ENTRA EL 17 DE SEPTIEMBRE DE 2026. La metrica solo miraba la
    // consola, asi que el panel externo pudo acumular 35 tamanos arbitrarios sin que nada avisara:
    // los rotulos de la ficha de un mercado a 9,92 px, las cabeceras de la tabla de eventos a 10,88
    // y —lo peor— la pantalla de autorizaciones de datos personales con cinco textos entre 10 y 11
    // px, incluido un boton. Saldado a cero y dentro del ambito, para que no vuelva a pasar.
    // EL PORTAL SIGUE FUERA: es diseno aprobado, y esa exencion esta declarada en la v07 del
    // lenguaje visual.
    soloEn: /^app\/(?:features\/admin|features\/panel-organizacion|shared\/components\/banco-de-archivos)\//,
    patron: () => /text-\[(?:0\.\d+|\d+(?:\.\d+)?)(?:rem|px)\]/g,
  },
  {
    clave: "enlaces_de_router",
    sentido: "minimo",
    titulo: "usos de routerLink",
    porque:
      "La navegacion se hace con <button (click)>, asi que no se abre en " +
      "pestana nueva, no se copia la direccion y un lector de pantalla anuncia " +
      "'boton' donde hay un enlace. Este minimo sube segun avance esa migracion " +
      "y evita que un refactor la deshaga sin que nadie lo note.",
    patron: () => /\brouterLink\b/g,
  },
  {
    clave: "dialogos_con_rol",
    sentido: "minimo",
    titulo: 'contenedores con role="dialog"',
    porque:
      "Los modales del panel no se anuncian como dialogo, no atrapan el foco y " +
      "no cierran con Escape. La directiva focus-trap ya existe y se usa una " +
      "sola vez. Este minimo sube conforme se adopten.",
    // CADA USO DE UNA CASCARA COMPARTIDA CUENTA COMO UN DIALOGO CON ROL, por la misma razon que en
    // `anuncios_accesibles`: el cajon lateral, el historial y la previsualizacion llevan el rol
    // dentro de su plantilla y se usan en muchas pantallas. Sin esto, pasar el historial del
    // registro a `app-panel-lateral` hacia BAJAR la metrica mientras
    // la practica se centralizaba.
    // Y LAS TRES PIEZAS DE VENTANA CUENTAN IGUAL. El 17 de septiembre de 2026, llevar las
    // confirmaciones escritas a mano de Mercados (dos), Festivales y Ediciones de mercado a
    // `app-confirmacion` dejo la metrica EN ROJO —de 60 a 57—; el mismo dia, llevar los siete
    // formularios de Categorias, Organizaciones y la ficha de un Festival a
    // `app-dialogo-de-formulario` la dejo otra vez en rojo —de 69 a 63—, mientras todos ganaban
    // foco atrapado, Escape y el mismo lenguaje. El rol sigue ahi, una capa mas adentro. Es el
    // mismo fallo que este comentario ya describe para el cajon lateral.
    patron: () => /role\s*=\s*"dialog"|<app-panel-lateral\b|<app-historial-de-registro\b|<app-dialogo-de-previsualizacion\b|<app-confirmacion\b|<app-dialogo-de-formulario\b|<app-asistente-de-alta\b/g,
  },
  {
    clave: "anuncios_accesibles",
    sentido: "minimo",
    titulo: 'regiones aria-live y role="alert"',
    porque:
      "Un cambio que no se anuncia no existe para quien usa lector de pantalla: " +
      "ni el error de validacion, ni el resultado de un filtro, ni el cambio de " +
      "ruta. Se cuentan juntos porque ambos resuelven lo mismo.",
    // EL ROL TAMBIEN CUENTA CUANDO VA LIGADO. `app-estado-de-lista` decide entre «alert» y
    // «status» segun el tipo, asi que escribe `[attr.role]` y no `role="alert"`. Sin esta
    // alternativa, adoptar la pieza compartida HACIA BAJAR la metrica: el 15 de septiembre de
    // 2026, al llevar tres bloques de error de Auditoria, Analisis y Paginas y bloques a la pieza
    // comun, la cifra cayo de 93 a 90 mientras la practica se centralizaba. Es el mismo arreglo
    // que ya se le hizo a `dialogos_con_rol` al leer plantillas en linea.
    // Se cuentan las cuatro formas: el atributo suelto, el ligado, y el rol tanto escrito como
    // ligado. `[attr.aria-live]` no casaba con `\baria-live\s*=` porque entre el nombre y el `=`
    // va un `]`, asi que la region viva de `app-estado-de-lista` era invisible para la metrica.
    // Y CADA USO DE LA PIEZA COMPARTIDA CUENTA COMO UNA REGION. `app-estado-de-lista` anuncia
    // siempre -«alert» si es un error, «status» si no, y una region viva mientras carga-, asi que
    // cada sitio donde se pone es un sitio que anuncia. Sin esto, la metrica CASTIGABA adoptar la
    // pieza comun: tres bloques de error escritos a mano contaban tres y la pieza que los sustituye
    // contaba uno, aunque la practica llegue ahora a mas pantallas y sea consistente.
    // Y LO MISMO CON `app-lista-sin-filas`, que envuelve a `app-estado-de-lista` y por tanto
    // anuncia igual. El 16 de septiembre de 2026, llevar los vacios de Auditoria y Boletin a la
    // pieza nueva puso la metrica EN ROJO —de 156 a 154— mientras el anuncio seguia ahi, solo que
    // una capa mas adentro. Es exactamente el fallo que este comentario ya describia para la pieza
    // de dentro: una metrica estatica tiene que conocer las piezas que cuentan por ella.
    // Y LO MISMO CON LAS TRES PIEZAS DE VENTANA: `app-confirmacion`, `app-dialogo-de-formulario` y
    // `app-asistente-de-alta` enseñan la negativa del servidor con `role="alert"` dentro de su
    // plantilla. El 17 de septiembre de 2026, llevar el alta de una organización a la pieza comun
    // puso la metrica EN ROJO —de 167 a 166— mientras el anuncio seguia ahi, una capa mas adentro.
    patron: () => /\baria-live\s*=|\[attr\.aria-live\]|role\s*=\s*"alert"|\[attr\.role\][^>]*'alert'|<app-estado-de-lista\b|<app-lista-sin-filas\b|<app-confirmacion\b|<app-dialogo-de-formulario\b|<app-asistente-de-alta\b/g,
  },
  {
    clave: "ventanas_sin_directiva",
    sentido: "maximo",
    titulo: 'ventanas con role="dialog" sin la directiva appDialogo',
    porque:
      "Una ventana compuesta a mano sin la directiva no cierra con Escape, no retiene el foco " +
      "y no lo devuelve al cerrar: habia nueve asi en la consola, " +
      "tres de ellas en Categorias, y el historial de registro no cerraba con Escape mientras " +
      "la previsualizacion, a su lado, si.",
    // SOLO LAS MODALES, y esto se corrigio. La metrica contaba toda
    // etiqueta con `role="dialog"`, y dos de las cuatro que le quedaban NO son ventanas: la tarjeta
    // de un municipio en el geovisor y el globo de ayuda de una practica musical son dialogos NO
    // MODALES —sin `aria-modal`, con el fondo vivo detras— y ponerles la directiva seria un
    // defecto, no un arreglo: encerraria el teclado en una tarjeta de 232 px sin salida. Los dos
    // atienden Escape por su cuenta y se cierran al pulsar fuera. Lo que la directiva resuelve
    // —atrapar el foco, devolverlo, bloquear el fondo— solo tiene sentido cuando la ventana
    // AFIRMA que lo de detras esta inerte, y eso es exactamente lo que dice `aria-modal="true"`.
    // La etiqueta entera, con sus atributos partidos en varias lineas; se descartan las que ya
    // llevan la directiva. `[^>]*` no cruza el cierre de la etiqueta, asi que el `appDialogo` de
    // otra ventana no cuenta para esta.
    patron: () =>
      /<[a-z][a-z0-9-]*\b(?=(?:(?!appDialogo)[^>])*\brole\s*=\s*"dialog")(?=(?:[^>])*\baria-modal\s*=\s*"true")(?:(?!appDialogo)[^>])*>/g,
  },
  {
    clave: "campos_sin_rotulo",
    sentido: "maximo",
    titulo: "campos de formulario sin nombre accesible",
    porque:
      "Un campo cuyo unico rotulo es su `placeholder` no tiene nombre para un lector de pantalla, " +
      "y el placeholder desaparece en cuanto se escribe: tambien se pierde para quien ve. El 17 de " +
      "septiembre de 2026 habia trece asi —el festival de un mercado, tres de importacion asistida " +
      "y nueve de los buscadores del portal—. Cuentan como rotulo: un `<label for>` que apunte al " +
      "`id`, un `[for]`/`[id]` ligados, envolver el campo en el `<label>`, o un `aria-label`.",
    contar: (texto) => {
      const literales = new Set(
        [...texto.matchAll(/<label[^>]*\bfor="([^"]+)"/g)].map((m) => m[1]),
      );
      // `[for]` y `[attr.for]` son la misma cosa escrita de dos maneras, y las piezas compartidas
      // usan la segunda: `buscador-de-lista` y `dato-de-la-ficha` rotulan asi. Sin esta forma, la
      // metrica acusaba de no tener rotulo justo a las piezas que lo hacen bien.
      const ligados = new Set(
        [...texto.matchAll(/<label[^>]*\[(?:attr\.)?for\]="([^"]+)"/g)].map((m) => m[1]),
      );
      let n = 0;
      // EL `>` PUEDE IR DENTRO DE UN ATRIBUTO. `[class.x]="valor().length > 0"` es corriente en
      // Angular, y con `[^>]*` la etiqueta se cortaba ahi: el buscador compartido parecia no tener
      // rotulo porque su `[attr.id]` quedaba fuera del trozo leido. Se consumen las comillas
      // enteras para que solo cierre la etiqueta el `>` que esta de verdad fuera de un valor.
      for (const m of texto.matchAll(/<(input|select|textarea)\b((?:"[^"]*"|'[^']*'|[^>"'])*)>/g)) {
        const atributos = m[2];
        const tipo = /type="([^"]+)"/.exec(atributos);
        if (tipo && ["hidden", "submit", "button"].includes(tipo[1])) continue;
        if (/aria-label/.test(atributos)) continue;
        const idLiteral = /\bid="([^"]+)"/.exec(atributos);
        if (idLiteral && literales.has(idLiteral[1])) continue;
        const idLigado = /\[(?:attr\.)?id\]="([^"]+)"/.exec(atributos);
        if (idLigado && ligados.has(idLigado[1])) continue;
        // EL ROTULO PUEDE VIVIR EN QUIEN EMBEBE LA PIEZA. `app-selector-de-categoria` recibe su
        // `id` por entrada y las tres pantallas que lo usan pintan su propio `<label for>`, cada
        // una con la tipografia de su modulo; meterlo dentro cambiaria tres disenos aprobados.
        // Un lector de ficheros no puede ver ese rotulo, asi que la pieza lo DECLARA con este
        // atributo. Es una afirmacion explicita y revisable, no una excepcion escondida en el techo.
        if (/data-rotulo-externo/.test(atributos)) continue;
        // Envuelto por un `<label>` todavia abierto: el rotulo es el propio contenedor.
        const antes = texto.slice(0, m.index);
        if (antes.lastIndexOf("<label") > antes.lastIndexOf("</label>")) continue;
        n++;
      }
      return n;
    },
  },
  {
    clave: "rotulos_de_campo_a_mano",
    sentido: "maximo",
    titulo: "rotulos de campo con su tipografia escrita a mano",
    porque:
      "El 17 de septiembre de 2026 se contaron 199 rotulos de campo SOLO en la consola, con 46 " +
      "firmas de clases distintas, y dentro de un mismo formulario convivian versalitas negras en " +
      "gris, minusculas en negrita oscura y el tratamiento del rotulo de GRUPO usado para un campo. " +
      "La forma comun es `.campo__rotulo`; esta metrica cuenta los que todavia la escriben aparte, " +
      "y los que quedan son diseno aprobado (Catalogo Editorial, Agenda, Noticias), la pantalla de " +
      "entrada y el portal.",
    // SOLO LOS QUE SE DIBUJAN EN VERSALITAS. Un rotulo en minusculas es otra familia —el espacio
    // externo entero esta escrito asi— y convertirla es una decision de diseno, no una limpieza.
    patron: () =>
      /<label\b(?=(?:"[^"]*"|'[^']*'|[^>"'])*\bclass="[^"]*\buppercase\b)(?!(?:"[^"]*"|'[^']*'|[^>"'])*campo__rotulo)(?:"[^"]*"|'[^']*'|[^>"'])*>/g,
  },
];

/**
 * Quita los comentarios HTML antes de contar.
 *
 * POR QUE. Este repositorio comenta el codigo a conciencia, y los comentarios
 * hablan de lo que el codigo hace: citan clases, colores y atributos. Sin este
 * filtro el trinquete cuenta esas menciones como si fueran codigo.
 *
 * Comprobado el 22 ago 2026, el dano real de no filtrarlas:
 *   · 30 de las 1578 ocurrencias de `colores_literales` eran hexadecimales
 *     citados en prosa.
 *   · Y el caso grave: de los 7 `anuncios_accesibles` que sostenian el MINIMO,
 *     uno era la cadena `role="alert"` escrita dentro del comentario de
 *     admin-shell-page.component.html:20, que explica por que se anade el
 *     atributo. O sea que el suelo se apoyaba en 6 anuncios reales y una
 *     mencion. Borrar ese comentario —una edicion inofensiva— habria puesto el
 *     CI en rojo sin que nadie entendiera por que.
 *
 * En un maximo, contar de mas es solo ruido. En un minimo, contar de mas es una
 * puerta que miente en la direccion peligrosa: dice que la practica esta mas
 * adoptada de lo que esta.
 *
 * Se sustituye por cadena vacia y no por espacios porque aqui solo se cuenta;
 * no se informa de numeros de linea.
 */
export function sinComentarios(texto) {
  return texto.replace(/<!--[\s\S]*?-->/g, "");
}

/** Lee el fichero ENTERO a proposito: ver la cabecera. */
export function contarEnTexto(textoCrudo, metrica, rutaRelativa = "") {
  // UNA METRICA PUEDE VALER SOLO EN UNA ZONA. `soloEn` existe porque no toda regla
  // es del proyecto entero: el formato de fecha del Espacio de Gestion Administrativa
  // es una decision de ESE panel, y el portal publico tiene sus propias formas de
  // escribir una fecha —«sabado 4 de octubre»— que ahi son correctas.
  if (metrica.soloEn && !metrica.soloEn.test(rutaRelativa)) return 0;
  const texto = sinComentarios(textoCrudo);
  // ALGUNAS METRICAS NECESITAN EL FICHERO ENTERO, no una etiqueta suelta. Saber si un campo tiene
  // rotulo exige mirar TODOS los `<label for>` de la plantilla, y eso no cabe en una expresion
  // regular sobre la propia etiqueta. Quien lo necesite declara `contar`; el resto sigue con
  // `patron`, que es lo normal.
  if (metrica.contar) return metrica.contar(texto, rutaRelativa);
  const re = metrica.patron();
  let n = 0;
  let m;
  while ((m = re.exec(texto)) !== null) {
    if (m[0].length === 0) re.lastIndex++; // guardia anti-bucle
    if (!metrica.filtro || metrica.filtro(m[0])) n++;
  }
  return n;
}

const IGNORAR = new Set(["node_modules", "dist", ".angular", ".git", "coverage"]);

export function plantillas(raiz) {
  const salida = [];
  const pila = [raiz];
  while (pila.length) {
    const dir = pila.pop();
    let entradas;
    try {
      entradas = fs.readdirSync(dir, { withFileTypes: true });
    } catch {
      continue;
    }
    for (const e of entradas) {
      if (e.isDirectory()) {
        if (!IGNORAR.has(e.name)) pila.push(path.join(dir, e.name));
      } else if (e.isFile() && (e.name.endsWith(".html") || (e.name.endsWith(".ts") && !e.name.endsWith(".spec.ts")))) {
        salida.push(path.join(dir, e.name));
      }
    }
  }
  return salida.sort();
}

/**
 * La plantilla de un componente escrito en linea, o `null` si el fichero no tiene.
 *
 * Solo el literal que sigue a `template:` y va entre acentos graves. Las plantillas del
 * proyecto no usan acentos graves dentro —un acento grave dentro de una plantilla en linea
 * termina el literal y rompe la compilacion—, asi que el primero que cierra es el que cierra.
 */
export function plantillaEnLinea(fuente) {
  const m = /template:\s*`([^`]*)`/.exec(fuente);
  return m ? m[1] : null;
}

export function medir(raiz) {
  const ficheros = plantillas(raiz);
  const totales = Object.fromEntries(METRICAS.map((m) => [m.clave, 0]));
  const porFichero = Object.fromEntries(METRICAS.map((m) => [m.clave, []]));

  for (const f of ficheros) {
    const texto = f.endsWith(".ts") ? plantillaEnLinea(fs.readFileSync(f, "utf8")) : fs.readFileSync(f, "utf8");
    if (texto === null) continue;
    const relativa = path.relative(raiz, f).replace(/\\/g, "/");
    for (const m of METRICAS) {
      const n = contarEnTexto(texto, m, relativa);
      if (n > 0) {
        totales[m.clave] += n;
        porFichero[m.clave].push({ fichero: path.relative(raiz, f).replace(/\\/g, "/"), n });
      }
    }
  }
  for (const k of Object.keys(porFichero)) porFichero[k].sort((a, b) => b.n - a.n);
  return { ficheros: ficheros.filter((f) => f.endsWith('.html') || plantillaEnLinea(fs.readFileSync(f, 'utf8')) !== null).length, totales, porFichero };
}

/**
 * Contra que estado del repositorio se esta midiendo.
 *
 * Falla en silencio y devuelve `git_disponible: false` si no hay git, si la
 * raiz no esta dentro de un repositorio, o si el comando peta. NO debe romper el
 * trinquete: las pruebas lo ejecutan sobre directorios temporales que no son
 * repositorios, y una puerta que se cae porque no encuentra git seria peor que
 * una que no sabe el commit.
 */
export function estadoDelArbol(raiz) {
  const git = (...args) =>
    // Conserva el espacio inicial del formato porcelain: forma parte de los
    // dos indicadores de estado. Recortar ambos extremos mutilaba el primer
    // carácter de la primera ruta cuando el cambio estaba solo en el índice de
    // trabajo (` M ruta`).
    execFileSync("git", ["-C", raiz, ...args], { encoding: "utf8", stdio: ["ignore", "pipe", "ignore"] }).trimEnd();
  try {
    const commit = git("rev-parse", "--short", "HEAD");
    const sucios = git("status", "--porcelain", "--", ".")
      .split("\n")
      .map((l) => l.slice(3).trim())
      .filter(Boolean);
    const plantillasSucias = sucios.filter((f) => f.endsWith(".html") || (f.endsWith(".ts") && !f.endsWith(".spec.ts")));
    return {
      git_disponible: true,
      commit_base: commit,
      rama: git("rev-parse", "--abbrev-ref", "HEAD"),
      arbol_limpio: sucios.length === 0,
      entradas_sucias_en_el_ambito: sucios.length,
      // Solo las plantillas: son las unicas que mueven estas metricas. Si la
      // lista no esta vacia, la cifra sellada incluye trabajo sin confirmar y
      // puede que no sea de quien sella.
      plantillas_sin_confirmar_al_sellar: plantillasSucias.slice(0, 40),
    };
  } catch {
    return { git_disponible: false };
  }
}

// ------------------------------------------------------------------ ejecucion

function salirCon(codigo, mensaje) {
  if (mensaje) console.error(mensaje);
  process.exit(codigo);
}

const args = process.argv.slice(2);
const bandera = (n) => args.includes(n);
const valor = (n) => {
  const i = args.indexOf(n);
  return i >= 0 ? args[i + 1] : undefined;
};

const raiz = path.resolve(valor("--raiz") ?? RAIZ_POR_DEFECTO);
const ficheroTecho = valor("--techo") ?? FICHERO_TECHO;

let techo;
try {
  techo = JSON.parse(fs.readFileSync(ficheroTecho, "utf8"));
} catch (e) {
  salirCon(
    1,
    `TRINQUETE DE INTERFAZ: no se pudo leer el techo (${ficheroTecho}). ${e.message}\n` +
      "Falla cerrado a proposito: sin techo no hay puerta.",
  );
}
if (!techo || typeof techo.metricas !== "object" || techo.metricas === null) {
  salirCon(1, `TRINQUETE DE INTERFAZ: el techo (${ficheroTecho}) no tiene un objeto "metricas". Falla cerrado.`);
}

const faltantes = METRICAS.filter((m) => typeof techo.metricas[m.clave] !== "number");
if (faltantes.length) {
  salirCon(
    1,
    `TRINQUETE DE INTERFAZ: el techo no declara ${faltantes.map((m) => m.clave).join(", ")}.\n` +
      "Falla cerrado: una metrica sin techo es una metrica sin vigilancia.",
  );
}

const { ficheros, totales, porFichero } = medir(raiz);

if (ficheros === 0) {
  salirCon(
    1,
    `TRINQUETE DE INTERFAZ: no se encontro NI UNA plantilla bajo ${raiz}.\n` +
      "Falla cerrado: una puerta que pasa porque no midio nada es peor que no tener puerta.",
  );
}

const filas = METRICAS.map((m) => {
  const valorActual = totales[m.clave];
  const limite = techo.metricas[m.clave];
  const peor = m.sentido === "maximo" ? valorActual > limite : valorActual < limite;
  const mejor = m.sentido === "maximo" ? valorActual < limite : valorActual > limite;
  return { m, valor: valorActual, limite, peor, mejor };
});

const rotas = filas.filter((f) => f.peor);
const holgadas = filas.filter((f) => f.mejor);

if (bandera("--json")) {
  console.log(
    JSON.stringify(
      {
        ficheros,
        filas: filas.map((f) => ({
          clave: f.m.clave,
          sentido: f.m.sentido,
          valor: f.valor,
          techo: f.limite,
          peor: f.peor,
          mejor: f.mejor,
        })),
      },
      null,
      2,
    ),
  );
} else {
  const destino = path.relative(process.cwd(), raiz) || raiz;
  console.log(`\nTRINQUETE DE INTERFAZ — ${ficheros} plantillas (.html y en linea) bajo ${destino}\n`);
  const ancho = Math.max(...METRICAS.map((m) => m.clave.length));
  for (const f of filas) {
    const flecha = f.m.sentido === "maximo" ? "<=" : ">=";
    const estado = f.peor ? "ROJO   " : f.mejor ? "holgura" : "ok     ";
    console.log(`  ${estado}  ${f.m.clave.padEnd(ancho)}  ${String(f.valor).padStart(5)}  ${flecha} ${f.limite}`);
  }
  console.log("");
}

if (bandera("--desglose")) {
  for (const f of filas) {
    if (!porFichero[f.m.clave].length) continue;
    console.log(`  ${f.m.clave} — mayores:`);
    for (const x of porFichero[f.m.clave].slice(0, 5)) {
      console.log(`      ${String(x.n).padStart(4)}  ${x.fichero}`);
    }
    console.log("");
  }
}

if (bandera("--sellar")) {
  if (rotas.length) {
    salirCon(
      1,
      `TRINQUETE DE INTERFAZ: --sellar se NIEGA. ${rotas.map((f) => f.m.clave).join(", ")} esta peor que su techo.\n` +
        "El trinquete gira en un solo sentido: aprieta, nunca afloja. Para aflojar hay que\n" +
        "editar el fichero a mano, y eso se ve en la revision.",
    );
  }
  if (!holgadas.length) {
    console.log("Nada que sellar: ninguna metrica ha mejorado respecto a su techo.");
    process.exit(0);
  }
  const ahora = new Date().toISOString();
  const cambios = holgadas.map((f) => ({ clave: f.m.clave, de: f.limite, a: f.valor }));
  for (const f of holgadas) techo.metricas[f.m.clave] = f.valor;
  // Contra QUE estado se tomo la medida. Sin esto, un techo sellado sobre un
  // arbol con trabajo ajeno a medio vuelo pone en rojo a quien no ha hecho nada
  // malo, y el rojo no trae forma de averiguar por que.
  //
  // Paso el 22 ago 2026 con el trinquete hermano de tipado: se sello a las 17:16
  // contra un arbol que varias sesiones estaban editando, y una hora despues el
  // CI estaba en rojo por tres `any` de una ola que nadie relacionaba con aquel
  // sello. Con el commit y la lista de ficheros sucios escritos aqui, la
  // pregunta «esta cifra de que momento es» tiene respuesta.
  Object.assign(techo, estadoDelArbol(raiz));
  techo.medido_en = ahora;
  // Se BORRA en vez de recalcularse. El sello inicial lo escribio a mano con la
  // hora del equipo; si aqui solo se actualizara `medido_en`, el fichero se
  // quedaria con dos fechas que no coinciden y la de abajo mintiendo. Node no
  // sabe la etiqueta de zona horaria que uso quien lo escribio, asi que la
  // alternativa honesta a recalcularla mal es no tenerla: `medido_en` es UTC y
  // no admite ambiguedad.
  delete techo.medido_en_local;
  techo.ficheros_escaneados = ficheros;
  techo.historial = techo.historial ?? [];
  techo.historial.push({ fecha: ahora, via: "--sellar", cambios });
  fs.writeFileSync(ficheroTecho, JSON.stringify(techo, null, 2) + "\n", "utf8");
  console.log(`Techo apretado: ${cambios.map((c) => `${c.clave} ${c.de} -> ${c.a}`).join(", ")}`);
  process.exit(0);
}

if (rotas.length) {
  console.error("TRINQUETE DE INTERFAZ EN ROJO\n");
  for (const f of rotas) {
    const dir =
      f.m.sentido === "maximo"
        ? `subio a ${f.valor}, el techo es ${f.limite}`
        : `bajo a ${f.valor}, el minimo es ${f.limite}`;
    console.error(`  ${f.m.clave}: ${dir}`);
    console.error(`     ${f.m.titulo}`);
    console.error(`     ${f.m.porque}\n`);
  }
  console.error("Si el cambio es correcto y la cifra debe moverse en el sentido malo, hay que");
  console.error("editar techo-interfaz.json a mano y justificarlo en la revision. La herramienta");
  console.error("no lo hace por ti a proposito.");
  process.exit(1);
}

if (holgadas.length && !bandera("--json")) {
  // El mensaje util no es «hay holgura»: es CUANTA y DESDE CUANDO. «Holgura de
  // 44 respecto a un techo sellado hace 6 horas» le dice al siguiente que
  // alguien limpio y no cerro. «Hay holgura» se lee como ruido y se ignora.
  const detalle = holgadas
    .map((f) => `${f.m.clave} ${f.limite}->${f.valor} (${Math.abs(f.valor - f.limite)} de margen)`)
    .join(", ");
  let antiguedad = "";
  if (techo.medido_en) {
    const horas = (Date.now() - Date.parse(techo.medido_en)) / 36e5;
    if (Number.isFinite(horas) && horas >= 0) {
      antiguedad =
        horas < 1
          ? `, sellado hace ${Math.round(horas * 60)} min`
          : horas < 48
            ? `, sellado hace ${Math.round(horas)} h`
            : `, sellado hace ${Math.round(horas / 24)} dias`;
    }
  }
  console.log(`Hay holgura: ${detalle}${antiguedad}.`);
  console.log("Ejecuta --sellar para apretar el techo; si no, ese margen se lo gasta el siguiente cambio.\n");

  // En CI se emite ademas como anotacion, para que salga en el RESUMEN de la
  // corrida y no enterrado entre cien lineas de log.
  //
  // POR QUE. Un trinquete solo muerde cuando la cifra SUPERA el techo, asi que
  // una mejora sin sellar no rompe nada: se queda como margen gratis para el
  // siguiente cambio, y nadie se entera de que existe. Paso el 22 ago 2026 en
  // el trinquete hermano de tipado: se pagaron 15 ocurrencias de `any` y, de no
  // haberse sellado a mano ese mismo dia, habrian quedado 15 de holgura
  // invisible. La deuda que se paga y no se sella se vuelve a gastar.
  if (process.env.GITHUB_ACTIONS) {
    console.log(`::notice title=Trinquete de interfaz con holgura::${detalle}${antiguedad}. Ejecuta 'node trinquete/trinquete-interfaz.mjs --sellar' y confirma el techo, o ese margen se lo gasta el siguiente cambio.`);
  }
}
process.exit(0);
