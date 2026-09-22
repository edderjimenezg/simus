import { test, expect, type Page } from '@playwright/test';

/**
 * La invariante que este archivo defiende:
 *
 *   Abrir un grupo y guardarlo sin tocar nada NO debe cambiar ningún campo.
 *
 * Dicho de otro modo: lo que el panel muestra tiene que ser lo que el servidor
 * tiene. Si el formulario sostiene un valor distinto —heredado de una sesión
 * anterior, de una siembra desincronizada o de un estado a medio cargar—, el
 * editor no lo nota, y al publicar el grupo ese valor sale al sitio sin que
 * nadie lo haya escrito.
 *
 * Eso ocurrió el 21 ago 2026: el encabezado del Home se publicó con el título
 * llevando dentro el texto del acento, y el sitio mostró «Diversidad Sonora»
 * dos veces. Ninguna prueba unitaria lo vio, porque todas simulan la API y
 * ninguna compara el formulario contra ella.
 *
 * No se lee el DOM campo por campo a propósito: los `input` del panel no llevan
 * su clave en ningún atributo. Se mira lo que el panel ENVÍA, que además es lo
 * único que cuenta.
 */

const CREDENCIALES = {
  email: process.env.PNMC_ADMIN_EMAIL ?? 'admin@pnmc.local',
  password: process.env.PNMC_ADMIN_PASSWORD ?? 'admin',
};

async function entrarAlPanel(page: Page) {
  // El panel muestra un tour de bienvenida la primera vez que entra cada
  // cuenta, y su fondo cubre la pantalla entera. Una persona lo cierra una vez
  // y no lo vuelve a ver; el navegador de la prueba estrena perfil en cada
  // corrida, así que lo vería siempre. Se marca como visto por su propia clave
  // en vez de pulsar por el tour: menos frágil y no depende de sus textos.
  await page.addInitScript((email) => {
    window.localStorage.setItem(`pnmc_tour_seen_${email}`, '1');
  }, CREDENCIALES.email);

  await page.goto('/administracion');
  await page.locator('input[type="email"]').fill(CREDENCIALES.email);
  await page.locator('input[type="password"]').fill(CREDENCIALES.password);
  await page.getByRole('button', { name: 'Entrar al panel' }).click();

  await page.getByRole('button', { name: 'Administración de textos' }).click();
  // Se ancla en el botón de respaldo y no en el título: «Administración de
  // textos» aparece dos veces (cabecera de la consola y del panel) y el
  // localizador da error por ambigüedad.
  await expect(page.getByRole('button', { name: 'Exportar respaldo' })).toBeVisible();
}

/**
 * Cuenta cuántos documentos ha cargado la pestaña.
 *
 * El recorrido dura minutos, y `ng serve` recompila y recarga el navegador cada
 * vez que alguien guarda un archivo. Cuando eso pasa, la consola vuelve a su
 * pantalla de inicio y la prueba muere diez segundos después buscando un botón
 * que ya no está: el mensaje habla del botón y no dice nada del recargue, que
 * es la causa.
 *
 * No sirve `framenavigated`: el enrutador de Angular navega con `pushState` y
 * eso también lo dispara, así que contaría como recargue cada clic del menú.
 * Un guion de inicialización, en cambio, se vuelve a ejecutar solo cuando hay
 * documento nuevo, que es justo lo que se quiere detectar.
 */
async function contarCargas(page: Page) {
  await page.addInitScript(() => {
    // SOLO EL DOCUMENTO PRINCIPAL. Playwright vuelve a ejecutar el guion de
    // inicializacion en CADA marco hijo que se adjunta, y desde el 29 de agosto
    // de 2026 el panel monta un <iframe> con la previsualizacion de la pagina
    // real. Sin esta linea, cada cambio de seccion del recorrido sumaria una
    // «recarga» que nadie hizo, y la prueba culparia de ello a `ng serve`.
    if (window.top !== window.self) { return; }
    const previas = Number(sessionStorage.getItem('__cargas') ?? '0');
    sessionStorage.setItem('__cargas', String(previas + 1));
  });
  return () => page.evaluate(() => Number(sessionStorage.getItem('__cargas') ?? '0'));
}

test('el panel no altera ningún grupo con solo abrirlo y guardarlo', { tag: '@backend' }, async ({ page }) => {
  test.setTimeout(15 * 60_000);
  const cargas = await contarCargas(page);
  await entrarAlPanel(page);
  // Línea base: lo que haya costado llegar al panel es normal; lo que importa
  // es que este número no se mueva durante el recorrido.
  const cargasAlEmpezar = await cargas();

  // Se registra lo que responde cada guardado. `changed` lo cuenta el servidor
  // comparando contra lo que ya tenía, así que es un juez independiente del
  // panel: no puede mentir a favor del cliente.
  const guardados: { grupo: string; changed: number }[] = [];
  page.on('response', async (res) => {
    if (res.request().method() !== 'POST') return;
    if (!/\/admin\/web-content\/groups\/[^/]+$/.test(new URL(res.url()).pathname)) return;
    try {
      const cuerpo = await res.json();
      guardados.push({ grupo: cuerpo.groupId, changed: cuerpo.changed ?? -1 });
    } catch {
      guardados.push({ grupo: new URL(res.url()).pathname.split('/').pop()!, changed: -1 });
    }
  });

  // Los contenedores llevan `data-testid` a propósito: localizarlos por clases
  // de Tailwind ataba la prueba al estilo y agarraba botones de otras zonas.
  const pestañas = page.getByTestId('section-tabs').getByRole('button');
  const totalPestañas = await pestañas.count();

  // Se recorre por posición y no por nombre: el texto de los botones viene con
  // saltos de línea del template, y casarlo con una expresión regular exacta
  // falla por espacios en blanco invisibles.
  for (let p = 0; p < totalPestañas; p++) {
    const sección = (await pestañas.nth(p).textContent())?.trim() ?? `#${p}`;
    await pestañas.nth(p).click();

    // Cabeceras del acordeon. Antes era la lista de la columna izquierda, que se retiro con
    // la maquetacion de tres columnas.
    const grupos = page.locator('[data-testid^="bloque-"]');

    // Angular repinta la lista de grupos de forma asíncrona. Contar justo
    // después del clic devuelve el número de la sección ANTERIOR, y si la nueva
    // tiene menos grupos el recorrido se sale del final. Se espera a que dos
    // lecturas seguidas coincidan.
    let total = -1;
    await expect
      .poll(
        async () => {
          const n = await grupos.count();
          const estable = n > 0 && n === total;
          total = n;
          return estable;
        },
        { timeout: 10_000, intervals: [150, 150, 200, 300], message: `la lista de «${sección}» no se estabilizó` },
      )
      .toBe(true);

    for (let i = 0; i < total; i++) {
      const etiqueta = `${sección} · ${(await grupos.nth(i).textContent())?.trim() ?? i}`;
      // El acordeon es un interruptor: pulsar el bloque que ya esta abierto lo pliega y deja
      // el formulario sin pintar. El primero de cada seccion se estrena abierto.
      if ((await grupos.nth(i).getAttribute('aria-expanded')) !== 'true') {
        await grupos.nth(i).click();
      }

      // La carga del grupo es una petición aparte; guardar antes de que llegue
      // enviaría los textos de fábrica y arruinaría la medición.
      await page.waitForResponse(
        (r) => r.request().method() === 'GET' && /\/admin\/web-content\/groups\//.test(r.url()),
        { timeout: 15_000 },
      ).catch(() => { /* ya estaba cargado: el panel cachea por grupo */ });

      expect(
        await cargas(),
        'La aplicación se recargó a mitad del recorrido y la consola volvió a su pantalla de ' +
        'inicio. Suele ser `ng serve` recompilando porque alguien guardó un archivo mientras la ' +
        'prueba corría: repítala con el árbol quieto.',
      ).toBe(cargasAlEmpezar);
      await expect(page.getByRole('button', { name: 'Guardar Borrador' })).toBeEnabled();

      const antes = guardados.length;
      await page.getByRole('button', { name: 'Guardar Borrador' }).click();
      await expect
        .poll(() => guardados.length, { timeout: 20_000, message: `sin respuesta al guardar «${etiqueta}»` })
        .toBeGreaterThan(antes);

      // Se informa sobre la marcha: si la prueba se rompe a mitad del recorrido,
      // lo ya medido no se pierde con ella.
      const ultimo = guardados[guardados.length - 1];
      console.log(`  ${ultimo.changed === 0 ? '✓' : '✗'} ${etiqueta} — changed: ${ultimo.changed}`);
    }
  }

  const sospechosos = guardados.filter((g) => g.changed !== 0);
  console.log(`\n  grupos guardados: ${guardados.length}`);
  console.log(`  grupos que cambiaron sin que nadie escribiera: ${sospechosos.length}`);
  for (const s of sospechosos) console.log(`    ✗ ${s.grupo} — ${s.changed} campo(s)`);

  expect(
    sospechosos,
    'Abrir un grupo y guardarlo no debe cambiar nada. Si cambió, el formulario ' +
      'mostraba algo distinto de lo que el servidor tenía, y publicar ese grupo ' +
      'habría alterado el sitio sin que nadie lo escribiera.',
  ).toEqual([]);
});
