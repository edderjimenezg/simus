import { defineConfig, devices } from '@playwright/test';

/**
 * Pruebas de extremo a extremo del panel «Administración de textos».
 *
 * Estas pruebas NO sustituyen a las de Karma: aquellas comprueban unidades con
 * la API simulada; estas comprueban lo único que ninguna simulación cubre —que
 * lo que el editor ve en el formulario es lo que el servidor tiene, y que el
 * botón que dice guardar guarda—. El defecto que las motivó salió publicado al
 * sitio sin que ninguna prueba unitaria lo notara.
 *
 * Requieren el entorno real levantado: `ng serve` en 4300, la API en 8180 y la
 * base sembrada. Por eso viven fuera de `npm test` y se corren con
 * `npm run e2e`.
 *
 * Si va a editar código mientras corren, levante un servidor sin recarga y
 * apunte las pruebas ahí:
 *
 *   ng serve --host localhost --port 4300 --no-live-reload --proxy-config proxy.conf.json
 *   PNMC_BASE_URL=http://localhost:4300 npm run e2e
 *
 * Dos detalles de esa orden que hacen perder media hora si se cambian: la
 * bandera es `--no-live-reload` (con `--live-reload false` el servidor no
 * arranca y no dice por qué), y hay que apuntar a `localhost` y no a
 * `127.0.0.1`, porque `ng serve` se ata al nombre y no a la dirección.
 *
 * `panel-fidelidad` recorre 37 grupos y tarda minutos: cualquier archivo que se
 * guarde a mitad hace que `ng serve` recompile, el navegador recargue y la
 * consola vuelva a su pantalla de inicio. La propia prueba lo detecta y lo dice,
 * pero es más cómodo no provocarlo.
 */
export default defineConfig({
  testDir: './e2e',
  // Las pruebas escriben en la MISMA base: si corrieran en paralelo se pisarían
  // los borradores entre ellas y los fallos serían irreproducibles.
  workers: 1,
  fullyParallel: false,
  // Un reintento automático esconde precisamente la clase de fallo intermitente
  // que buscamos. Si falla, queremos verlo.
  retries: 0,
  reporter: [['list']],
  timeout: 60_000,
  expect: { timeout: 10_000 },
  use: {
    baseURL: process.env.PNMC_BASE_URL ?? 'http://127.0.0.1:4300',
    // Sin estos topes, un selector equivocado se come el timeout de la prueba
    // entera antes de decir nada: la primera corrida tardó 16 minutos en
    // informar de un clic bloqueado por un modal.
    actionTimeout: 15_000,
    navigationTimeout: 30_000,
    // La traza guarda DOM, red y capturas del fallo: es la evidencia de «el
    // formulario mostraba X», que es justo lo que no pudimos reconstruir a mano.
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'off',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
});
