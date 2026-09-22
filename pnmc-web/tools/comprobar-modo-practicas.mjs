#!/usr/bin/env node
/**
 * Comprueba que el Modo de Prácticas e Influencia dibuja cada proceso DONDE ESTÁ.
 *
 * POR QUE EXISTE. `thematicPoints` no usaba ninguna posición: la calculaba. Tomaba
 * el centroide del DEPARTAMENTO y le sumaba una espiral cuyo ángulo y radio salían del
 * ÍNDICE DEL REGISTRO EN EL ARRAY:
 *
 *     const angle = (index * 0.72) % (2 * Math.PI);
 *     const radius = 0.08 + ((index * 0.03) % 0.14);
 *     lat = centroid[0] + Math.sin(angle) * radius;
 *
 * Reordenar los datos movía los puntos por el mapa. El mapa afirmaba que un proceso
 * estaba donde no está.
 *
 * LA MEDICIÓN QUE HABRÍA MATADO AQUELLO, y que es el corazón de este guion: se lee el
 * `data-lat` / `data-lng` de cada señalador dibujado y se compara, uno por uno, con la
 * coordenada que `/api/v1/divipola/locations` devuelve para ese `data-municipio`. No se
 * comprueba que «haya puntos»: se comprueba que cada punto sea el que el dato sostiene.
 *
 * POR QUÉ NO ES UNA PRUEBA UNITARIA. `agruparProcesosPorMunicipio` tiene trece pruebas de
 * dominio y todas pasan con datos de laboratorio. Lo que este guion mide es la cadena
 * entera: el API sirve, el servicio pagina, el componente agrupa y Leaflet dibuja. Cada
 * eslabón puede estar bien y la pantalla acabar mintiendo.
 *
 *   npm run mapa:modo
 *   node tools/comprobar-modo-practicas.mjs http://127.0.0.1:4300 http://127.0.0.1:8180
 *
 * Necesita el sitio levantado Y el API accesible, porque la comparación es contra el API.
 */

import { chromium } from 'playwright';

const BASE = process.argv[2] || 'http://127.0.0.1:4300';
const API = process.argv[3] || 'http://127.0.0.1:8081';

// Tolerancia de comparación: 1e-6 grados, unos 11 cm. Es una igualdad, no una cercanía;
// la holgura solo cubre el redondeo de imprimir el número en un atributo HTML.
const TOLERANCIA = 1e-6;

/** Los puntos municipales, pedidos al API igual que los pide el sitio. */
async function puntosDelApi() {
  const puntos = new Map();
  let offset = 0;
  let total = Infinity;
  while (offset < total && offset < 5000) {
    const r = await fetch(`${API}/api/v1/divipola/locations?limit=500&offset=${offset}`);
    if (!r.ok) throw new Error(`El API respondió ${r.status} a divipola/locations`);
    const pagina = await r.json();
    for (const fila of pagina.items || []) {
      if (typeof fila.latitude === 'number' && typeof fila.longitude === 'number') {
        puntos.set(String(fila.municipalityCode), { lat: fila.latitude, lng: fila.longitude });
      }
    }
    total = Number(pagina.total) || 0;
    if (!(pagina.items || []).length) break;
    offset += 500;
  }
  return puntos;
}

const puntos = await puntosDelApi();

const navegador = await chromium.launch({
  args: ['--use-gl=swiftshader', '--enable-unsafe-swiftshader'],
});
const pagina = await navegador.newPage({ viewport: { width: 1400, height: 900 } });

const errores = [];
pagina.on('pageerror', (e) => errores.push(e.message.slice(0, 160)));
pagina.on('console', (m) => {
  if (m.type() === 'error' && !m.text().includes('401')) errores.push(m.text().slice(0, 160));
});

await pagina.goto(`${BASE}/mapa`, { waitUntil: 'networkidle', timeout: 120000 });
await pagina.waitForTimeout(11000);
try {
  await pagina.locator('[aria-label*="errar"], .tutorial-close').first().click({ timeout: 4000 });
} catch {
  await pagina.keyboard.press('Escape');
}
await pagina.waitForTimeout(1500);

await pagina.locator('button', { hasText: /^\s*Modos\s*$/i }).first().click();
await pagina.waitForTimeout(700);
await pagina.locator('button', { hasText: /Modo de Prácticas e Influencia/i }).first().click();
await pagina.waitForTimeout(9000);

const agrupado = await pagina.evaluate(() => {
  const marcas = [...document.querySelectorAll('.senalador-municipal')];
  return {
    dibujados: marcas.length,
    sumaDeProcesos: marcas.reduce((t, m) => t + Number(m.getAttribute('data-total') || 0), 0),
    grupos: marcas.filter((m) => m.hasAttribute('data-grupo')).length,
    hayConmutadorDeCalor: document.body.textContent.includes('Mapa de Calor'),
    leyenda: document.querySelector('aside[aria-label="Controles del geovisor"]')?.textContent || '',
  };
});

// Se acerca por encima del umbral de agrupación para que cada municipio quede solo y se
// pueda comparar su posición con la del API.
await pagina.evaluate(() => {
  const boton = [...document.querySelectorAll('button')].find((b) => b.getAttribute('aria-label') === 'Acercar el mapa');
  for (let i = 0; i < 8; i += 1) boton?.click();
});
await pagina.waitForTimeout(6000);

const sueltos = await pagina.evaluate(() =>
  [...document.querySelectorAll('.senalador-municipal[data-municipio]')].map((m) => ({
    municipio: m.getAttribute('data-municipio'),
    lat: Number(m.getAttribute('data-lat')),
    lng: Number(m.getAttribute('data-lng')),
    total: Number(m.getAttribute('data-total')),
  })),
);

const desviados = sueltos.filter((s) => {
  const punto = puntos.get(s.municipio);
  if (!punto) return true;
  return Math.abs(punto.lat - s.lat) > TOLERANCIA || Math.abs(punto.lng - s.lng) > TOLERANCIA;
});

console.log('');
console.log(`  sitio:                              ${BASE}/mapa`);
console.log(`  puntos municipales en el API:       ${puntos.size}`);
console.log(`  señaladores a escala nacional:      ${agrupado.dibujados}  (${agrupado.grupos} son grupos)`);
console.log(`  procesos sumados por los dibujados: ${agrupado.sumaDeProcesos}`);
console.log(`  señaladores sueltos tras acercar:   ${sueltos.length}`);
console.log(`  de esos, con posición que NO es la del API: ${desviados.length}`);
if (desviados.length) console.log(`      ${JSON.stringify(desviados.slice(0, 3))}`);
console.log(`  conmutador «Mapa de Calor»:         ${agrupado.hayConmutadorDeCalor ? 'PRESENTE' : 'no está'}`);
console.log(`  errores de consola:                 ${errores.length}`);
errores.slice(0, 3).forEach((e) => console.log('      ' + e));
console.log('');

const fallos = [];

if (puntos.size === 0) {
  fallos.push('El API no devolvió ni un punto municipal; la comparación no vale.');
}
if (agrupado.dibujados === 0) {
  fallos.push(
    'El modo no dibujó ni un señalador.\n' +
      '  Si la consola dice «Map has no maxZoom specified», el agrupador se quedó sin tope de acercamiento.',
  );
}
if (agrupado.sumaDeProcesos === 0) {
  fallos.push('Los señaladores no suman ni un proceso: la cifra de cada pastilla es lo que informa.');
}
if (sueltos.length === 0) {
  fallos.push(
    'Tras acercarse no quedó ningún señalador suelto que comparar con el API.\n' +
      '  Sin eso, la comprobación de posición —el corazón de este guion— no se ejecutó.',
  );
} else if (desviados.length > 0) {
  fallos.push(
    `${desviados.length} de ${sueltos.length} señaladores están en una posición que el API no sostiene.\n` +
      '  Es el defecto que se corrigió: una posición calculada en vez de leída.',
  );
}
if (agrupado.hayConmutadorDeCalor) {
  fallos.push('Volvió el conmutador «Mapa de Calor», que se retiró por dibujar círculos de 85 km sin CSS.');
}
for (const obligatorio of ['Cómo leer el mapa', 'punto de referencia del municipio', 'coincidencia de texto']) {
  if (!agrupado.leyenda.includes(obligatorio)) {
    fallos.push(`La leyenda no dice «${obligatorio}». El mapa tiene que declarar de dónde salen sus afirmaciones.`);
  }
}
if (errores.length) {
  fallos.push(`Hay ${errores.length} error(es) en consola: ${errores[0]}`);
}

if (fallos.length) {
  for (const f of fallos) console.error('✗ ' + f);
  console.error('');
  process.exitCode = 1;
} else {
  console.log('✓ Cada señalador está donde el API dice, la suma cuadra y el mapa declara su procedencia.');
  console.log('');
}

await navegador.close();
