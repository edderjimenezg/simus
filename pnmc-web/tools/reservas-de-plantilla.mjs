/**
 * Que publicar un texto en blanco deje la página en blanco.
 *
 *   npm run cms:reservas
 *
 * Suena obvio y no lo es. `WebTextsService.getWebText` resuelve con `??`, de
 * modo que una cadena vacía publicada gana sobre el texto de fábrica: fue una
 * corrección deliberada, porque con `||` el panel confirmaba un cambio que la
 * página ignoraba.
 *
 * Pero hasta agosto de 2026 **cada página del sitio lo deshacía en el último
 * tramo**. Once componentes envolvían la llamada así:
 *
 *     getWebText(key, fallback) { return this.webTexts.getWebText(key) || fallback; }
 *
 * y las plantillas pasaban un texto de reserva escrito a mano. Vaciar
 * `home_title` y publicar no vaciaba el encabezado: mostraba «PLAN NACIONAL DE
 * MÚSICA», una frase que no está en el panel, no está en el catálogo, y que no
 * se encuentra buscándola en el administrador de textos.
 *
 * De las 89 reservas que había, **23 decían algo distinto** del catálogo, diez
 * de ellas en Galería. No eran una copia de seguridad del texto: eran una
 * segunda copia que nadie mantenía.
 *
 * ES UNA HERRAMIENTA Y NO UNA PRUEBA DE KARMA porque el defecto vive en la
 * FORMA de la llamada, no en el resultado de un caso concreto. Atraparlo
 * renderizando exigiría un caso por clave y por página, y aun así solo cubriría
 * las claves que a alguien se le ocurriera probar. Además Karma corre en el
 * navegador y no puede leer el árbol de archivos.
 *
 * Es estática y barata: no necesita base, ni API, ni navegador.
 */
import { readFileSync, readdirSync, statSync } from 'node:fs';
import { join } from 'node:path';

const RAIZ = 'src/app';

const archivos = [];
(function recorrer(dir) {
  for (const entrada of readdirSync(dir)) {
    const ruta = join(dir, entrada);
    if (statSync(ruta).isDirectory()) { recorrer(ruta); continue; }
    if (/\.(ts|html)$/.test(ruta) && !ruta.endsWith('.spec.ts')) { archivos.push(ruta); }
  }
})(RAIZ);

const corto = (ruta) => ruta.replace(/\\/g, '/').replace(`${RAIZ}/`, '');

const conOperador = [];
const conReserva = [];
const conTextoPegado = [];

/**
 * Palabras escritas a mano justo detrás de un texto del CMS.
 *
 * Es la tercera forma del mismo defecto —el panel gobierna solo una parte de lo
 * que se lee— y la que más tiempo aguantó sin que nadie la viera. El pie de
 * página decía:
 *
 *     {{ getWebText('footer_credits_text') }} {{ currentYear }} Ministerio de las Culturas
 *
 * y el texto de fábrica de esa clave ya llevaba el año y el Ministerio dentro.
 * Resultado: «Copyright © 2026 Ministerio de las Culturas 2026 Ministerio de las
 * Culturas», **en trece páginas y durante meses**. No lo vio ninguna puerta: la
 * instantánea lo había congelado en su línea base, y esta herramienta solo
 * miraba `||` y segundos argumentos.
 *
 * Se exige que lo pegado tenga LETRAS. Un contador entre paréntesis —
 * `{{ getWebText('gallery_filter_all_cats') }} ({{ sortedAlbums().length }})`—
 * o un separador `;` no son copia y denunciarlos convertiría la puerta en ruido
 * que se acaba ignorando.
 */
/**
 * Repartos deliberados, declarados uno por uno.
 *
 * Un reparto puede estar bien: el dato que se pega puede ser algo que el panel
 * NO debe gobernar. Pero tiene que ser una decisión escrita, no un descuido, y
 * por eso se enumeran aquí con su motivo en vez de deducirlos.
 */
const REPARTOS_DECLARADOS = {
  // El panel gobierna el prefijo —«Copyright ©»— y el código pone el año y el
  // Ministerio. El año se CALCULA a propósito: si viviera en el panel, en enero
  // habría que acordarse de publicarlo, y el valor viejo ya llevaba «2026»
  // escrito a mano. Es justo lo que produjo la duplicación de trece páginas.
  footer_credits_text: 'el año se calcula y el nombre del Ministerio es fijo',
};

const palabrasPegadas = (linea) => {
  const encontradas = [];
  const patron = /getWebText\('([a-z0-9_]+)'\)\s*\}\}([^<]*)/g;
  for (const m of linea.matchAll(patron)) {
    if (m[1] in REPARTOS_DECLARADOS) { continue; }
    const cola = m[2].replace(/\{\{[^}]*\}\}/g, ' ');
    const palabra = cola.match(/[A-Za-zÁÉÍÓÚÜÑáéíóúüñ]{2,}/);
    if (palabra) { encontradas.push({ clave: m[1], pegado: cola.trim() }); }
  }
  return encontradas;
};

for (const ruta of archivos) {
  const texto = readFileSync(ruta, 'utf8');

  /*
    EL OPERADOR SE BUSCA EN LOS DOS SITIOS, y hasta solo en `.ts`.

    El agujero se encontró sembrándolo: `{{ getWebText('editorial_empty_title') || 'No
    encontramos resultados' }}` escrito en una PLANTILLA pasaba esta puerta en verde. Es el mismo
    defecto que el resumen de arriba describe —«las plantillas pasaban un texto de reserva escrito
    a mano»— solo que expresado dentro de la interpolación en vez de como segundo argumento, y era
    justamente la forma que no se miraba.

    Se cubre también `??`, que se lee como más inocente y hace exactamente lo mismo con `null`, y
    `getWebImage`, que resuelve igual desde.
  */
  for (const m of texto.matchAll(/^.*getWeb(?:Text|Image)\([^)]*\)\s*(?:\|\||\?\?).*$/gm)) {
    conOperador.push({ ruta: corto(ruta), linea: m[0].trim() });
  }

  if (!ruta.endsWith('.ts')) {
    // El segundo argumento es el que alimentaba el `||`.
    for (const m of texto.matchAll(/getWebText\('([a-z0-9_]+)',\s*'((?:[^'\\]|\\.)*)'\)/g)) {
      conReserva.push({ ruta: corto(ruta), clave: m[1], reserva: m[2] });
    }
    for (const [numero, linea] of texto.split(/\r?\n/).entries()) {
      for (const hallazgo of palabrasPegadas(linea)) {
        conTextoPegado.push({ ruta: `${corto(ruta)}:${numero + 1}`, ...hallazgo });
      }
    }
  }
}

console.log(`Revisados ${archivos.length} archivos de código.\n`);

if (conOperador.length === 0 && conReserva.length === 0 && conTextoPegado.length === 0) {
  console.log('✓ Ningún componente rellena un texto publicado en blanco,');
  console.log('  ni le pega palabras escritas a mano por detrás.');
  console.log('  Lo publicado manda; si no hay nada publicado, sale el texto del registro.');
  process.exit(0);
}

if (conOperador.length > 0) {
  console.error(`✗ ${conOperador.length} llamada(s) caen a un texto de reserva con «||» o «??»:\n`);
  for (const c of conOperador) { console.error(`    ${c.ruta}\n      ${c.linea}`); }
  console.error('');
}

if (conReserva.length > 0) {
  console.error(`✗ ${conReserva.length} llamada(s) de plantilla pasan un texto de reserva:\n`);
  for (const c of conReserva) { console.error(`    ${c.ruta}  ${c.clave} → ${JSON.stringify(c.reserva)}`); }
  console.error('');
}

if (conTextoPegado.length > 0) {
  console.error(`✗ ${conTextoPegado.length} clave(s) llevan palabras pegadas por detrás en la plantilla:\n`);
  for (const c of conTextoPegado) {
    console.error(`    ${c.ruta}\n      ${c.clave} → …}} ${JSON.stringify(c.pegado)}`);
  }
  console.error('');
  console.error('  El editor gobierna solo una parte de lo que se lee, y no puede saber cuál.');
  console.error('  Así apareció «Copyright © 2026 Ministerio de las Culturas 2026 Ministerio');
  console.error('  de las Culturas» en trece páginas: el texto de fábrica ya llevaba dentro lo');
  console.error('  que la plantilla añadía. O el texto entero sale del CMS, o el reparto se');
  console.error('  escribe en un comentario para que quien edite lo entienda.');
  console.error('');
}

console.error('El texto por defecto ya lo pone el registro. Una segunda copia en la');
console.error('plantilla se desincroniza y convierte «publicar en blanco» en «publicar otra cosa».');
process.exit(1);
