import { test, Page } from '@playwright/test';

/**
 * Mira el resultado del sembrado y deja capturas de las pantallas que tienen que haber cambiado.
 *
 * NO COMPRUEBA NADA POR SÍ MISMO: la comprobación es leer las imágenes. Un recuento en la base dice
 * que hay quince Festivales publicados; solo la pantalla dice si se ven.
 */

const CAPTURAS = 'e2e/capturas/poblar/final';

async function mirar(page: Page, ruta: string, nombre: string, espera = 8000): Promise<void> {
  await page.goto(ruta, { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(espera);
  await page.screenshot({ path: `${CAPTURAS}/${nombre}.png`, animations: 'disabled', fullPage: false });
}

test('mirar el sitio con los Festivales sembrados', async ({ page }) => {
  await mirar(page, '/ecosistema/festivales', '1-consulta-publica');

  // EL GEOVISOR ABRE CON SU TUTORIAL ENCIMA y tapa el mapa entero. Una persona lo cierra una vez;
  // el navegador de esta corrida estrena perfil y lo ve siempre.
  await page.goto('/mapa', { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(6000);
  const cerrarTutorial = page.getByRole('button', { name: /cerrar|saltar|omitir/i }).first();
  if (await cerrarTutorial.count() > 0) await cerrarTutorial.click().catch(() => {});
  await page.keyboard.press('Escape');
  await page.waitForTimeout(9000);
  await page.screenshot({ path: `${CAPTURAS}/2-mapa.png`, animations: 'disabled' });

  await page.addInitScript(() => window.localStorage.setItem('pnmc_tour_seen_admin@pnmc.local', '1'));
  await page.goto('/administracion');
  await page.locator('input[type="email"]').fill('admin@pnmc.local');
  await page.locator('input[type="password"]').fill('admin');
  await page.getByRole('button', { name: 'Entrar al panel' }).click();
  await page.waitForTimeout(6000);
  await page.screenshot({ path: `${CAPTURAS}/3-consola-inicio.png`, animations: 'disabled' });

  await page.getByRole('button', { name: 'Revisión', exact: true }).click();
  await page.waitForTimeout(6000);
  await page.screenshot({ path: `${CAPTURAS}/4-bandeja-revision.png`, animations: 'disabled' });
});
