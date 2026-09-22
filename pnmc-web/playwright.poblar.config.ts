import { defineConfig, devices } from '@playwright/test';

/**
 * Configuración APARTE para poblar la base local con datos de trabajo.
 *
 * NO ES UNA SUITE DE PRUEBAS y por eso no vive en `playwright.config.ts`: aquello se corre en cada
 * cambio y tiene que poder correrse mil veces; esto ESCRIBE dieciocho organizaciones y dieciocho
 * Festivales, y correrlo dos veces choca con `UX_Entidades_CorreoContacto`, que impide repetir el
 * correo de una organización. Los ficheros terminan en `.seed.ts` para que el `testMatch` de la
 * suite —`*.spec.ts`— no los recoja nunca por accidente.
 *
 * Se corre a mano, con el sitio en 4300 y la API en 8180:
 *
 *   npx playwright test -c playwright.poblar.config.ts
 *
 * LOS PLAZOS SON LARGOS A PROPÓSITO. El panel de la organización tarda entre 2 y 26 s por carga
 * —`ADtoAsync` hace seis consultas por Festival— y este
 * guion entra y sale de él una vez por organización.
 */
export default defineConfig({
  testDir: './e2e',
  testMatch: ['**/poblar/**/*.seed.ts', '**/ajustes/**/*.seed.ts'],
  workers: 1,
  fullyParallel: false,
  retries: 0,
  reporter: [['list']],
  timeout: 30 * 60_000,
  expect: { timeout: 30_000 },
  use: {
    baseURL: process.env.PNMC_BASE_URL ?? 'http://127.0.0.1:4300',
    actionTimeout: 60_000,
    navigationTimeout: 60_000,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'off',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
});
