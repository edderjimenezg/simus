#!/usr/bin/env node
/**
 * Comprueba que TODOS los territorios del mapa se rotulan con la misma regla.
 *
 * POR QUE EXISTE, DEL 29 DE AGOSTO DE 2026. El archipiélago llevaba
 * `className: 'archipelago-label'`, una clase que NO EXISTE en `styles.css`: ni una
 * regla. Sin estilo propio se quedaba con el globo por defecto de Leaflet —caja blanca
 * con borde y sombra, 312 px de ancho— flotando sobre el Caribe. Y como el interruptor
 * que apaga los rótulos fuera de zoom se aplica a `.department-label`, ese rótulo se
 * quedaba fuera del interruptor y salía SIEMPRE.
 *
 * Medido antes del arreglo, contando rótulos visibles al abrir la página:
 *
 *   1280x639    0 de 32 departamentos    archipiélago VISIBLE
 *   1500x950   32 de 32 departamentos    archipiélago visible
 *
 * En la pantalla del usuario era la única etiqueta del mapa, y además la única con
 * caja. Su queja, literal: «aquí siempre me está mostrando una etiqueta de san andrés».
 *
 * POR QUÉ NO ES UNA PRUEBA UNITARIA. Ninguna de las tres piezas está rota por separado:
 * el nombre de clase es una cadena válida, el CSS es válido, y `labelVisibilityForZoom`
 * tiene nueve pruebas y es correcta. Lo que falla es que la cadena no case con ninguna
 * regla, y eso solo se ve cuando el navegador resuelve el estilo.
 *
 * LAS DOS REGLAS QUE FIJA:
 *
 *   1. A cada distancia, o se leen TODOS los territorios o no se lee NINGUNO. Un mapa
 *      con una sola etiqueta no está rotulado: está sucio.
 *   2. Ningún rótulo pasa de `ANCHO_MAXIMO`. Esta segunda salió de un falso verde: con
 *      la clase ya corregida, el archipiélago seguía escribiendo su nombre completo
 *      —«Archipiélago de San Andrés, Providencia y Santa Catalina»— y ocupaba 311 px,
 *      casi el triple que el siguiente más ancho, «NORTE DE SANTANDER» con 112. La
 *      primera regla lo daba por bueno porque el rótulo ya se apagaba con los demás.
 *
 *   npm run mapa:rotulos
 *   node tools/comprobar-rotulos.mjs http://127.0.0.1:4300
 *
 * Necesita el sitio levantado. Si la capa base da cero tejas, mira antes la nota de la
 * caché de Vite en la cabecera de `comprobar-capa-base.mjs`.
 */

import { chromium } from 'playwright';

const BASE = process.argv[2] || 'http://127.0.0.1:4300';

// El nombre de departamento más largo que existe de verdad, «NORTE DE SANTANDER», mide
// 112 px con la tipografía del rótulo. El tope deja holgura para uno algo mayor y corta
// muy por debajo de los 311 px que ocupaba el nombre completo del archipiélago.
const ANCHO_MAXIMO = 170;

// El primero es el del usuario: mapa pequeño, sin rótulos. El segundo, con rótulos.
const PANTALLAS = [
  { width: 1280, height: 639, rotulada: false },
  { width: 1500, height: 950, rotulada: true },
];

const navegador = await chromium.launch({
  args: ['--use-gl=swiftshader', '--enable-unsafe-swiftshader'],
});

const fallos = [];

for (const pantalla of PANTALLAS) {
  const pagina = await navegador.newPage({ viewport: { width: pantalla.width, height: pantalla.height } });
  await pagina.goto(`${BASE}/mapa`, { waitUntil: 'networkidle', timeout: 120000 });
  await pagina.waitForTimeout(11000);
  try {
    await pagina.locator('[aria-label*="errar"], .tutorial-close').first().click({ timeout: 4000 });
  } catch {
    await pagina.keyboard.press('Escape');
  }
  await pagina.waitForTimeout(2000);

  const medida = await pagina.evaluate(() => {
    const visible = (el) => {
      const s = getComputedStyle(el);
      return s.visibility !== 'hidden' && Number(s.opacity) > 0 && s.display !== 'none';
    };

    const contenedor = document.querySelector('.leaflet-container');

    // TODOS los globos permanentes del mapa, sean de la clase que sean. Contar solo
    // `.department-label` daría verde justo en el caso que rompió: un rótulo con una
    // clase inventada no aparece en ese recuento.
    const todos = [...document.querySelectorAll('.leaflet-tooltip')];
    const visibles = todos.filter(visible);

    return {
      interruptor: Boolean(contenedor?.classList.contains('map-department-labels-visible')),
      totalGlobos: todos.length,
      visibles: visibles.length,
      // Un rótulo de territorio no lleva caja: el estilo lo deja transparente y sin
      // borde. Cualquiera con fondo opaco es un globo de Leaflet sin estilo propio.
      conCaja: visibles
        .filter((t) => {
          const s = getComputedStyle(t);
          const fondo = s.backgroundColor;
          return fondo !== 'rgba(0, 0, 0, 0)' && fondo !== 'transparent';
        })
        .map((t) => `«${(t.textContent || '').trim().slice(0, 46)}» [${t.className.replace(/leaflet-\S*/g, '').trim()}]`),
      // Clases de rótulo que el CSS no conoce: se detectan porque el navegador no les
      // aplicó ninguna de las propiedades que definen un rótulo de territorio.
      textos: visibles.map((t) => (t.textContent || '').replace(/\s+/g, ' ').trim().slice(0, 46)),
      anchoMaximo: visibles.reduce((max, t) => Math.max(max, Math.round(t.getBoundingClientRect().width)), 0),
      demasiadoAnchos: visibles
        .map((t) => ({ w: Math.round(t.getBoundingClientRect().width), txt: (t.textContent || '').trim() }))
        .sort((uno, otro) => otro.w - uno.w)
        .slice(0, 3),
    };
  });

  const etiqueta = `${pantalla.width}x${pantalla.height}`;
  console.log('');
  console.log(`  ${etiqueta}`);
  console.log(`    interruptor de rótulos:  ${medida.interruptor ? 'encendido' : 'apagado'}`);
  console.log(`    globos en el mapa:       ${medida.totalGlobos}   visibles: ${medida.visibles}`);
  console.log(`    ancho del más ancho:     ${medida.anchoMaximo} px`);
  if (medida.conCaja.length) console.log(`    CON CAJA:                ${medida.conCaja.join(' | ')}`);
  if (medida.visibles > 0 && medida.visibles <= 4) console.log(`    textos:                  ${medida.textos.join(' | ')}`);

  if (medida.totalGlobos === 0) {
    fallos.push(`${etiqueta}: no hay ni un rótulo en el mapa; la medición no vale.`);
  }

  if (pantalla.rotulada) {
    if (!medida.interruptor) {
      fallos.push(`${etiqueta}: se esperaba el interruptor encendido y está apagado; la medición no vale.`);
    } else if (medida.visibles < medida.totalGlobos) {
      fallos.push(
        `${etiqueta}: con los rótulos encendidos se leen ${medida.visibles} de ${medida.totalGlobos}.\n` +
          '  A esta distancia o se nombran todos los territorios o no se nombra ninguno.',
      );
    }
  } else {
    if (medida.interruptor) {
      fallos.push(`${etiqueta}: se esperaba el interruptor apagado y está encendido; la medición no vale.`);
    } else if (medida.visibles > 0) {
      fallos.push(
        `${etiqueta}: con los rótulos apagados se sigue leyendo ${medida.visibles}: ${medida.textos.join(', ')}.\n` +
          '  Es el defecto exacto que se corrigió: un rótulo fuera del interruptor de zoom.',
      );
    }
  }

  const anchos = medida.demasiadoAnchos.filter((fila) => fila.w > ANCHO_MAXIMO);
  if (anchos.length) {
    const detalle = anchos.map((fila) => `  ${fila.w} px «${fila.txt}»`).join('\n');
    fallos.push(
      `${etiqueta}: ${anchos.length} rótulo(s) pasan de ${ANCHO_MAXIMO} px.\n${detalle}\n` +
        '  Un rótulo así cruza la cuenca entera y tapa los territorios de al lado.',
    );
  }

  if (medida.conCaja.length) {
    fallos.push(
      `${etiqueta}: hay ${medida.conCaja.length} rótulo(s) con caja de globo de Leaflet.\n` +
        `  ${medida.conCaja[0]}\n` +
        '  Un rótulo de territorio va sin fondo ni borde. Con caja, su clase no casa con ninguna regla del CSS.',
    );
  }

  await pagina.close();
}

console.log('');
if (fallos.length) {
  for (const f of fallos) console.error('✗ ' + f);
  console.error('');
  process.exitCode = 1;
} else {
  console.log('✓ Todos los territorios siguen la misma regla de rótulo, y ninguno lleva caja.');
  console.log('');
}

await navegador.close();
