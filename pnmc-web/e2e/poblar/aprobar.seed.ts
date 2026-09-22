import { test, expect, Page } from '@playwright/test';
import { FESTIVALES } from './datos';

/**
 * Publica desde la consola los quince Festivales que el conjunto marca como `publicado`.
 *
 * SE HACE POR LA CONSOLA Y NO POR SQL, por lo mismo que el sembrado: un `UPDATE` deja el estado
 * puesto y no comprueba nada. Aquí se pulsa lo que pulsa un funcionario, así que si la decisión no
 * se registra —o la registra sin escribir historial— sale aquí.
 *
 * SE CORRE DESPUÉS DE `poblar.seed.ts`:
 *
 *   npx playwright test -c playwright.poblar.config.ts e2e/poblar/aprobar.seed.ts
 */

const CAPTURAS = 'e2e/capturas/poblar';
const FUNCIONARIO = {
  email: process.env.PNMC_ADMIN_EMAIL ?? 'admin@pnmc.local',
  password: process.env.PNMC_ADMIN_PASSWORD ?? 'admin',
};

const HALLAZGOS: string[] = [];
let paso = 500;

async function foto(page: Page, nombre: string): Promise<void> {
  paso += 1;
  await page.screenshot({ path: `${CAPTURAS}/${paso}-${nombre}.png`, animations: 'disabled' });
}

/**
 * Entra a la consola por el formulario.
 *
 * SE MARCA EL TOUR COMO VISTO: la consola lo enseña la primera vez de cada cuenta y su fondo cubre
 * la pantalla entera. Una persona lo cierra una vez; el navegador de la prueba estrena perfil.
 */
async function entrarALaConsola(page: Page): Promise<void> {
  await page.addInitScript(email => {
    window.localStorage.setItem(`pnmc_tour_seen_${email}`, '1');
  }, FUNCIONARIO.email);

  await page.goto('/administracion');
  await page.locator('input[type="email"]').fill(FUNCIONARIO.email);
  await page.locator('input[type="password"]').fill(FUNCIONARIO.password);
  await page.getByRole('button', { name: 'Entrar al panel' }).click();
  await page.getByRole('button', { name: 'Revisión', exact: true }).click();
  await expect(page.locator('app-admin-festival-review-panel'))
    .toContainText('Decisiones institucionales', { timeout: 120000 });
}

/**
 * Publica un Festival concreto desde su fila de la bandeja.
 *
 * SE ESPERA A LA FILA, NO SE CUENTA. `count()` devuelve lo que haya en ese instante, y la bandeja
 * se recarga entera después de cada decisión: preguntando sin esperar, ocho de quince Festivales
 * daban «no está en la bandeja» estando en ella, y solo siete se publicaron.
 */
async function publicar(page: Page, nombre: string): Promise<boolean> {
  const fila = page.locator('article').filter({ hasText: nombre }).first();
  try {
    await expect(fila).toBeVisible({ timeout: 120000 });
  } catch {
    HALLAZGOS.push(`${nombre}: no está en la bandeja de revisión`);
    return false;
  }
  await fila.scrollIntoViewIfNeeded();

  // El botón que abre el resumen de doce campos y su formulario de decisión.
  await fila.locator('[data-testid^="decidir-"]').click();
  await expect(fila.locator('input[name="accion"][value="Publicar"]')).toBeVisible({ timeout: 60000 });
  await fila.locator('input[name="accion"][value="Publicar"]').check();

  // «Publicar» es irreversible desde el panel y por eso pide confirmación en el .ts.
  page.once('dialog', dialogo => void dialogo.accept());
  await fila.getByRole('button', { name: 'Registrar decisión' }).click();

  await expect(page.getByText('La decisión institucional fue registrada')).toBeVisible({ timeout: 120000 });
  await expect(page.locator('article').filter({ hasText: nombre })).toHaveCount(0, { timeout: 120000 });
  return true;
}

test('publicar desde la consola los quince Festivales aprobados', async ({ page }) => {
  const aPublicar = FESTIVALES.filter(f => f.destino === 'publicado').map(f => f.nombre);
  expect(aPublicar.length, 'el conjunto tiene que traer quince Festivales para publicar').toBe(15);

  await entrarALaConsola(page);
  await foto(page, 'bandeja-antes');

  // LA TRAZA DECÍA «publicado» SIEMPRE, hubiera pasado o no: estaba fuera del resultado de
  // `publicar`. Quince líneas verdes con siete publicaciones reales.
  for (const nombre of aPublicar) {
    const salio = await publicar(page, nombre);
    console.log(`${salio ? 'publicado' : 'NO se pudo'}: ${nombre}`);
  }

  await foto(page, 'bandeja-despues');
  if (HALLAZGOS.length > 0) console.log('HALLAZGOS:\n' + HALLAZGOS.map(h => '  - ' + h).join('\n'));
  expect(HALLAZGOS, HALLAZGOS.join('\n')).toEqual([]);
});
