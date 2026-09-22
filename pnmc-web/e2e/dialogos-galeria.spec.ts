import { test, expect, type Page } from '@playwright/test';

/**
 * Sonda desechable: comprueba que los dos visores de la galeria se comportan
 * como dialogos de verdad, y sobre todo que sus dos trampas de foco NO se
 * pelean cuando quedan montadas a la vez (el visor se abre ENCIMA del album).
 *
 * Va contra el entorno vivo porque la galeria necesita la API para tener
 * albumes: sin datos no hay nada que abrir. Solo navega y lee.
 */

async function foco(page: Page) {
  return page.evaluate(() => {
    const el = document.activeElement as HTMLElement | null;
    if (!el) return null;
    return {
      etiqueta: el.tagName.toLowerCase(),
      nombre: (el.getAttribute('aria-label') ?? el.textContent ?? '').trim().slice(0, 40),
      dentroDeDialogo: !!el.closest('[role="dialog"]'),
      rotuloDelDialogo: el.closest('[role="dialog"]')?.getAttribute('aria-label') ?? null,
    };
  });
}

test('el overlay de album es un dialogo, atrapa el foco y lo devuelve', { tag: '@backend' }, async ({ page }) => {
  await page.goto('/galeria', { waitUntil: 'networkidle' });

  const tarjeta = page.locator('button, [role="button"]').filter({ hasText: /ver|album|explorar/i }).first();
  await expect(tarjeta).toBeVisible({ timeout: 15_000 });

  // Se recuerda quien abre, para comprobar despues que el foco vuelve aqui.
  await tarjeta.focus();
  const abridor = await foco(page);
  console.log('  abre:', JSON.stringify(abridor));

  await tarjeta.press('Enter');

  const dialogo = page.locator('[role="dialog"][aria-label="Detalle del album"]');
  await expect(dialogo).toBeVisible({ timeout: 10_000 });
  await expect(dialogo).toHaveAttribute('aria-modal', 'true');

  // El foco tiene que haber entrado solo.
  const trasAbrir = await foco(page);
  console.log('  tras abrir:', JSON.stringify(trasAbrir));
  expect(trasAbrir?.dentroDeDialogo, 'el foco deberia estar dentro del dialogo').toBe(true);

  // Tabular muchas veces sin salirse nunca.
  let fugas = 0;
  for (let i = 0; i < 40; i++) {
    await page.keyboard.press('Tab');
    const f = await foco(page);
    if (!f?.dentroDeDialogo) {
      fugas++;
      console.log(`  FUGA en el tab ${i + 1}: ${JSON.stringify(f)}`);
    }
  }
  expect(fugas, 'el foco se escapo del dialogo').toBe(0);

  await page.keyboard.press('Escape');
  await expect(dialogo).toBeHidden({ timeout: 5_000 });

  const trasCerrar = await foco(page);
  console.log('  tras cerrar:', JSON.stringify(trasCerrar));
  expect(trasCerrar?.nombre, 'el foco deberia volver al control que abrio').toBe(abridor?.nombre);
});

test('con el visor abierto ENCIMA del album, las dos trampas no se pelean', { tag: '@backend' }, async ({ page }) => {
  await page.goto('/galeria', { waitUntil: 'networkidle' });

  const tarjeta = page.locator('button, [role="button"]').filter({ hasText: /ver|album|explorar/i }).first();
  await expect(tarjeta).toBeVisible({ timeout: 15_000 });
  await tarjeta.press('Enter');

  const album = page.locator('[role="dialog"][aria-label="Detalle del album"]');
  await expect(album).toBeVisible({ timeout: 10_000 });

  // Abrir una foto desde dentro del album: quedan DOS dialogos montados.
  // Se pulsa el <button> que envuelve la miniatura, no la <img>: el manejador
  // (click)="openLightbox(...)" vive en el boton (galeria-page.component.html:645-647).
  const foto = album.locator('button').filter({ has: album.page().locator('img, video') }).last();
  await expect(foto).toBeVisible({ timeout: 10_000 });
  await foto.click();

  const visor = page.locator('[role="dialog"][aria-label="Visor de fotografia"]');
  await expect(visor).toBeVisible({ timeout: 10_000 });

  const montados = await page.locator('[role="dialog"]').count();
  console.log(`  dialogos montados a la vez: ${montados}`);

  // Con los dos montados, el foco debe quedarse en el de arriba.
  const dentro = await foco(page);
  console.log('  foco con los dos abiertos:', JSON.stringify(dentro));
  expect(dentro?.rotuloDelDialogo, 'el foco deberia estar en el visor, no en el album').toBe(
    'Visor de fotografia',
  );

  let fugasAlAlbum = 0;
  for (let i = 0; i < 25; i++) {
    await page.keyboard.press('Tab');
    const f = await foco(page);
    if (f?.rotuloDelDialogo !== 'Visor de fotografia') {
      fugasAlAlbum++;
      console.log(`  FUGA en el tab ${i + 1}: ${JSON.stringify(f)}`);
    }
  }
  expect(fugasAlAlbum, 'el foco se fue del visor al album de detras').toBe(0);

  // Al cerrar el visor, el foco debe volver DENTRO del album, no a la pagina.
  await page.keyboard.press('Escape');
  await expect(visor).toBeHidden({ timeout: 5_000 });
  await expect(album).toBeVisible();

  const trasCerrarVisor = await foco(page);
  console.log('  tras cerrar el visor:', JSON.stringify(trasCerrarVisor));
  expect(trasCerrarVisor?.rotuloDelDialogo, 'el foco deberia volver al album').toBe(
    'Detalle del album',
  );
});
