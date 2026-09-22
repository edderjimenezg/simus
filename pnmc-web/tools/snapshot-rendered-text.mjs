/**
 * Instantánea del texto que un visitante ve de verdad.
 *
 * La conmutación de la Fase 3 cambió de dónde salen los textos del sitio. El
 * riesgo de un cambio así no es que falle ruidosamente —eso se nota—, sino que
 * una página quede con un texto distinto y nadie lo mire. Esta herramienta
 * convierte «nadie lo mire» en una comparación mecánica.
 *
 * Se ejecuta en dos modos:
 *
 *   node tools/snapshot-rendered-text.mjs            → escribe la línea base
 *   node tools/snapshot-rendered-text.mjs --check    → compara y falla si difiere
 *
 * El sitio es una SPA: `curl` solo devuelve el armazón vacío. Hay que renderizar
 * con un navegador de verdad, y por eso esto usa Chromium sin interfaz en vez de
 * una petición HTTP.
 *
 * SOBRE EL NAVEGADOR. Esto arrancaba Chrome del sistema por línea de órdenes con
 * `--dump-dom --virtual-time-budget=15000`. Dejó de funcionar en Chrome 151: la
 * orden se queda colgada indefinidamente y no vuelve nunca —no falla, no imprime
 * nada, se queda—, de modo que la puerta pasó de vigilar a bloquear. Y quitar el
 * presupuesto de tiempo virtual no es una salida: sin él Chrome vuelca el
 * armazón sin hidratar, unos 2,8 KB de HTML vacío, y la comparación diría que
 * todas las páginas son idénticas porque todas están igual de vacías. Un falso
 * verde es peor que un cuelgue.
 *
 * Ahora usa Playwright, que ya es dependencia del proyecto para las pruebas de
 * extremo a extremo y trae su propio Chromium anclado. Además abre UN navegador
 * para las catorce rutas en vez de un proceso por ruta.
 */
import { readFile, writeFile, mkdir } from 'node:fs/promises';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { chromium } from 'playwright-core';

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..');
const DESTINO = join(ROOT, 'tools/.snapshots/texto-renderizado.json');

const BASE = process.env.SNAPSHOT_BASE_URL ?? 'http://127.0.0.1:4300';
const COMPROBAR = process.argv.includes('--check');

/**
 * `--solo=/editorial,/agenda` regenera SOLO esas rutas y conserva el resto de la linea base.
 *
 * POR QUE HIZO FALTA. El 27 de agosto de 2026 `/editorial` cambio a proposito —la pagina paso
 * de mostrar albumes de galeria a mostrar el catalogo editorial de verdad, que hasta ese dia
 * estaba vacio— y habia que actualizar su referencia. Pero regenerar sin esta opcion reescribe
 * LAS QUINCE, y en ese momento otras cuatro rutas diferian solo por RECUENTOS DE DATOS DE
 * PRUEBA (Escuelas 30 a 31, Escenarios 30 a 31, Festivales 30 a 31, Luteria 30 a 31).
 * Tragarselos habria dejado la puerta atada a cuantos datos de prueba tenga la base ese dia,
 * que es exactamente lo contrario de lo que la puerta mide.
 *
 * Sin `--check` y sin `--solo`, el comportamiento es el de siempre: regenerar las quince.
 */
const SOLO = (() => {
  const arg = process.argv.find((a) => a.startsWith('--solo='));
  if (!arg) return null;
  const pedidas = arg.slice('--solo='.length).split(',').map((r) => r.trim()).filter(Boolean);
  return pedidas.length > 0 ? pedidas : null;
})();

/**
 * Las rutas con contenido gobernado por el CMS. No están todas las del sitio: se
 * listan las que el catálogo alimenta, que son las que el corte puede romper.
 *
 * `/registro` entró al conectarle el CMS. Es la que más vigilancia merece: dos
 * de sus textos son las frases de consentimiento, y un cambio accidental ahí no
 * es una errata sino un problema jurídico.
 */
const RUTAS = [
  '/',
  '/pnmc',
  '/ejes',
  '/noticias',
  '/agenda',
  '/editorial',
  '/galeria',
  '/mapa',
  '/registro',
  // Las dos estrategias entraron al conectar «Celebra la Música». Van juntas a
  // propósito: comparten componente, así que un cambio pensado para una puede
  // llevarse por delante la otra sin que nadie mire.
  '/estrategia/circulacion',
  '/estrategia/investigacion',
  // El ecosistema entró al darle pestaña propia en el panel. Es la sección con
  // más prosa institucional del portal, así que es también donde más caro sale
  // que un cambio pase inadvertido.
  //
  // Se llamaba `/simus` hasta que se aclaró que SIMUS es una plataforma externa
  // del Ministerio. Las rutas viejas siguen redirigiendo, pero la línea base se
  // toma sobre las canónicas: comparar la redirección mediría el destino dos
  // veces y dejaría el redirect sin vigilar de todos modos.
  '/ecosistema',
  '/ecosistema/escuelas',
  '/ecosistema/festivales',
  // El 404 entró al hacer editables sus cinco textos. Es la página cuyo texto
  // puede volverse falso sin que nadie la toque —cuando una sección se renombra
  // o se retira—, así que es justo la que más merece estar vigilada.
  '/esta-ruta-no-existe',
];

/**
 * Reduce el HTML a su texto visible.
 *
 * Se descartan `script`, `style` y `svg` porque su contenido no es texto que
 * alguien lea, y cambia por razones que no tienen que ver con el CMS.
 */
function textoVisible(html) {
  return html
    .replace(/<(script|style|noscript|svg)\b[^>]*>[\s\S]*?<\/\1>/gi, ' ')
    .replace(/<!--[\s\S]*?-->/g, ' ')
    .replace(/<[^>]+>/g, ' ')
    .replace(/&nbsp;/g, ' ')
    .replace(/&amp;/g, '&')
    .replace(/&lt;/g, '<')
    .replace(/&gt;/g, '>')
    .replace(/&quot;/g, '"')
    .replace(/&#(\d+);/g, (_, code) => String.fromCharCode(Number(code)))
    .replace(/\s+/g, ' ')
    .trim();
}

/**
 * Neutraliza lo que cambia solo con el paso del tiempo. Sin esto la comparación
 * fallaría por motivos que no son el corte, y una puerta que da falsas alarmas
 * se acaba ignorando, que es peor que no tenerla.
 *
 * Lo que NO se neutraliza son los datos: `/galeria` lista álbumes y `/mapa`
 * cuenta procesos, así que sembrar registros nuevos hace que estas dos rutas
 * salgan como cambiadas aunque no se haya tocado una sola cadena. Es ruido
 * conocido y se deja a la vista: filtrar cifras a ciegas escondería también un
 * cambio de copy con números dentro. Antes de regenerar la línea base, LEER el
 * diff —dice el carácter exacto— y comprobar si lo que cambió es texto o datos.
 */
function normalizar(texto) {
  return texto
    .replace(/\d{1,2}\s+de\s+[a-záéíóúñ]+\s+de\s+\d{4}/gi, '«fecha»')
    .replace(/\d{4}-\d{2}-\d{2}([T ]\d{2}:\d{2}(:\d{2})?)?/g, '«fecha»')
    .replace(/\d{1,2}\/\d{1,2}\/\d{2,4}/g, '«fecha»');
}

async function capturar(pagina, ruta) {
  const url = `${BASE}${ruta}`;

  // `networkidle` y no `load`: el armazón llega en milisegundos y las páginas
  // piden sus textos al API después. Esperar solo a `load` mediría el HTML
  // vacío, que es precisamente el falso verde que hay que evitar.
  await pagina.goto(url, { waitUntil: 'networkidle', timeout: 45_000 });

  const html = await pagina.content();
  const texto = normalizar(textoVisible(html));
  if (texto.length < 200) {
    throw new Error(
      `La ruta ${ruta} rindió solo ${texto.length} caracteres de texto. ` +
      '¿Está el sitio levantado en ' + BASE + '?',
    );
  }
  return texto;
}

console.log(`Sitio:  ${BASE}\n`);

const navegador = await chromium.launch();
const actual = {};
try {
  const pagina = await navegador.newPage({ viewport: { width: 1280, height: 900 } });
  for (const ruta of RUTAS) {
    process.stdout.write(`  ${ruta.padEnd(26)} `);
    actual[ruta] = await capturar(pagina, ruta);
    console.log(`${actual[ruta].length} caracteres`);
  }
} finally {
  // Sin esto un fallo a mitad del recorrido deja el navegador vivo y el proceso
  // no termina: la puerta se quedaría colgada en lugar de dar su veredicto.
  await navegador.close();
}

if (!COMPROBAR) {
  let rutas = actual;

  if (SOLO) {
    // SE PARTE DE LA LINEA BASE GUARDADA, no de cero: lo no pedido conserva TAL CUAL su
    // referencia anterior, aunque hoy rinda otra cosa. Escribir `actual` entero seria
    // regenerarlo todo con otro nombre.
    let previa;
    try {
      previa = JSON.parse(await readFile(DESTINO, 'utf8')).rutas ?? {};
    } catch {
      console.error('\nNo hay linea base: --solo necesita una para conservar el resto.');
      process.exit(2);
    }

    const desconocidas = SOLO.filter((r) => !(r in actual));
    if (desconocidas.length > 0) {
      console.error('\nEstas rutas no estan en la lista del guion: ' + desconocidas.join(', '));
      process.exit(2);
    }

    rutas = { ...previa };
    for (const ruta of SOLO) rutas[ruta] = actual[ruta];
    console.log('\nSe regeneran ' + SOLO.length + ' de ' + RUTAS.length + ': ' + SOLO.join(', '));
    console.log('Las otras ' + (RUTAS.length - SOLO.length) + ' conservan su referencia anterior.');
  }

  await mkdir(dirname(DESTINO), { recursive: true });
  await writeFile(DESTINO, `${JSON.stringify({ base: BASE, rutas }, null, 2)}\n`, 'utf8');
  console.log(`\nLínea base escrita: ${DESTINO}`);
  console.log('Ejecute con --check después del corte para comparar.');
  process.exit(0);
}

// --- Modo comparación ---
let baseGuardada;
try {
  baseGuardada = JSON.parse(await readFile(DESTINO, 'utf8'));
} catch {
  console.error(`\nNo hay línea base en ${DESTINO}. Ejecute la herramienta sin --check antes del corte.`);
  process.exit(2);
}

const diferencias = [];
for (const ruta of RUTAS) {
  const antes = baseGuardada.rutas?.[ruta];
  const ahora = actual[ruta];

  if (antes === undefined) {
    diferencias.push({ ruta, motivo: 'no estaba en la línea base' });
    continue;
  }
  if (antes === ahora) { continue; }

  // Se muestra el primer punto donde divergen, que es lo que permite ubicarlo.
  let i = 0;
  while (i < antes.length && i < ahora.length && antes[i] === ahora[i]) { i++; }
  diferencias.push({
    ruta,
    motivo: `difieren desde el carácter ${i}`,
    antes: antes.slice(Math.max(0, i - 60), i + 120),
    ahora: ahora.slice(Math.max(0, i - 60), i + 120),
  });
}

if (diferencias.length === 0) {
  console.log(`\n✓ Las ${RUTAS.length} rutas rinden el mismo texto que la línea base.`);
  process.exit(0);
}

console.error(`\n✗ ${diferencias.length} de ${RUTAS.length} rutas cambiaron:\n`);
for (const d of diferencias) {
  console.error(`  ${d.ruta} — ${d.motivo}`);
  if (d.antes !== undefined) {
    console.error(`      antes: …${d.antes}…`);
    console.error(`      ahora: …${d.ahora}…\n`);
  }
}
console.error('Si algún cambio es deliberado, revíselo y regenere la línea base sin --check.');
process.exit(1);
