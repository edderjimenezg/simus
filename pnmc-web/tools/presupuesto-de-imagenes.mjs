/**
 * ¿Pesa alguna imagen del sitio mucho más de lo que su hueco justifica?
 *
 * EL DEFECTO QUE ESTA PUERTA ATRAPA, medido antes de escribirla:
 * `public/assets/branding/pnmc-blanco.png` pesa 227.579 bytes y mide 4500x2796, y el sitio lo
 * pinta en un hueco de 103x64 (`navigation.component.html:14`, con `h-10 sm:h-12 md:h-14 lg:h-16`).
 * Son 222 KB descargados EN CADA PÁGINA para dibujar un logotipo de 103 píxeles de ancho. Lleva
 * ahí desde siempre y nada lo dijo, porque «la imagen pesa mucho» no era una cifra que nadie
 * pudiera poner en rojo.
 *
 * QUÉ MIDE, y por qué así. No mide bytes: mide **bytes por píxel pintado**. Un archivo de 300 KB
 * es correcto para una portada de 1440x520 y un disparate para un logotipo de 103x64. Dividir por
 * el área del hueco es lo único que compara las dos cosas con la misma vara.
 *
 * El hueco sale del catálogo del CMS (`suggestedWidth`/`suggestedHeight`), que desde el 29 de
 * agosto de 2026 es el marco medido con el navegador y no una medida deseable.
 *
 * LO QUE NO MIDE, y conviene decirlo: solo mira las ranuras del catálogo con archivo local. Las
 * que hoy apuntan a `images.unsplash.com` no se pueden pesar sin red, y las fotos de
 * `public/Galeria` que el sitio usa como ranura fija entran solo cuando su clave esté declarada.
 * Una cifra con un punto ciego sin documentar es peor que no tener cifra.
 *
 *   node tools/presupuesto-de-imagenes.mjs
 *
 * Sale con código 1 si alguna ranura supera su presupuesto.
 */
import { readFileSync, statSync, existsSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
// `sharp` ya es dependencia de desarrollo: la usa `tools/optimize-images.mjs`. Aquí solo se le
// piden las medidas del archivo, que es lo único que `statSync` no sabe.
import sharp from 'sharp';

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..');
const CATALOGO = join(ROOT, '../pnmc-api/src/PNMC.Infrastructure/Data/web-content-catalog.json');

/**
 * Presupuesto en bytes por píxel del hueco, a densidad 1x.
 *
 * <b>ESTE NÚMERO SE CORRIGIÓ EL 30 DE AGOSTO DE 2026, Y CONVIENE SABER POR QUÉ.</b> Estaba en
 * 0,25, y decía haberse medido «recodificando las imágenes del sitio a dos veces su hueco en
 * WebP calidad 0,82». La medición era real pero la muestra era UNA: la portada de página, que se
 * pinta con `grayscale(1)` y por eso se guarda en gris —56 % menos— y además es un rectángulo
 * muy ancho y poco alto. Daba 0,146 y de ahí salió el 0,25.
 *
 * Cuando entraron las dieciocho ranuras de la portada, todas fotografías en color y con mucho
 * detalle, el mismo procedimiento —2x del hueco, WebP calidad 0,82— dio entre 0,19 y 0,55 B/px, y
 * la puerta marcó en rojo trece archivos correctos. Comprobado que no era pereza de compresión:
 * bajar una tarjeta de ruta a 0,25 exige 39 KB para 982x636, es decir 0,062 B por píxel PROPIO,
 * que en WebP es calidad 55-65 y se ve sucia.
 *
 * Se sube a 0,70 con el corpus completo delante —18 fotos en color más las que ya había— y no
 * con una. Sigue atrapando lo que había que atrapar: la peor ranura antes de optimizar daba 4,76
 * y el logotipo del defecto original, 8,63.
 *
 * <b>Y NO SE QUEDA SOLO CON ESTA VARA.</b> Aflojar un tope sin poner otro es aflojar la guarda,
 * así que este cambio vino con `SOBREDIMENSION_MAXIMA`, que mide algo que la compresión no puede
 * disimular: cuántas veces más píxeles tiene el archivo que el hueco donde se pinta.
 */
const PRESUPUESTO = 0.70;

/**
 * Cuántas veces más píxeles puede tener el archivo que su hueco.
 *
 * A densidad 2x —la que produce el recortador— un archivo tiene 4 veces los píxeles del hueco.
 * El tope es 8: deja holgura para las ranuras de clase `variable`, cuyo hueco crece con la
 * ventana, y sigue siendo implacable con lo que importa.
 *
 * Medido sobre los casos reales:
 *  · `pnmc-blanco.png` antes del arreglo: 4500x2796 para un hueco de 103x64 = 1.908 veces.
 *  · La foto de «Red Nacional de Jazz»: 1369x2048 para 491x318 = 18 veces.
 *  · Las mismas ya recodificadas: 982x636 para 491x318 = 4,0 veces.
 *
 * Esta vara es la que no depende de lo bien que comprima el formato: un archivo puede pesar poco
 * y seguir obligando al navegador a decodificar dieciocho veces los píxeles que va a pintar.
 */
const SOBREDIMENSION_MAXIMA = 8;

/**
 * Los logotipos van aparte y con un número más estrecho.
 *
 * Un `logotipo` se pinta con `object-contain` sobre un hueco pequeño y suele ser plano: comprime
 * muchísimo mejor que una fotografía. `logo-gov-co.png` recodificado a 200x40 da 3,7 KB, que son
 * 0,46 B/px del hueco de 100x20 — por encima del presupuesto general, porque el hueco es diminuto
 * y las cabeceras del formato pesan lo mismo. Por eso tiene su propio tope, más alto en B/px pero
 * mucho más estrecho en bytes absolutos.
 */
const PRESUPUESTO_LOGOTIPO = 0.60;

/** Ningún archivo del sitio tiene por qué pasar de aquí, sea cual sea su hueco. */
const TOPE_ABSOLUTO = 320 * 1024;

const catalogo = JSON.parse(readFileSync(CATALOGO, 'utf8'));
const ranuras = catalogo.images ?? [];

if (ranuras.length === 0) {
  console.error('El catálogo no trae ranuras de imagen. Ejecute `npm run cms:catalog` primero.');
  process.exit(1);
}

const hallazgos = [];
const medidas = [];
let externas = 0;

for (const ranura of ranuras) {
  const url = ranura.defaultUrl ?? '';
  if (!url.startsWith('/')) { externas++; continue; }

  const fichero = join(ROOT, 'public', decodeURIComponent(url).replace(/^\//, ''));
  if (!existsSync(fichero)) {
    hallazgos.push({
      clave: ranura.key,
      motivo: `su imagen de fábrica no existe en disco: ${url}`,
      cifra: '—',
    });
    continue;
  }

  const bytes = statSync(fichero).size;
  const area = (ranura.suggestedWidth ?? 0) * (ranura.suggestedHeight ?? 0);
  if (area === 0) {
    hallazgos.push({ clave: ranura.key, motivo: 'no declara el marco, así que no se puede presupuestar', cifra: '—' });
    continue;
  }

  const porPixel = bytes / area;
  const tope = ranura.use === 'logotipo' ? PRESUPUESTO_LOGOTIPO : PRESUPUESTO;

  let propios = 0;
  try {
    const meta = await sharp(fichero).metadata();
    propios = (meta.width ?? 0) * (meta.height ?? 0);
  } catch {
    // Un formato que `sharp` no abre no invalida la medida de peso, que es la principal.
  }
  const sobredimension = propios > 0 ? propios / area : 0;
  medidas.push({ clave: ranura.key, bytes, area, porPixel, tope, uso: ranura.use, url, sobredimension });

  if (sobredimension > SOBREDIMENSION_MAXIMA) {
    hallazgos.push({
      clave: ranura.key,
      motivo: `el archivo tiene ${sobredimension.toFixed(0)} veces los píxeles de su hueco de ${ranura.suggestedWidth}x${ranura.suggestedHeight}`,
      cifra: `el tope son ${SOBREDIMENSION_MAXIMA} veces (2x de densidad son 4)`,
    });
  } else if (porPixel > tope) {
    hallazgos.push({
      clave: ranura.key,
      motivo: `${(bytes / 1024).toFixed(0)} KB para un hueco de ${ranura.suggestedWidth}x${ranura.suggestedHeight}`,
      cifra: `${porPixel.toFixed(2)} B/px contra un tope de ${tope}`,
    });
  } else if (bytes > TOPE_ABSOLUTO) {
    hallazgos.push({
      clave: ranura.key,
      motivo: `${(bytes / 1024).toFixed(0)} KB en un solo archivo`,
      cifra: `el tope absoluto son ${(TOPE_ABSOLUTO / 1024).toFixed(0)} KB`,
    });
  }
}

medidas.sort((a, b) => b.porPixel - a.porPixel);

console.log(`Presupuesto de imágenes: ${ranuras.length} ranuras en el catálogo`);
console.log(`  ${medidas.length} con archivo local · ${externas} apuntan todavía a una URL externa\n`);

if (medidas.length) {
  console.log('  B/px   PESO     VECES  HUECO        RANURA');
  for (const m of medidas.slice(0, 8)) {
    const marca = m.porPixel > m.tope || m.sobredimension > SOBREDIMENSION_MAXIMA ? '✗' : ' ';
    console.log(
      `${marca} ${m.porPixel.toFixed(2).padStart(5)}  ${(m.bytes / 1024).toFixed(0).padStart(5)} KB  ` +
      `${m.sobredimension.toFixed(1).padStart(5)}x  ${String(m.area).padStart(9)} px  ${m.clave}`,
    );
  }
  console.log('');
}

if (!hallazgos.length) {
  console.log('✓ Ninguna ranura pasa de su presupuesto.');
  process.exit(0);
}

console.error(`✗ ${hallazgos.length} ranura(s) por encima de su presupuesto:\n`);
for (const h of hallazgos) {
  console.error(`  ${h.clave.padEnd(30)} ${h.motivo}`);
  console.error(`  ${''.padEnd(30)} ${h.cifra}\n`);
}
console.error(
  'Cada una es peso que el visitante descarga y no ve. Recodifíquela al doble de su hueco en\n' +
  'WebP calidad 0,82, o corrija el marco declarado si el que está mal es el marco.',
);
process.exit(1);
