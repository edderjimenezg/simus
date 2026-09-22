/**
 * Censo visual del Espacio de Gestión Administrativa.
 *
 * Recorre todas las secciones y anota, por sección, cuántos tratamientos distintos hay para el
 * mismo papel —título, pestaña, tabla, botón— y qué textos parecen notas internas. Es la base de la
 * auditoría: los números salen de la pantalla, no de una lectura.
 *
 * Se ejecuta desde `pnmc-web/` con la pila local levantada:  node tools/censo-de-la-consola.mjs
 */
import { chromium } from 'playwright';
import { writeFileSync } from 'node:fs';

const BASE = 'http://localhost:4300';
const SALIDA = process.argv[2] || '/tmp/censo-consola';
const nav = await chromium.launch();
const p = await (await nav.newContext({ viewport: { width: 1500, height: 950 } })).newPage();
await p.goto(BASE + '/administracion', { waitUntil: 'networkidle' });
await p.fill('#admin-email', 'admin@pnmc.local'); await p.fill('#admin-password', 'admin');
await p.click('button[type=submit]'); await p.waitForTimeout(4000);

const firma = (el) => {
  const s = getComputedStyle(el);
  return `${s.fontFamily.split(',')[0].replace(/"/g, '')} ${s.fontSize} ${s.fontWeight} ${s.textTransform === 'uppercase' ? 'MAY' : 'min'} ${s.color}`;
};
const censar = () => {
  const dentro = document.querySelector('.foundation-main') || document.body;
  const cuenta = (lista) => { const m = new Map(); for (const f of lista) m.set(f, (m.get(f) || 0) + 1); return [...m.entries()].sort((a, b) => b[1] - a[1]); };
  const visibles = sel => [...dentro.querySelectorAll(sel)].filter(e => e.getClientRects().length);
  const firma = (el) => { const s = getComputedStyle(el); return `${s.fontFamily.split(',')[0].replace(/"/g, '')} ${s.fontSize} ${s.fontWeight} ${s.textTransform === 'uppercase' ? 'MAY' : 'min'} ${s.color}`; };
  const firmaBoton = (el) => { const s = getComputedStyle(el); const r = el.getBoundingClientRect(); return `${Math.round(r.height)}px ${s.fontSize} ${s.fontWeight} ${s.textTransform === 'uppercase' ? 'MAY' : 'min'} radio:${s.borderTopLeftRadius} fondo:${s.backgroundColor} borde:${s.borderTopColor}`; };
  const pestanas = visibles('[role="tab"], [aria-selected], .tab, [class*="pestana"], [class*="tab-"], app-selector-segmentado button');
  const tablas = visibles('table');
  const tablaFirma = (t) => { const th = t.querySelector('th'); const td = t.querySelector('td'); const fila = t.querySelector('tbody tr'); return `th:${th ? firma(th) : '—'} | td:${td ? getComputedStyle(td).fontSize + ' ' + getComputedStyle(td).color : '—'} | fila:${fila ? Math.round(fila.getBoundingClientRect().height) + 'px' : '—'}`; };
  const notas = [];
  const w = document.createTreeWalker(dentro, NodeFilter.SHOW_TEXT);
  let n;
  while ((n = w.nextNode())) {
    const t = (n.textContent || '').replace(/\s+/g, ' ').trim();
    if (t.length < 12 || !n.parentElement?.getClientRects().length) continue;
    // `TODO` y `FIXME` SE BUSCAN EN MAYUSCULAS Y APARTE. Con la bandera `i` sobre el resto del
    // patrón, «TODO» casaba con la palabra española «todo»: marcó como
    // nota interna la descripción de Solicitudes, que empieza por «Todo lo que espera una
    // decisión». Un instrumento de medida que da falsos positivos hace dudar de los verdaderos.
    if (/\b(TODO|FIXME)\b/.test(t) ||
        /\b(mock|simulad|provisional|temporal(mente)?|de prueba|en desarrollo|pendiente de implementar|próximamente|placeholder|corte \d+|septiembre de 2026|agosto de 2026|componente|endpoint|backend|frontend|spec\b|el diseño aprobado del portal)\b/i.test(t))
      notas.push(t.slice(0, 110));
  }
  return {
    titulos: cuenta(visibles('h1, h2, h3, h4').map(firma)),
    pestanas: cuenta(pestanas.map(firma)), nPestanas: pestanas.length,
    tablas: tablas.map(tablaFirma), nTablas: tablas.length,
    botones: cuenta(visibles('button, a[class*="rounded"]').filter(e => e.textContent.trim().length > 0 && !e.closest('.foundation-sidebar')).map(firmaBoton)),
    tarjetas: visibles('[class*="rounded-2xl"], [class*="rounded-xl"], [class*="shadow"]').length,
    notas: [...new Set(notas)],
    cabecera: (() => { const h = document.querySelector('.foundation-header'); const s = h ? getComputedStyle(h) : null; return h ? { alto: Math.round(h.getBoundingClientRect().height), posicion: s.position } : null; })(),
  };
};

const resultados = {};
const secciones = await p.locator('.foundation-sidebar button').allTextContents();
for (let i = 0; i < secciones.length; i++) {
  const nombre = secciones[i].replace(/\s+/g, ' ').trim();
  await p.locator('.foundation-sidebar button').nth(i).click().catch(() => {});
  await p.mouse.move(2, 2); await p.waitForTimeout(2200);
  resultados[nombre] = await p.evaluate(censar);
  resultados[nombre].ruta = p.url().replace(BASE, '');
  if (/Resumen operativo|Solicitudes|Calidad|Festivales|Organizaciones/.test(nombre))
    await p.screenshot({ path: `${SALIDA}/${nombre.replace(/[^a-z0-9]+/gi, '-').toLowerCase()}.png`, fullPage: true });
}
writeFileSync(`${SALIDA}/censo.json`, JSON.stringify(resultados, null, 1));

// Resumen legible.
const todasTitulos = new Map(), todasPestanas = new Map(), todosBotones = new Map();
for (const [sec, r] of Object.entries(resultados)) {
  for (const [f, c] of r.titulos) todasTitulos.set(f, (todasTitulos.get(f) || 0) + c);
  for (const [f, c] of r.pestanas) todasPestanas.set(f, (todasPestanas.get(f) || 0) + c);
  for (const [f, c] of r.botones) todosBotones.set(f, (todosBotones.get(f) || 0) + c);
}
console.log('SECCIONES RECORRIDAS:', Object.keys(resultados).length);
console.log('CABECERA:', JSON.stringify(Object.values(resultados)[0]?.cabecera));
console.log('\nFIRMAS DISTINTAS EN TODA LA CONSOLA  ·  títulos:', todasTitulos.size, ' pestañas:', todasPestanas.size, ' botones:', todosBotones.size);
console.log('\nPOR SECCIÓN  (títulos / pestañas / tablas / botones / tarjetas / notas):');
for (const [sec, r] of Object.entries(resultados))
  console.log(`  ${sec.padEnd(34)} ${String(r.titulos.length).padStart(2)} / ${String(r.pestanas.length).padStart(2)} (${r.nPestanas}) / ${String(r.nTablas).padStart(2)} / ${String(r.botones.length).padStart(2)} / ${String(r.tarjetas).padStart(3)} / ${r.notas.length}`);
console.log('\nNOTAS QUE PARECEN INTERNAS:');
for (const [sec, r] of Object.entries(resultados)) for (const t of r.notas) console.log(`  [${sec}] ${t}`);
await nav.close();
