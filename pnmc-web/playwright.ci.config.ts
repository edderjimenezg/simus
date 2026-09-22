import { defineConfig, devices } from '@playwright/test';

/**
 * Subconjunto de pruebas de extremo a extremo QUE PUEDE CORRER EN CI.
 *
 * EL PROBLEMA QUE RESUELVE. `npm run e2e` existe desde hace tiempo y **nunca se
 * ha ejecutado en el pipeline**. Necesita `ng serve` en 4300, la API en 8180 y
 * la base sembrada, y eso no lo hay en una corrida limpia. Resultado: la unica
 * automatizacion de accesibilidad del repositorio no vigila nada.
 *
 * Costo real de que no corriera, medido el 22 ago 2026: la auditoria de front
 * convirtio un `<main class="portal-acceso">` anidado en un `<section>` —cambio
 * correcto, el landmark principal unico lo declara el shell— y con eso dejo en
 * rojo las cinco pruebas de contraste de `/registro`, que localizaban
 * `main.portal-acceso`. Nadie se entero durante un dia entero. Con esta
 * configuracion cableada, se habria sabido en el primer push.
 *
 * QUE SE EJECUTA Y QUE NO, y por que. Se midio prueba a prueba contra el
 * artefacto de produccion servido SIN backend:
 *
 *   SI corren (4):
 *     · contraste-registro.spec.ts — las 2 pantallas vigentes. Miden contraste sobre el DOM real,
 *       que es mejor evidencia que calcularlo sobre las clases.
 *     · teclado-sitio-publico.spec.ts — «tarjetas de eje del Home se activan
 *       con Enter» y «los hitos de Sobre el PNMC se despliegan con el teclado».
 *
 *   NO corren las pruebas marcadas `@backend`, y quedan fuera a proposito:
 *     · «las noticias se abren con el teclado» — las tarjetas de noticia vienen
 *       de la API. Sin backend no hay ninguna en el recorrido del tabulador y la
 *       prueba falla midiendo la ausencia de datos, no un defecto de teclado.
 *     · «el panel no altera ningun grupo» — necesita sesion iniciada y escribe
 *       en la base.
 *     · las dos de `dialogos-galeria.spec.ts` — la galeria necesita albumes de
 *       la API: sin datos no hay nada que abrir. Se ejecutan a mano contra el
 *       entorno vivo con `PNMC_BASE_URL=http://127.0.0.1:4300 npx playwright
 *       test e2e/dialogos-galeria.spec.ts`, y ahi pasan las dos.
 *     · las SEIS de `edicion-organica.spec.ts` — una por pestana del panel. Cada
 *       una inicia sesion, escribe, guarda, publica y repone contra la base
 *       real: es el recorrido completo del editor, y no hay forma de fingirlo
 *       sobre un artefacto estatico.
 *
 * AQUI DECIA «NO corren (4)» y «7 de 11». Se quedo corto al anadirse las seis
 * organicas, que llegaron ya etiquetadas `@backend` y por eso nadie tuvo que
 * tocar este archivo — exactamente el efecto que la etiqueta buscaba, y el
 * motivo de que una cifra escrita a mano envejezca sin avisar. La selección
 * vigente se comprueba con `npx playwright test --list`.
 *
 * NO SE OCULTA LA EXCLUSION: las que faltan estan entre las que mas valor
 * tienen —el recorrido del editor, la fidelidad del panel y las dos trampas de
 * foco de la galeria lo estan claramente— y para ejecutarlas hace falta levantar
 * SQL y la API en el CI. Eso es un trabajo aparte, de A11, y esta configuracion
 * no pretende sustituirlo.
 *
 * SE SIRVE EL ARTEFACTO DE PRODUCCION, no `ng serve`: es el que se despliega, y
 * es el unico que aplica los `fileReplacements` que dejan fuera la barra de
 * herramientas de desarrollo y las cuentas de prueba.
 *
 * PUERTO 4201: esta suite sirve por sí misma el artefacto de producción y no debe
 * ocupar el 4300 reservado al entorno de desarrollo.
 *
 * USO:
 *   npm run build && npm run e2e:ci
 */

/**
 * Las pruebas que necesitan API, sesion o base se marcan con `@backend`.
 *
 * Antes esto era una lista de trozos de titulo escritos aqui. La lista no se
 * puede mantener: quien anade una prueba que necesita base no tiene forma de
 * saber que existe este archivo, y CI la ejecuta contra un artefacto estatico y
 * falla por una razon que no tiene nada que ver con la prueba. Paso justo eso
 * con `home-edicion-organica.spec.ts` el 22 ago 2026 —hoy `edicion-organica`,
 * renombrado al dejar de ser solo del Home—.
 *
 * La etiqueta viaja con la prueba, que es donde se sabe lo que necesita:
 *
 *   test('...', { tag: '@backend' }, async ({ page }) => { ... })
 */
const NECESITAN_BACKEND = /@backend/;

export default defineConfig({
  testDir: './e2e',
  workers: 1,
  fullyParallel: false,
  // Un reintento automatico esconde el fallo intermitente que buscamos.
  retries: 0,
  reporter: process.env.CI ? [['list'], ['github']] : [['list']],
  timeout: 60_000,
  expect: { timeout: 10_000 },
  grepInvert: NECESITAN_BACKEND,
  // Falla la corrida si alguien deja un `test.only` puesto: en CI eso apagaria
  // el resto de la suite en silencio.
  forbidOnly: !!process.env.CI,
  use: {
    baseURL: 'http://127.0.0.1:4201',
    actionTimeout: 15_000,
    navigationTimeout: 30_000,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'off',
    ...devices['Desktop Chrome'],
  },
  // Levanta el servidor el propio Playwright y lo apaga al terminar. Sin esto
  // hay que acordarse de arrancarlo y de matarlo, y un servidor huerfano en el
  // 4201 hace que la siguiente corrida mida un artefacto viejo.
  webServer: {
    command: 'node tools/servidor-estatico.mjs --puerto 4201',
    url: 'http://127.0.0.1:4201/',
    reuseExistingServer: !process.env.CI,
    timeout: 60_000,
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
});
