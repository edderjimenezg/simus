import { test as setup, expect } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';
import { RUTA_SESION } from './sesion-compartida';

/**
 * Inicia sesión UNA vez y guarda la cookie para que la reutilicen los barridos.
 *
 * ## Por qué existe
 *
 * El API tiene un limitador de tasa (`Program.cs-112`, `PermitLimit = 30`).
 * Cuando cada prueba iniciaba sesión por su cuenta, una corrida completa
 * disparaba **más de sesenta accesos en pocos minutos** y agotaba la ventana.
 * Los últimos proyectos —portátil y escritorio— se quedaban sin poder entrar, y
 * el barrido informaba de «10 paneles INALCANZABLES».
 *
 * Eso es una **falsa alarma del instrumento**: el panel estaba perfecto, lo que
 * fallaba era el acceso. Ejecutado en solitario, el mismo barrido daba seis en
 * verde. Un arnés que informa de defectos que él mismo provoca es peor que no
 * tener arnés.
 *
 * Con esta sesión compartida, una corrida completa hace **un** acceso.
 */

const CREDENCIALES = {
  email: process.env.PNMC_ADMIN_EMAIL ?? 'admin@pnmc.local',
  password: process.env.PNMC_ADMIN_PASSWORD ?? 'admin',
};

setup('iniciar sesion una sola vez', async ({ page }) => {
  await page.addInitScript((email) => {
    window.localStorage.setItem(`pnmc_tour_seen_${email}`, '1');
  }, CREDENCIALES.email);

  await page.goto('/administracion', { waitUntil: 'domcontentloaded' });
  await page.locator('input[type="email"]').fill(CREDENCIALES.email);
  await page.locator('input[type="password"]').fill(CREDENCIALES.password);
  await page.getByRole('button', { name: 'Entrar al panel' }).click();

  // Se espera al sidebar, no a un texto: «Administración de textos» aparece dos
  // veces y el localizador da error por ambigüedad.
  //
  // Y SE LE NOMBRA POR SU IDENTIFICADOR, no por su etiqueta. Esperar a `aside` a secas funcionó
  // hasta que la consola añadió un segundo —«Estado técnico del sistema»—: desde entonces el modo
  // estricto de Playwright fallaba por ambigüedad, la sesión no llegaba a guardarse y las cuarenta y
  // dos pruebas que dependen de ella informaban de paneles inalcanzables. El síntoma no mencionaba
  // la sesión por ninguna parte, así que parecían cuarenta y dos defectos del producto.
  await expect(page.locator('aside#navegacion-administrativa')).toBeVisible({ timeout: 30_000 });

  await page.context().storageState({ path: RUTA_SESION });
  fs.mkdirSync(path.dirname(RUTA_SESION), { recursive: true });
  console.log('  sesion guardada en', RUTA_SESION);
});
