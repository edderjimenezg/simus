#!/usr/bin/env node
/**
 * Comprueba que la capa base del geovisor está pintando de verdad.
 *
 * POR QUÉ ESTO NO ES UNA PRUEBA UNITARIA. Lo que falla aquí no falla en el dominio ni
 * en una plantilla: falla en la conjunción de Leaflet, MapLibre, WebGL y el orden en
 * que Angular ejecuta los efectos. Los tres defectos que ya nos costó esta zona no los
 * habría cazado ningún `expect`:
 *
 *   1. CARTO empezó a exigir clave y siguió respondiendo 200 con el peso de siempre,
 *      estampando «API KEY REQUIRED» DENTRO del PNG.
 *   2. `initializeMapOnce` se llamaba desde dentro de un `effect()` y lanzaba `NG0602`
 *      en la primera línea que creaba otro efecto. La capa base quedaba con `style` en
 *      nulo y NO PEDÍA NI UNA TEJA. El mapa se veía: era el color de fondo del estilo.
 *   3. Un `getStyle()` vacío no lanza nada ni ensucia la consola.
 *
 * Lo único que distingue «pinta» de «no pinta» es que se PIDAN TEJAS. Eso es lo que
 * mide este guion.
 *
 *   npm run mapa:capa-base                  # contra http://127.0.0.1:4300/mapa-ecosistemico
 *   node tools/comprobar-capa-base.mjs http://127.0.0.1:4300
 *
 * Necesita el sitio levantado. Con WebGL por software las tejas tardan, de ahí la
 * espera larga.
 *
 * SI DA CERO TEJAS DE GOLPE Y NO HAS TOCADO LA CAPA BASE, ANTES DE DIAGNOSTICAR: reinicia
 * el servidor de desarrollo con la caché limpia.
 *
 *     rm -rf .angular/cache && npm start
 *
 * La optimización de dependencias de Vite deja `maplibre-gl` en un estado en el que el
 * mapa se construye y el lienzo WebGL existe, pero la petición del JSON del estilo sale y
 * muere en `net::ERR_ABORTED`. No hay error en consola ni excepción: el mapa se ve, porque
 * el color de fondo del estilo se pinta igual.
 *
 * NO HACE FALTA RECARGAR EN CALIENTE PARA CAERSE AHÍ, y esto se corrigió el 29 de agosto
 * de 2026 porque la nota anterior lo decía mal. Basta con arrancar el servidor sobre una
 * caché ya escrita. Medido ese día, en este orden:
 *
 *   caché limpia + servidor nuevo                       ->  6 tejas
 *   caché caliente + servidor nuevo                     ->  0 tejas
 *   caché caliente + servidor nuevo, SIN el cambio en
 *   curso, tres pasadas seguidas sobre el commit 0796720 ->  0, 0, 0 tejas
 *
 * La tercera línea es la que importa: el cero no depende del código. Antes de acusar a un
 * cambio, se reproduce el cero sin él.
 */

import { chromium } from 'playwright';

const BASE = process.argv[2] || 'http://127.0.0.1:4300';
const ESPERA_MS = 20000;
const MINIMO_TEJAS = 1;

const navegador = await chromium.launch({
  args: ['--use-gl=swiftshader', '--enable-unsafe-swiftshader'],
});
const pagina = await navegador.newPage({ viewport: { width: 1400, height: 900 } });

const tejas = [];
const conClave = [];
pagina.on('response', (r) => {
  const u = r.url();
  // Una teja de mapa termina en /z/x/y.ext, con los tres numeros. El sprite del estilo
  // tambien es un .png y NO lo es: `/sprites/ofm_f384/ofm.png` no trae la terna. Contarlo
  // ponia este comprobador en verde sin que se hubiese descargado un solo dato.
  if (/\/\d+\/\d+\/\d+\.(pbf|png|jpe?g|webp)(\?|$)/.test(u)) {
    tejas.push(r.status() + ' ' + u.slice(0, 90));
  }
  if (/[?&](key|api_key|apikey|access_token)=/.test(u)) conClave.push(u.slice(0, 90));
});

const errores = [];
pagina.on('pageerror', (e) => errores.push(e.message.slice(0, 160)));
pagina.on('console', (m) => {
  if (m.type() === 'error' && !m.text().includes('401')) errores.push(m.text().slice(0, 160));
});

const RUTA_MAPA = '/mapa-ecosistemico';

await pagina.goto(`${BASE}${RUTA_MAPA}`, { waitUntil: 'networkidle', timeout: 120000 });
await pagina.waitForTimeout(ESPERA_MS);

const lienzo = await pagina.locator('canvas.maplibregl-canvas').count();
const atribucion = await pagina
  .locator('.leaflet-control-attribution')
  .innerText()
  .catch(() => '');

console.log('');
console.log(`  sitio:              ${BASE}${RUTA_MAPA}`);
console.log(`  tejas descargadas:  ${tejas.length}`);
console.log(`  lienzo de MapLibre: ${lienzo}`);
console.log(`  atribución visible: ${atribucion.replace(/\s+/g, ' ').trim() || '(NINGUNA)'}`);
if (tejas.length) console.log(`  primera teja:       ${tejas[0]}`);
console.log('');

const fallos = [];
if (tejas.length < MINIMO_TEJAS) {
  fallos.push(
    `La capa base no descargó ninguna teja. El mapa puede verse igual —el color de fondo\n` +
      `  del estilo se pinta aunque no llegue un solo dato—, pero no hay cartografía debajo.`,
  );
}
if (conClave.length) {
  fallos.push(`Hay peticiones con clave de acceso, y ningún proveedor de la tabla debería pedirla:\n  ${conClave[0]}`);
}
if (!atribucion.trim()) {
  fallos.push('No hay atribución visible. Las licencias de OpenFreeMap, OpenMapTiles y OpenStreetMap la exigen.');
}
const ng0602 = errores.find((e) => e.includes('NG0602'));
if (ng0602) {
  fallos.push('Volvió el NG0602: se está creando un efecto dentro de un contexto reactivo.\n  ' + ng0602.slice(0, 120));
}

if (fallos.length) {
  for (const f of fallos) console.error('✗ ' + f);
  if (errores.length) {
    console.error('');
    console.error('  errores de consola:');
    errores.slice(0, 5).forEach((e) => console.error('    ' + e));
  }
  console.error('');
  process.exitCode = 1;
} else {
  console.log('✓ La capa base descarga cartografía, no pide clave y atribuye.');
  console.log('');
}

await navegador.close();
