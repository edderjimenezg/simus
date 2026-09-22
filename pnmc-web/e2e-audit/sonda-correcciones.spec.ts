import { test, expect } from '@playwright/test';

/**
 * Sonda dirigida: mide SOLO lo que se corrigió, con los valores previos
 * escritos como umbral. A diferencia del barrido, esta SÍ falla — es la prueba
 * de que el arreglo hizo algo, no la observación de que existe.
 *
 * Valores medidos ANTES de corregir (barrido del 2026-08-21, movil-360):
 *   - banner de medios: contenedor interno de altura 0 px, nada renderizado
 *   - puntos del carrusel: 4 x 6 px (la clase w-1.5 no se aplicaba)
 *   - descripcion de la tarjeta de estrategia: inalcanzable con teclado
 */

test('el banner de medios se dibuja y ocupa alto real', async ({ page }) => {
  await page.goto('/', { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(3000);

  const medida = await page.evaluate(() => {
    const host = document.querySelector('app-home-media-banner');
    if (!host) return { existe: false, altoHost: 0, altoInterno: 0, imagenes: 0, tituloVisible: false };
    const interno = host.firstElementChild as HTMLElement | null;
    const img = host.querySelector('img') as HTMLImageElement | null;
    const h3 = host.querySelector('h3');
    const rH3 = h3?.getBoundingClientRect();
    return {
      existe: true,
      altoHost: Math.round(host.getBoundingClientRect().height),
      altoInterno: interno ? Math.round(interno.getBoundingClientRect().height) : 0,
      anchoImagen: img ? Math.round(img.getBoundingClientRect().width) : 0,
      altoImagen: img ? Math.round(img.getBoundingClientRect().height) : 0,
      imagenes: host.querySelectorAll('img').length,
      tituloVisible: !!rH3 && rH3.height > 0 && rH3.width > 0,
    };
  });

  console.log('  banner de medios:', JSON.stringify(medida));
  expect(medida.existe, 'el componente debe estar en el DOM').toBe(true);
  expect(medida.altoInterno, 'ANTES era 0 px: la caja interna colapsaba').toBeGreaterThan(100);
  expect(medida.altoImagen, 'la fotografia debe tener alto real').toBeGreaterThan(100);
  expect(medida.tituloVisible, 'el titulo de la diapositiva debe ocupar espacio').toBe(true);
});

test('los puntos del carrusel alcanzan el objetivo tactil de 24 px', async ({ page }) => {
  await page.goto('/', { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(3000);

  const puntos = await page.evaluate(() => {
    const botones = Array.from(
      document.querySelectorAll('app-home-strategies-section button[aria-label^="Ir al proceso"]'),
    );
    return botones.map((b) => {
      const r = b.getBoundingClientRect();
      const punto = b.querySelector('span');
      const rp = punto?.getBoundingClientRect();
      return {
        ancho: Math.round(r.width),
        alto: Math.round(r.height),
        anchoPunto: rp ? Math.round(rp.width) : 0,
        clasesPunto: punto?.className || '',
        nombre: b.getAttribute('aria-label'),
      };
    });
  });

  console.log('  puntos del carrusel:', JSON.stringify(puntos.slice(0, 3)));
  expect(puntos.length, 'debe haber indicadores').toBeGreaterThan(0);
  for (const p of puntos) {
    expect(p.ancho, 'ANTES median 4 px de ancho').toBeGreaterThanOrEqual(24);
    expect(p.alto, 'ANTES median 6 px de alto').toBeGreaterThanOrEqual(24);
    expect(p.nombre, 'cada indicador necesita nombre accesible').toBeTruthy();
  }
  // la clase con punto decimal ya no debe aparecer sin aplicarse
  const noSeleccionados = puntos.filter((p) => !p.clasesPunto.includes('w-6'));
  for (const p of noSeleccionados) {
    expect(p.anchoPunto, 'el punto visual debe medir 6 px (w-[6px]), no 4').toBeGreaterThanOrEqual(6);
  }
});

test('la descripcion de la tarjeta de estrategia es alcanzable con teclado', async ({ page }) => {
  await page.goto('/', { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(3000);

  const antes = await page.evaluate(() => {
    const panel = document.querySelector(
      'app-home-strategies-section [role="link"] .max-h-0',
    ) as HTMLElement | null;
    return panel ? Math.round(panel.getBoundingClientRect().height) : -1;
  });

  // enfocar la primera tarjeta como haria el tabulador
  await page.evaluate(() => {
    const tarjeta = document.querySelector('app-home-strategies-section [role="link"]') as HTMLElement | null;
    tarjeta?.focus();
  });
  await page.waitForTimeout(900);

  const despues = await page.evaluate(() => {
    const tarjeta = document.querySelector('app-home-strategies-section [role="link"]');
    const panel = tarjeta?.querySelector('div.mt-3') as HTMLElement | null;
    const parrafo = panel?.querySelector('p:not([aria-hidden])') as HTMLElement | null;
    return {
      altoPanel: panel ? Math.round(panel.getBoundingClientRect().height) : -1,
      altoParrafo: parrafo ? Math.round(parrafo.getBoundingClientRect().height) : -1,
      enfocada: document.activeElement === tarjeta,
    };
  });

  console.log('  panel en reposo:', antes, '| tras enfocar:', JSON.stringify(despues));
  expect(despues.enfocada, 'la tarjeta debe poder recibir el foco').toBe(true);
  expect(
    despues.altoPanel,
    'ANTES el panel seguia colapsado con el foco puesto: group-hover no responde al foco',
  ).toBeGreaterThan(40);
});

test('el titulo de la tarjeta no se anuncia dos veces', async ({ page }) => {
  await page.goto('/', { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(3000);

  const dup = await page.evaluate(() => {
    const tarjeta = document.querySelector('app-home-strategies-section [role="link"]');
    if (!tarjeta) return { encabezados: -1, textosRepetidos: -1 };
    const enc = Array.from(tarjeta.querySelectorAll('h1,h2,h3,h4,h5,h6')).filter(
      (h) => !h.closest('[aria-hidden="true"]') && h.getAttribute('aria-hidden') !== 'true',
    );
    const textos = enc.map((h) => (h.textContent || '').trim());
    const repetidos = textos.length - new Set(textos).size;
    return { encabezados: enc.length, textosRepetidos: repetidos };
  });

  console.log('  encabezados accesibles por tarjeta:', JSON.stringify(dup));
  expect(dup.textosRepetidos, 'ANTES el mismo titulo aparecia en dos h3 de la misma tarjeta').toBe(0);
});
