import { defineConfig } from '@playwright/test';
import * as path from 'path';
import * as os from 'os';

/**
 * Arnes de AUDITORIA. Deliberadamente separado de `playwright.config.ts`:
 * aquel fichero lo esta editando otra sesion, y las pruebas de `e2e/` son
 * invariantes de producto, no barridos de dispositivo.
 *
 * Apunta al servidor de desarrollo que ya corre en 4300. Navegar y capturar es
 * lectura pura: no escribe en la base compartida. El barrido del panel
 * administrativo NO vive aqui: necesita un entorno aislado.
 */

const VIEWPORTS = [
  { name: 'movil-360',        width: 360,  height: 640,  isMobile: true,  hasTouch: true,  scale: 3 },
  { name: 'movil-414',        width: 414,  height: 896,  isMobile: true,  hasTouch: true,  scale: 2 },
  { name: 'tablet-vertical',  width: 768,  height: 1024, isMobile: true,  hasTouch: true,  scale: 2 },
  { name: 'tablet-horizontal',width: 1024, height: 768,  isMobile: true,  hasTouch: true,  scale: 2 },
  { name: 'portatil',         width: 1366, height: 768,  isMobile: false, hasTouch: false, scale: 1 },
  { name: 'escritorio',       width: 1920, height: 1080, isMobile: false, hasTouch: false, scale: 1 },
];

export default defineConfig({
  testDir: './e2e-audit',
  workers: 2,
  fullyParallel: false,
  retries: 0,
  reporter: [['list']],
  timeout: 120_000,
  expect: { timeout: 10_000 },
  // Fuera del repositorio a proposito. Con la salida dentro del arbol, cada
  // corrida dejaba una carpeta sin rastrear que la sesion paralela acababa
  // confirmando en su siguiente commit. Los artefactos de una medicion no son
  // codigo fuente.
  outputDir:
    process.env.PNMC_AUDIT_ARTEFACTOS ??
    path.join(os.tmpdir(), 'pnmc-auditoria-artefactos'),
  use: {
    baseURL: process.env.PNMC_BASE_URL ?? 'http://127.0.0.1:4300',
    actionTimeout: 15_000,
    navigationTimeout: 45_000,
    trace: 'off',
    screenshot: 'off',
    video: 'off',
  },
  projects: [
    {
      // Corre antes que todo lo demas y deja la cookie guardada. Sin esto, cada
      // prueba iniciaba sesion por su cuenta y una corrida completa agotaba el
      // limitador de tasa del API (PermitLimit = 30), con lo que los ultimos
      // dispositivos informaban «paneles inalcanzables» por un fallo de acceso
      // que provocaba el propio arnes.
      name: 'preparacion',
      testMatch: /.*\.setup\.ts/,
      use: { browserName: 'chromium' as const },
    },
    ...VIEWPORTS.map(v => ({
    name: v.name,
    use: {
      browserName: 'chromium' as const,
      viewport: { width: v.width, height: v.height },
      isMobile: v.isMobile,
      hasTouch: v.hasTouch,
      deviceScaleFactor: v.scale,
    },
    dependencies: ['preparacion'],
  })),
  ],
});
