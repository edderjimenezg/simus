#!/usr/bin/env node
/**
 * Comprueba que la tarjeta de procesos de un municipio NO TAPA EL MAPA.
 *
 * LA QUEJA, LITERAL, DEL 29 DE AGOSTO DE 2026: «la ventana emergente cuando de click no
 * debe quitar el mapa, debe salir mas pequena alli justo al lado, para poder ver un
 * listado de los procesos e ir a ver los procesos completos a sus fichas completas».
 *
 * QUE MIDE, Y POR QUE NO BASTA UNA PRUEBA UNITARIA. `territoryPopupPlacement` es
 * geometria pura y ya tiene nueve pruebas que la fijan. Ninguna de ellas ve la pantalla.
 * Lo que se rompio aqui no fue el calculo: fue que la tarjeta venia envuelta en un
 * `fixed inset-0` con velo y desenfoque. Se puede calcular la posicion perfecta y seguir
 * tapando el mapa entero con el contenedor.
 *
 * LA MEDIDA CENTRAL ES UNA DIFERENCIA, NO UN PORCENTAJE. Se muestrea una rejilla sobre
 * todo el viewport y se cuenta cuantos puntos son mapa ANTES de abrir la tarjeta y
 * cuantos DESPUES. El valor absoluto no dice nada —la pagina tiene panel de control a la
 * izquierda, barra arriba y columna de lectura a la derecha, asi que el mapa nunca pasa
 * del 45 % de la pantalla— pero la DIFERENCIA es exactamente lo que la ventana emergente
 * le quita al usuario. Con el modal anterior la diferencia era el 100 %: el velo se
 * llevaba los 170 puntos de mapa. Con la tarjeta debe llevarse solo su propio hueco.
 *
 * EL ARRASTRE SE CALCULA DESDE EL CONTENEDOR, NO SE ADIVINA. La primera version de este
 * guion arrastraba en x=1150 y daba «la tarjeta se quedo clavada». El contenedor del mapa
 * termina en x=1143: el arrastre caia fuera del mapa y no movia nada. El codigo estaba
 * bien y el comprobador mentia.
 *
 *   npm run mapa:tarjeta
 *   node tools/comprobar-tarjeta-municipio.mjs http://127.0.0.1:4300 ANTIOQUIA
 *
 * Necesita el sitio levantado. Si da cero municipios, mira antes la nota de la cache de
 * Vite en la cabecera de `comprobar-capa-base.mjs`.
 */

import { chromium } from 'playwright';

const BASE = process.argv[2] || 'http://127.0.0.1:4300';
const DEPARTAMENTO = process.argv[3] || 'ANTIOQUIA';
const ANCHO_MAXIMO = 260; // 232 de la tarjeta + holgura de borde y sombra
const PERDIDA_MAXIMA = 15; // % de puntos de mapa que la tarjeta puede quitar
const INTENTOS = 30; // municipios que se prueban buscando uno con procesos

const contarMapa = () =>
  pagina.evaluate(() => {
    let mapa = 0;
    let total = 0;
    const paso = 60;
    for (let x = paso / 2; x < window.innerWidth; x += paso) {
      for (let y = paso / 2; y < window.innerHeight; y += paso) {
        total += 1;
        const bajoElCursor = document.elementFromPoint(x, y);
        if (bajoElCursor && bajoElCursor.closest('.leaflet-container')) mapa += 1;
      }
    }
    return { mapa, total };
  });

const leerTarjeta = (punto) =>
  pagina.evaluate((p) => {
    const tarjeta = document.querySelector('[role="dialog"][data-lado]');
    const rect = tarjeta ? tarjeta.getBoundingClientRect() : null;

    // Un velo a pantalla completa por encima del mapa: el comportamiento anterior.
    const velos = [...document.querySelectorAll('body *')]
      .filter((el) => {
        const s = getComputedStyle(el);
        if (s.position !== 'fixed' || s.display === 'none' || s.visibility === 'hidden') return false;
        const r = el.getBoundingClientRect();
        const cubreTodo = r.width >= window.innerWidth * 0.95 && r.height >= window.innerHeight * 0.95;
        const opaca = s.backgroundColor !== 'rgba(0, 0, 0, 0)' || s.backdropFilter !== 'none';
        return cubreTodo && opaca;
      })
      .map((el) => String(el.className || el.tagName).slice(0, 80));

    return {
      hayTarjeta: Boolean(tarjeta),
      lado: (tarjeta && tarjeta.getAttribute('data-lado')) || '',
      titulo: tarjeta?.querySelector('h2')?.textContent?.trim() || '',
      ancho: rect ? Math.round(rect.width) : 0,
      alto: rect ? Math.round(rect.height) : 0,
      izquierda: rect ? Math.round(rect.left) : 0,
      arriba: rect ? Math.round(rect.top) : 0,
      dentroDelViewport: rect
        ? rect.left >= 0 && rect.top >= 0 && rect.right <= window.innerWidth && rect.bottom <= window.innerHeight
        : false,
      tapaElPunto:
        rect && p
          ? p.x >= rect.left && p.x <= rect.right && p.y >= rect.top && p.y <= rect.bottom
          : false,
      procesos: document.querySelectorAll('[role="dialog"][data-lado] article').length,
      // La otra mitad del pedido: «ir a ver los procesos completos a sus fichas completas».
      enlacesAFicha: [...document.querySelectorAll('[role="dialog"][data-lado] article button')].filter((b) =>
        /ficha completa/i.test(b.textContent || ''),
      ).length,
      // `aria-modal` sobre un dialogo que deja el mapa vivo seria mentira al lector de pantalla.
      diceSerModal: Boolean(tarjeta) && tarjeta.getAttribute('aria-modal') === 'true',
      velos,
    };
  }, punto);

const navegador = await chromium.launch({
  args: ['--use-gl=swiftshader', '--enable-unsafe-swiftshader'],
});
const pagina = await navegador.newPage({ viewport: { width: 1500, height: 950 } });

await pagina.goto(`${BASE}/mapa`, { waitUntil: 'networkidle', timeout: 120000 });
await pagina.waitForTimeout(9000);
try {
  await pagina.locator('[aria-label*="errar"], .tutorial-close').first().click({ timeout: 4000 });
} catch {
  await pagina.keyboard.press('Escape');
}
await pagina.waitForTimeout(1200);

const territorio = pagina.locator('button', { hasText: /^\s*Territorio\s*$/i }).first();
if (await territorio.count()) await territorio.click().catch(() => undefined);
await pagina.waitForTimeout(800);

const selector = pagina.locator('select').first();
await selector.selectOption({ label: DEPARTAMENTO }).catch(async () => {
  await selector.selectOption(DEPARTAMENTO).catch(() => undefined);
});
await pagina.waitForTimeout(9000);

const marco = await pagina.evaluate(() => {
  const c = document.querySelector('.leaflet-container');
  return c ? c.getBoundingClientRect().toJSON() : null;
});

// LINEA BASE: cuanta pantalla es mapa con NADA abierto.
const base = await contarMapa();

// Se pulsa sobre el rotulo de un municipio: el marcador es `interactive: false`, asi que
// el clic atraviesa hasta el poligono de debajo. Es la unica manera fiable de dar con un
// municipio concreto sin conocer la geometria. Se recorren varios porque la mayoria de
// los municipios no tiene ningun proceso registrado, y con la lista vacia no se puede
// comprobar la mitad del pedido: el enlace a la ficha completa.
const rotulos = pagina.locator('.municipality-label');
const cuantosRotulos = await rotulos.count();
let puntoPulsado = null;
let medida = null;
let probados = 0;

for (let i = 0; i < Math.min(INTENTOS, cuantosRotulos); i += 1) {
  const indice = Math.floor((i * cuantosRotulos) / Math.min(INTENTOS, cuantosRotulos));
  const caja = await rotulos.nth(indice).boundingBox();
  if (!caja) continue;
  const punto = { x: Math.round(caja.x + caja.width / 2), y: Math.round(caja.y + caja.height / 2) };
  if (marco && (punto.x < marco.left || punto.x > marco.right || punto.y < marco.top || punto.y > marco.bottom)) {
    continue;
  }
  probados += 1;
  await pagina.mouse.click(punto.x, punto.y);
  await pagina.waitForTimeout(700);
  const lectura = await leerTarjeta(punto);
  if (lectura.hayTarjeta) {
    puntoPulsado = punto;
    medida = lectura;
    if (lectura.procesos > 0) break;
  }
}

const conTarjeta = medida?.hayTarjeta ? await contarMapa() : { mapa: 0, total: base.total };
const perdidos = base.mapa - conTarjeta.mapa;
const porcentajePerdido = base.mapa ? Math.round((perdidos / base.mapa) * 100) : 100;

// Se arrastra el mapa: la tarjeta tiene que seguir a su municipio, no quedarse clavada.
let posicionTrasArrastrar = null;
if (medida?.hayTarjeta && marco) {
  const desdeX = Math.round(marco.left + marco.width * 0.7);
  const desdeY = Math.round(marco.top + marco.height * 0.7);
  await pagina.mouse.move(desdeX, desdeY);
  await pagina.mouse.down();
  for (let i = 1; i <= 15; i += 1) {
    await pagina.mouse.move(desdeX - i * 8, desdeY - i * 5);
    await pagina.waitForTimeout(20);
  }
  await pagina.mouse.up();
  await pagina.waitForTimeout(1500);
  posicionTrasArrastrar = await pagina.evaluate(() => {
    const t = document.querySelector('[role="dialog"][data-lado]');
    if (!t) return null;
    const r = t.getBoundingClientRect();
    return { izquierda: Math.round(r.left), arriba: Math.round(r.top) };
  });
}

medida = medida || { hayTarjeta: false, velos: [], procesos: 0, enlacesAFicha: 0 };

console.log('');
console.log(`  sitio:                          ${BASE}/mapa`);
console.log(`  departamento:                   ${DEPARTAMENTO}  (${cuantosRotulos} municipios rotulados, ${probados} pulsados)`);
console.log(`  municipio abierto:              ${medida.titulo || '(ninguno)'}  en ${puntoPulsado ? `${puntoPulsado.x},${puntoPulsado.y}` : '-'}`);
console.log(`  tarjeta:                        ${medida.hayTarjeta ? `si, lado ${medida.lado}` : 'NO'}`);
console.log(`  tamano:                         ${medida.ancho} x ${medida.alto} px  en ${medida.izquierda},${medida.arriba}`);
console.log(`  procesos listados:              ${medida.procesos}  (${medida.enlacesAFicha} con enlace a ficha completa)`);
console.log(`  puntos de mapa sin la tarjeta:  ${base.mapa} de ${base.total}`);
console.log(`  puntos de mapa con la tarjeta:  ${conTarjeta.mapa}  ->  le quita ${perdidos} (${porcentajePerdido} %)`);
console.log(`  velos a pantalla completa:      ${medida.velos.length ? medida.velos.join(' | ') : 'ninguno'}`);
console.log(`  tras arrastrar el mapa:         ${posicionTrasArrastrar ? `${posicionTrasArrastrar.izquierda},${posicionTrasArrastrar.arriba}` : '(sin medir)'}`);
console.log('');

const fallos = [];
if (!cuantosRotulos) fallos.push('No hay municipios rotulados: no se pudo pulsar ninguno y la medicion no vale.');
if (!medida.hayTarjeta) fallos.push('Pulsar un municipio no abrio la tarjeta de procesos.');
if (medida.ancho > ANCHO_MAXIMO) {
  fallos.push(`La tarjeta mide ${medida.ancho} px de ancho. Se pidio pequena; el tope es ${ANCHO_MAXIMO}.`);
}
if (medida.velos.length) {
  fallos.push(`Volvio el velo a pantalla completa sobre el mapa: ${medida.velos[0]}`);
}
if (porcentajePerdido > PERDIDA_MAXIMA) {
  fallos.push(
    `Abrir la tarjeta le quita al usuario el ${porcentajePerdido} % del mapa visible (tope ${PERDIDA_MAXIMA} %).\n` +
      '  La queja original era exactamente esta: la ventana emergente quitaba el mapa.',
  );
}
if (medida.tapaElPunto) fallos.push('La tarjeta se pinta ENCIMA del municipio que se acaba de pulsar.');
if (medida.hayTarjeta && !medida.dentroDelViewport) fallos.push('La tarjeta se sale del viewport.');
if (medida.diceSerModal) {
  fallos.push('La tarjeta declara `aria-modal="true"` y el mapa sigue vivo detras: le miente al lector de pantalla.');
}
if (medida.procesos > 0 && medida.enlacesAFicha === 0) {
  console.warn(
    `  aviso: ${medida.procesos} procesos listados y ninguno ofrece ficha completa.\n` +
      '  Solo Escuelas y Festivales tienen pagina enrutada; en las otras capas es lo esperado.',
  );
}
if (medida.hayTarjeta && medida.procesos === 0) {
  console.warn(`  aviso: ninguno de los ${probados} municipios pulsados tiene procesos en la capa activa.`);
  console.warn('  El listado y el enlace a ficha completa NO quedaron comprobados en esta pasada.');
}
if (
  posicionTrasArrastrar &&
  posicionTrasArrastrar.izquierda === medida.izquierda &&
  posicionTrasArrastrar.arriba === medida.arriba
) {
  fallos.push('Al arrastrar el mapa la tarjeta se quedo clavada: ya no senala a su municipio.');
}

if (fallos.length) {
  for (const f of fallos) console.error('✗ ' + f);
  console.error('');
  process.exitCode = 1;
} else {
  console.log('✓ La tarjeta sale pequena al lado del punto, lista los procesos y deja el mapa a la vista.');
  console.log('');
}

await navegador.close();
