import { test, expect, type Page } from '@playwright/test';

/**
 * Que ESLint se calle no prueba que el sitio se pueda usar con el teclado.
 * Prueba que ya no hay avisos. Estas pruebas comprueban las tres cosas que un
 * aviso silenciado no garantiza:
 *
 *   1. que el elemento esté en el recorrido del tabulador,
 *   2. que Enter haga lo mismo que el ratón,
 *   3. que se vea dónde está el foco (WCAG 2.4.7).
 *
 * Sin la tercera, alguien puede tabular por el sitio entero sin saber nunca
 * dónde está parado, que es tan inutilizable como no poder tabular.
 */

/** Tabula hasta dar con un elemento que cumpla el predicado, o se rinde. */
async function tabularHasta(
  page: Page,
  cumple: (info: { etiqueta: string; rol: string; nombre: string }) => boolean,
  maxTabs = 60,
) {
  for (let i = 0; i < maxTabs; i++) {
    await page.keyboard.press('Tab');
    const info = await page.evaluate(() => {
      const el = document.activeElement as HTMLElement | null;
      if (!el) return null;
      return {
        etiqueta: el.tagName.toLowerCase(),
        rol: el.getAttribute('role') ?? '',
        nombre: (el.getAttribute('aria-label') ?? el.textContent ?? '').trim().slice(0, 80),
      };
    });
    if (info && cumple(info)) return { ...info, tabs: i + 1 };
  }
  return null;
}

/** ¿Hay alguna señal visual de foco? Las utilidades `ring` pintan un box-shadow. */
async function focoVisible(page: Page) {
  return page.evaluate(() => {
    const el = document.activeElement as HTMLElement | null;
    if (!el) return { hay: false, detalle: 'no hay elemento con foco' };
    const e = getComputedStyle(el);
    const sombra = e.boxShadow && e.boxShadow !== 'none';
    const contorno = e.outlineStyle !== 'none' && parseFloat(e.outlineWidth || '0') > 0;
    return { hay: sombra || contorno, detalle: `box-shadow: ${e.boxShadow} · outline: ${e.outlineWidth} ${e.outlineStyle}` };
  });
}

test('las noticias se abren con el teclado, y se ve dónde está el foco', { tag: '@backend' }, async ({ page }) => {
  await page.goto('/noticias');
  await page.waitForLoadState('networkidle');
  await page.locator('body').click({ position: { x: 2, y: 2 } });

  const tarjeta = await tabularHasta(page, (i) => i.etiqueta === 'article' && i.rol === 'button');
  expect(tarjeta, 'ninguna tarjeta de noticia aparece en el recorrido del tabulador').not.toBeNull();
  console.log(`  tarjeta alcanzada tras ${tarjeta!.tabs} tabulaciones: «${tarjeta!.nombre}»`);

  const foco = await focoVisible(page);
  expect(foco.hay, `la tarjeta con foco no muestra ninguna señal visible — ${foco.detalle}`).toBe(true);

  const antes = await page.locator('h1, h2').first().textContent();
  await page.keyboard.press('Enter');
  await expect
    .poll(async () => (await page.locator('h1, h2').first().textContent()) !== antes, { timeout: 8_000 })
    .toBe(true);
});

test('las tarjetas de eje del Home se activan con Enter', async ({ page }) => {
  await page.goto('/');
  await page.waitForLoadState('networkidle');
  await page.locator('body').click({ position: { x: 2, y: 2 } });

  const tarjeta = await tabularHasta(page, (i) => i.rol === 'link' && /MÚSICA PARA LA VIDA/i.test(i.nombre), 120);
  expect(tarjeta, 'la tarjeta del eje 1 no aparece en el recorrido del tabulador').not.toBeNull();

  const foco = await focoVisible(page);
  expect(foco.hay, `la tarjeta con foco no muestra señal visible — ${foco.detalle}`).toBe(true);

  await page.keyboard.press('Enter');
  await expect.poll(() => page.url(), { timeout: 8_000 }).toContain('/ejes');
});

test('los hitos de «Sobre el PNMC» se despliegan con el teclado y dicen si están abiertos', async ({ page }) => {
  await page.goto('/pnmc');
  await page.waitForLoadState('networkidle');

  const hitos = page.locator('[role="button"][aria-expanded]');
  await expect(hitos.first()).toBeVisible();

  // aria-expanded es lo que un lector de pantalla anuncia. Si no cambia al
  // activarlo, la persona no tiene forma de saber que algo se abrió.
  const primero = hitos.first();
  const inicial = await primero.getAttribute('aria-expanded');
  const otro = hitos.nth(1);
  await otro.focus();
  await page.keyboard.press('Enter');

  await expect.poll(() => otro.getAttribute('aria-expanded'), { timeout: 5_000 }).toBe('true');
  await expect.poll(() => primero.getAttribute('aria-expanded'), { timeout: 5_000 }).not.toBe(inicial === 'true' ? 'true' : null);
});
