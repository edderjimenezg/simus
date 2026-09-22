import { test, expect, type Page } from '@playwright/test';
import { RUTA_SESION } from './sesion-compartida';

/**
 * Comprueba que las capas del panel administrativo que ahora declaran
 * `role="dialog"` se comportan de verdad como dialogos.
 *
 * ## Por que vive aqui y no en `e2e/`
 *
 * Necesita sesion iniciada, y la sesion la monta el proyecto `preparacion` de
 * `playwright.audit.config.ts` una sola vez (el API limita a 30 accesos por
 * ventana). En `e2e/` cada fichero se las arregla solo y volveriamos a agotar el
 * limitador.
 *
 * ## Que NO hace, y es deliberado
 *
 * No pulsa ninguna accion que escriba. La base es compartida con otra sesion de
 * trabajo. Abrir un modal solo cambia una senal en el cliente: eso si es seguro.
 * Guardar, borrar o fusionar, no, y no se toca.
 */

// Reutiliza la cookie que dejo el proyecto `preparacion`. Sin esta linea el
// navegador abre sin sesion y /administracion muestra el formulario de acceso: la prueba
// mide la ausencia de sesion creyendo que mide el panel.
test.use({ storageState: RUTA_SESION });

async function foco(page: Page) {
  return page.evaluate(() => {
    const el = document.activeElement as HTMLElement | null;
    if (!el) return null;
    const dlg = el.closest('[role="dialog"]');
    return {
      etiqueta: el.tagName.toLowerCase(),
      nombre: (el.getAttribute('aria-label') ?? el.textContent ?? '').trim().slice(0, 40),
      rotuloDelDialogo: dlg?.getAttribute('aria-label') ?? null,
    };
  });
}

test('el editor de usuarios es un dialogo, atrapa el foco y sale con Escape', async ({ page }) => {
  await page.goto('/administracion', { waitUntil: 'domcontentloaded' });
  await expect(page.locator('aside')).toBeVisible({ timeout: 30_000 });

  // Ir al panel de Usuarios por su entrada del sidebar.
  const entrada = page.locator('aside').getByText(/usuarios/i).first();
  if (!(await entrada.isVisible().catch(() => false))) {
    test.skip(true, 'este rol no ve el panel de Usuarios');
  }
  await entrada.click();

  // El boton que abre el editor. No guarda nada: solo levanta el modal.
  const abrir = page.getByRole('button', { name: /nuevo usuario|crear usuario|añadir/i }).first();
  await expect(abrir).toBeVisible({ timeout: 15_000 });
  await abrir.focus();
  const abridor = await foco(page);
  await abrir.click();

  const dialogo = page.locator('[role="dialog"][aria-label="Editor de usuario"]');
  await expect(dialogo).toBeVisible({ timeout: 10_000 });
  await expect(dialogo).toHaveAttribute('aria-modal', 'true');

  const dentro = await foco(page);
  console.log('  foco al abrir:', JSON.stringify(dentro));
  expect(dentro?.rotuloDelDialogo, 'el foco deberia entrar solo al dialogo').toBe('Editor de usuario');

  let fugas = 0;
  for (let i = 0; i < 30; i++) {
    await page.keyboard.press('Tab');
    const f = await foco(page);
    if (f?.rotuloDelDialogo !== 'Editor de usuario') {
      fugas++;
      console.log(`  FUGA en el tab ${i + 1}: ${JSON.stringify(f)}`);
    }
  }
  expect(fugas, 'el foco se escapo del dialogo').toBe(0);

  // Escape: la salida por teclado. Sin ella, atrapar el foco seria PEOR que no
  // atraparlo, porque solo se podria salir con el raton.
  await page.keyboard.press('Escape');
  await expect(dialogo).toBeHidden({ timeout: 5_000 });

  const trasCerrar = await foco(page);
  console.log('  foco al cerrar:', JSON.stringify(trasCerrar));
  expect(trasCerrar?.nombre, 'el foco deberia volver al control que abrio').toBe(abridor?.nombre);
});
