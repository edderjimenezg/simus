import { test, Page } from '@playwright/test';

/**
 * El banco de trabajo de los ajustes del piloto de Festivales.
 *
 * NO COMPRUEBA NADA POR SÍ MISMO, y es a propósito. Abre la pantalla de un punto concreto del plan
 * —`AJUSTES-FESTIVALES.md`— y deja la captura para mirarla. La auditoría es leer la imagen contra
 * lo que pidió el documento del usuario; una aserción automática solo comprobaría lo que yo creo
 * que pidió.
 *
 * Se corre por punto, antes y después de cada cambio:
 *
 *   PUNTO=B npx playwright test -c playwright.poblar.config.ts e2e/ajustes/auditar.seed.ts
 *
 * Con `MOMENTO=antes` o `MOMENTO=despues` separa las dos capturas para poder compararlas.
 */

const PUNTO = (process.env.PUNTO ?? 'B').toUpperCase();
const MOMENTO = process.env.MOMENTO ?? 'ahora';
const RAIZ = `e2e/capturas/ajustes/${PUNTO}`;

const ORGANIZACION = { email: 'pruebas@pnmc.local', password: 'Pnmc2026*Local' };
const FUNCIONARIO = { email: 'admin@pnmc.local', password: 'admin' };

async function foto(page: Page, nombre: string, completa = false): Promise<void> {
  await page.screenshot({
    path: `${RAIZ}/${MOMENTO}-${nombre}.png`,
    animations: 'disabled',
    fullPage: completa,
  });
}

/** Espera a que el servidor de desarrollo quite su pantalla de error, si la puso. */
async function sinOverlay(page: Page): Promise<void> {
  const overlay = page.locator('vite-error-overlay');
  for (let i = 0; i < 60 && await overlay.count() > 0; i++) await page.waitForTimeout(2000);
}

async function abrir(page: Page, ruta: string, espera = 6000): Promise<void> {
  await page.goto(ruta, { waitUntil: 'domcontentloaded' });
  await sinOverlay(page);
  await page.waitForTimeout(espera);
}

async function entrarComoOrganizacion(page: Page): Promise<void> {
  await page.request.post('/api/v1/external/auth/login', { data: ORGANIZACION, failOnStatusCode: false });
}

async function entrarALaConsola(page: Page): Promise<void> {
  await page.addInitScript(correo => {
    window.localStorage.setItem(`pnmc_tour_seen_${correo}`, '1');
  }, FUNCIONARIO.email);
  await page.goto('/administracion');
  await page.locator('input[type="email"]').fill(FUNCIONARIO.email);
  await page.locator('input[type="password"]').fill(FUNCIONARIO.password);
  await page.getByRole('button', { name: 'Entrar al panel' }).click();
  await page.waitForTimeout(6000);
}

/** El primer Festival publicado, para abrir su ficha pública sin escribir un id a mano. */
async function primerFestivalPublicado(page: Page): Promise<string | null> {
  const respuesta = await page.request.get('/api/v1/publico/festivales?limit=1', { failOnStatusCode: false });
  if (respuesta.status() !== 200) return null;
  const cuerpo = await respuesta.json();
  const lista = Array.isArray(cuerpo) ? cuerpo : (cuerpo.items ?? []);
  return lista.length > 0 ? String(lista[0].id) : null;
}

test(`auditar el punto ${PUNTO}`, async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });

  // ── A · Cabecera y navegación ────────────────────────────────────────────────────────────
  if (PUNTO.startsWith('A')) {
    await abrir(page, '/');
    await foto(page, 'home-cabecera');
    await page.getByRole('button', { name: /ecosistema/i }).first().hover().catch(() => {});
    await page.waitForTimeout(1200);
    await foto(page, 'menu-ecosistema');
    await abrir(page, '/registro');
    await foto(page, 'acceso-externo', true);
  }

  // ── A3 · El menú de acceso, con sus dos puertas ──────────────────────────────────────────
  if (PUNTO === 'A3') {
    await abrir(page, '/');
    await page.locator('[data-testid="iniciar-sesion"]').click();
    await page.waitForTimeout(1200);
    await foto(page, 'menu-de-acceso');
  }

  // ── B · Página pública de Festivales ─────────────────────────────────────────────────────
  if (PUNTO.startsWith('B') || PUNTO.startsWith('C')) {
    await abrir(page, '/ecosistema/festivales', 9000);
    await foto(page, 'consulta-arriba');
    await foto(page, 'consulta-completa', true);
  }

  // ── B5 · El paso al mapa con la capa puesta ──────────────────────────────────────────────
  if (PUNTO === 'B5') {
    await abrir(page, '/ecosistema/festivales', 9000);
    await page.locator('[data-testid="ir-al-mapa-festivales"]').scrollIntoViewIfNeeded();
    await foto(page, 'llamado-al-mapa');
    await page.locator('[data-testid="ir-al-mapa-festivales"]').click();
    await page.waitForTimeout(12000);
    await page.keyboard.press('Escape');
    await page.waitForTimeout(2000);
    await foto(page, 'mapa-con-capa-festivales');
  }

  // ── D · Ficha pública del Festival ───────────────────────────────────────────────────────
  if (PUNTO.startsWith('D')) {
    const id = await primerFestivalPublicado(page);
    await abrir(page, id ? `/ecosistema/festivales/${id}` : '/ecosistema/festivales', 9000);
    await foto(page, 'ficha-publica', true);
  }

  // ── E y F · Panel de la organización ─────────────────────────────────────────────────────
  if (PUNTO.startsWith('E') || PUNTO.startsWith('F')) {
    await entrarComoOrganizacion(page);
    await abrir(page, '/ecosistema/mi-panel?pestana=organizacion', 9000);
    await foto(page, 'panel-organizacion', true);
    await abrir(page, '/ecosistema/mi-panel?pestana=ecosistema', 9000);
    await foto(page, 'panel-procesos', true);
    await abrir(page, '/ecosistema/mi-panel?pestana=solicitudes', 9000);
    await foto(page, 'panel-solicitudes', true);
  }

  // ── G · Ficha interna del Festival ───────────────────────────────────────────────────────
  if (PUNTO.startsWith('G')) {
    await entrarComoOrganizacion(page);
    await abrir(page, '/ecosistema/mi-panel?pestana=ecosistema', 9000);
    const abrirFicha = page.locator('[data-testid^="abrir-ficha-"]').first();
    if (await abrirFicha.count() > 0) {
      await abrirFicha.click();
      await page.waitForTimeout(5000);
      await foto(page, 'ficha-interna-datos');
      const pestanaEdicion = page.locator('[data-testid="pestana-edicion"]');
      if (await pestanaEdicion.count() > 0) {
        await pestanaEdicion.click();
        await page.waitForTimeout(3000);
        await foto(page, 'ficha-interna-ediciones');
      }
    } else {
      await foto(page, 'ficha-interna-NO-SE-ABRIO', true);
    }
  }

  // ── La consola, para el bloque del panel institucional ───────────────────────────────────
  if (PUNTO.startsWith('H')) {
    await entrarALaConsola(page);
    await foto(page, 'consola', true);
  }
});
