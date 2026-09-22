#!/usr/bin/env node
/**
 * Comprueba las dos columnas laterales del geovisor: la de controles y la de lectura.
 *
 * QUÉ SE PIDIÓ EL 29 DE AGOSTO DE 2026: *
 *   «elimina esto, solo deja los filtros»  — sobre la columna izquierda.
 *   «este pásalo arriba como una pestaña independiente, en una pestaña que solo quede
 *    el directorio aquí»  — sobre la llamada «Ser parte del ecosistema».
 *
 * POR QUÉ ESTO NO ES UNA PRUEBA UNITARIA. Lo que estaba mal no era una función: era
 * DÓNDE caía un bloque una vez apilado debajo de una lista que crece con los datos.
 * Medido antes del cambio a 1280x639, el borde superior de «Ser parte del ecosistema»
 * estaba en y=1498, con la columna empezando en y=144: mil trescientos cincuenta píxeles
 * por debajo del pliegue, después de las tarjetas del directorio. Ninguna prueba de
 * signals ve eso; hay que medir el rectángulo en pantalla.
 *
 * LAS TRES REGLAS:
 *   1. La columna izquierda no vuelve a llevar prosa de cabecera ni el pie de
 *      «Cobertura / Lectura activa», y sí lleva sus tres desplegables.
 *   2. La columna derecha tiene dos pestañas y NUNCA enseña las dos a la vez.
 *   3. Con su pestaña abierta, «Ser parte del ecosistema» se ve sin desplazar: su borde
 *      superior cae dentro de la columna.
 *
 *   npm run mapa:paneles
 *   node tools/comprobar-paneles.mjs http://127.0.0.1:4300
 *
 * Necesita el sitio levantado. Corre al viewport del usuario, 1280x639, porque el
 * defecto que cierra solo se manifiesta cuando la columna es corta.
 */

import { chromium } from 'playwright';

const BASE = process.argv[2] || 'http://127.0.0.1:4300';

const navegador = await chromium.launch({
  args: ['--use-gl=swiftshader', '--enable-unsafe-swiftshader'],
});
const pagina = await navegador.newPage({ viewport: { width: 1280, height: 639 } });

await pagina.goto(`${BASE}/mapa`, { waitUntil: 'networkidle', timeout: 120000 });
await pagina.waitForTimeout(11000);
try {
  await pagina.locator('[aria-label*="errar"], .tutorial-close').first().click({ timeout: 4000 });
} catch {
  await pagina.keyboard.press('Escape');
}
await pagina.waitForTimeout(1800);

const visible = (sel) =>
  pagina.evaluate((s) => {
    const el = document.querySelector(s);
    if (!el) return null;
    return !el.hasAttribute('hidden');
  }, sel);

const leer = () =>
  pagina.evaluate(() => {
    const control = document.querySelector('aside[aria-label="Controles del geovisor"]');
    const lectura = document.querySelector('aside[aria-label="Lectura rápida del territorio"]');
    const participar = document.querySelector('#panel-participar');
    const cajaLectura = lectura?.getBoundingClientRect();
    const cajaParticipar = participar?.getBoundingClientRect();

    return {
      textoControl: (control?.textContent || '').replace(/\s+/g, ' ').trim(),
      desplegables: control ? control.querySelectorAll('select').length : 0,
      hayPestanas: Boolean(document.querySelector('#pestana-directorio') && document.querySelector('#pestana-participar')),
      seleccionadas: ['directorio', 'participar'].filter(
        (id) => document.querySelector('#pestana-' + id)?.getAttribute('aria-selected') === 'true',
      ),
      directorioVisible: Boolean(document.querySelector('#panel-directorio')) && !document.querySelector('#panel-directorio').hasAttribute('hidden'),
      participarVisible: Boolean(participar) && !participar.hasAttribute('hidden'),
      // Cuánto hay que bajar desde el borde de la columna para llegar al bloque.
      caidaDeParticipar:
        cajaLectura && cajaParticipar ? Math.round(cajaParticipar.top - cajaLectura.top) : null,
      altoDeLectura: cajaLectura ? Math.round(cajaLectura.height) : 0,
    };
  });

const antes = await leer();

// Se abre la pestaña de participar y se vuelve a medir.
if (antes.hayPestanas) {
  await pagina.locator('#pestana-participar').click();
  await pagina.waitForTimeout(700);
}
const conParticipar = await leer();

if (conParticipar.hayPestanas) {
  await pagina.locator('#pestana-directorio').click();
  await pagina.waitForTimeout(700);
}
const deVuelta = await leer();

const PROSA_FUERA = ['Lectura territorial del ecosistema musical', 'Lectura activa', 'Cobertura'];
const prosaQueVolvio = PROSA_FUERA.filter((frase) => antes.textoControl.includes(frase));

console.log('');
console.log(`  sitio:                            ${BASE}/mapa   (1280x639)`);
console.log(`  columna de controles`);
console.log(`    desplegables de filtro:         ${antes.desplegables}`);
console.log(`    prosa que debía irse:           ${prosaQueVolvio.length ? prosaQueVolvio.join(' | ') : 'ninguna'}`);
console.log(`  columna de lectura   (alto ${antes.altoDeLectura} px)`);
console.log(`    pestañas:                       ${antes.hayPestanas ? 'sí' : 'NO'}`);
console.log(`    al abrir, seleccionada:         ${antes.seleccionadas.join(', ') || '(ninguna)'}`);
console.log(`    paneles a la vista al abrir:    directorio=${antes.directorioVisible} participar=${antes.participarVisible}`);
console.log(`    con «Ser parte» abierta:        directorio=${conParticipar.directorioVisible} participar=${conParticipar.participarVisible}`);
console.log(`    caída de «Ser parte»:           ${conParticipar.caidaDeParticipar} px por debajo del borde de la columna`);
console.log(`    de vuelta en Directorio:        directorio=${deVuelta.directorioVisible} participar=${deVuelta.participarVisible}`);
console.log('');

const fallos = [];

if (antes.desplegables !== 3) {
  fallos.push(`La columna de controles tiene ${antes.desplegables} desplegables y se esperaban 3: Departamento, Territorio sonoro y Práctica.`);
}
if (prosaQueVolvio.length) {
  fallos.push(
    `Volvió a la columna de controles lo que se pidió quitar: ${prosaQueVolvio.join(', ')}.\n` +
      '  «Solo deja los filtros»: ni prosa de cabecera ni pie de cobertura.',
  );
}
if (!antes.hayPestanas) {
  fallos.push('La columna de lectura no tiene las dos pestañas; la medición no vale.');
} else {
  if (antes.seleccionadas.length !== 1 || antes.seleccionadas[0] !== 'directorio') {
    fallos.push(`Al abrir debería estar seleccionada solo «Directorio», y está: ${antes.seleccionadas.join(', ') || 'ninguna'}.`);
  }
  if (antes.directorioVisible && antes.participarVisible) {
    fallos.push('Las dos secciones se ven a la vez: es exactamente la columna apilada que se pidió partir.');
  }
  if (!conParticipar.participarVisible) {
    fallos.push('Pulsar la pestaña «Ser parte» no muestra su sección.');
  }
  if (conParticipar.directorioVisible) {
    fallos.push('Con «Ser parte» abierta el directorio sigue a la vista: las pestañas no se excluyen.');
  }
  if (!deVuelta.directorioVisible || deVuelta.participarVisible) {
    fallos.push('No se puede volver al Directorio: la pestaña no revierte.');
  }
  // El defecto original, en un número: el bloque caía 1.354 px por debajo del borde.
  if (conParticipar.caidaDeParticipar !== null && conParticipar.caidaDeParticipar > conParticipar.altoDeLectura) {
    fallos.push(
      `«Ser parte del ecosistema» empieza ${conParticipar.caidaDeParticipar} px por debajo del borde de una columna de ${conParticipar.altoDeLectura} px.\n` +
        '  Hay que desplazarse para llegar, que es el defecto que se corrigió.',
    );
  }
}

if (fallos.length) {
  for (const f of fallos) console.error('✗ ' + f);
  console.error('');
  process.exitCode = 1;
} else {
  console.log('✓ La columna de controles es solo filtros, y la de lectura parte directorio y participación en pestañas.');
  console.log('');
}

await navegador.close();
