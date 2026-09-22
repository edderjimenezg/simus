#!/usr/bin/env node
/**
 * Comprueba que el geovisor conecta la cartografía territorial con los datos.
 *
 * QUÉ MIDE, Y POR QUÉ ESTO NO ES UNA PRUEBA UNITARIA. Lo que se rompe aquí es la
 * cadena: el API sirve, el servicio normaliza, el componente pide y Leaflet pinta. Cada
 * eslabón por separado puede estar bien y la pantalla quedarse vacía. Ya pasó dos veces:
 *
 *   1. Al partir el TopoJSON monolítico en departamentos + fragmento municipal, la
 *      colección de municipios embebida quedó VACÍA. El componente seguía filtrándola
 *      en vez de pedir el fragmento remoto, así que abrir un departamento no dibujaba
 *      un solo municipio. Ninguna prueba se puso en rojo: el filtro sobre una lista
 *      vacía devuelve una lista vacía, que es «correcto».
 *   2. Los rótulos de departamento existían como tooltips permanentes, pero el CSS los
 *      deja en `opacity: 0` hasta que algo añade `map-department-labels-visible` al
 *      contenedor. Ese algo vivía en el componente que se perdió.
 *
 *   npm run mapa:territorios
 *   node tools/comprobar-territorios.mjs http://127.0.0.1:4300 ANTIOQUIA
 *
 * Necesita el sitio levantado y apuntando al API de este árbol. `proxy.conf.json`
 * debe enviar `/api` al puerto local vigente. Si apunta a otra instancia puede ocurrir
 * lo siguiente en silencio:
 *
 *   /api/v1/map/topojson/departments                 ->  404
 *   /api/v1/map/topojson/territories                 ->  200, 28.547.780 bytes
 *
 * `catalog.service.ts` tiene un `catchError` que cae al monolito, así que el mapa se
 * ve entero, los 125 municipios se dibujan y sus rótulos aparecen: lo único que delata el
 * problema es que no se pidió ni un fragmento municipal, y que el navegador acaba de
 * descargar 28 MB. Por eso este guion cuenta peticiones y no polígonos.
 *
 * Se arranca así:
 *
 *   npm start
 *
 * `proxy.conf.json` no se toca: lo comparten las dos sesiones.
 */

import { chromium } from 'playwright';

const BASE = process.argv[2] || 'http://127.0.0.1:4300';
const DEPARTAMENTO = process.argv[3] || 'ANTIOQUIA';

const navegador = await chromium.launch({
  args: ['--use-gl=swiftshader', '--enable-unsafe-swiftshader'],
});
const pagina = await navegador.newPage({ viewport: { width: 1500, height: 950 } });

const municipiosPedidos = [];
pagina.on('response', (r) => {
  if (/\/map\/topojson\/departments\/\d+\/municipalities/.test(r.url())) {
    municipiosPedidos.push(r.status() + ' ' + r.url().split('/api')[1]);
  }
});

await pagina.goto(`${BASE}/mapa`, { waitUntil: 'networkidle', timeout: 120000 });
await pagina.waitForTimeout(9000);
try {
  await pagina.locator('[aria-label*="errar"], .tutorial-close').first().click({ timeout: 4000 });
} catch {
  await pagina.keyboard.press('Escape');
}
await pagina.waitForTimeout(1500);

// El desplegable de departamento vive en la pestaña «Territorio» del panel izquierdo.
const territorio = pagina.locator('button', { hasText: /^\s*Territorio\s*$/i }).first();
if (await territorio.count()) await territorio.click().catch(() => undefined);
await pagina.waitForTimeout(800);

const selector = pagina.locator('select').first();
const hayDesplegable = (await selector.count()) > 0;
if (hayDesplegable) {
  await selector.selectOption({ label: DEPARTAMENTO }).catch(async () => {
    await selector.selectOption(DEPARTAMENTO).catch(() => undefined);
  });
}
await pagina.waitForTimeout(9000);

const medida = await pagina.evaluate(() => {
  const contenedor = document.querySelector('.leaflet-container');
  const tooltips = [...document.querySelectorAll('.leaflet-tooltip')];
  const visible = (el) => {
    const s = getComputedStyle(el);
    return s.visibility !== 'hidden' && Number(s.opacity) > 0;
  };
  return {
    poligonos: document.querySelectorAll('.leaflet-overlay-pane path').length,
    rotulosDepartamento: tooltips.filter((t) => t.classList.contains('department-label')).length,
    rotulosDepartamentoVisibles: tooltips.filter((t) => t.classList.contains('department-label') && visible(t)).length,
    // El rotulo de municipio es un marcador con divIcon, no un tooltip: Leaflet solo
    // admite un tooltip por capa y el globo de hover ya ocupaba ese sitio.
    rotulosMunicipio: document.querySelectorAll('.municipality-label').length,
    rotulosMunicipioVisibles: [...document.querySelectorAll('.municipality-label')].filter(visible).length,
    clasesDelMapa: contenedor ? contenedor.className : '(sin contenedor)',
  };
});

console.log('');
console.log(`  sitio:                        ${BASE}/mapa`);
console.log(`  departamento elegido:         ${DEPARTAMENTO}${hayDesplegable ? '' : '  (NO se encontró el desplegable)'}`);
console.log(`  fragmentos municipales pedidos al API:  ${municipiosPedidos.length}`);
municipiosPedidos.slice(0, 3).forEach((m) => console.log(`      ${m}`));
console.log(`  polígonos dibujados:          ${medida.poligonos}`);
console.log(`  rótulos de departamento:      ${medida.rotulosDepartamento} (visibles: ${medida.rotulosDepartamentoVisibles})`);
console.log(`  rótulos de municipio:         ${medida.rotulosMunicipio} (visibles: ${medida.rotulosMunicipioVisibles})`);
console.log('');

const fallos = [];
if (!hayDesplegable) fallos.push('No se encontró el desplegable de departamento; la medición no vale.');
if (municipiosPedidos.length === 0) {
  fallos.push(
    'Al abrir un departamento no se pidió su fragmento municipal al API.\n' +
      '  El mapa se ve igual —los departamentos siguen pintados— pero no hay municipios debajo.',
  );
}
if (medida.rotulosMunicipio === 0) {
  fallos.push('Ningún municipio lleva rótulo.');
}
if (medida.rotulosDepartamento === 0) {
  fallos.push('Ningún departamento lleva rótulo.');
}

if (fallos.length) {
  for (const f of fallos) console.error('✗ ' + f);
  console.error('');
  process.exitCode = 1;
} else {
  console.log('✓ Los municipios se piden y se dibujan, y los territorios llevan rótulo.');
  console.log('');
}

await navegador.close();
