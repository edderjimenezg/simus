import { test, expect, Page } from '@playwright/test';

/**
 * El circuito de devolución, de punta a punta y por el navegador.
 *
 * <b>QUÉ MIDE ESTO QUE NINGUNA PRUEBA DE KARMA PUEDE MEDIR.</b> Este circuito cruza DOS
 * AUTENTICACIONES: la organización tiene cookie `pnmc.external` y el funcionario `pnmc.admin`. En
 * Karma las dos son el mismo doble; aquí son dos sesiones distintas contra el mismo servidor, y lo
 * que se comprueba es que lo que una escribe la otra lo recibe. También cruza las dos tablas nuevas
 * —`dbo.RevisionesFestival` y `dbo.RevisionesFestivalObservaciones`— y el cambio de estado del
 * Festival, que es lo que lo saca de la cola de revisión.
 *
 * <b>EL RECORRIDO, tal como lo pidió el usuario:</b> «cuando la mando a
 * revisión en el CMS debería poder abrir completamente la ficha […] en todos los campos poder pedir
 * cambios puntuales sobre alguno de los campos, ir guardando el borrador, y luego poder enviar la
 * solicitud de cambios y que al devolverlo le llegue al usuario para hacer esos cambios».
 *
 *   1. La organización crea un Festival con una edición y lo manda a revisión.
 *   2. El funcionario abre la FICHA COMPLETA desde la bandeja, señala un campo del Festival y otro
 *      de la edición, y GUARDA EL BORRADOR.
 *   3. Cierra, vuelve a abrir y comprueba que las dos notas siguen ahí: es lo que distingue guardar
 *      de parecer que se guarda.
 *   4. ENVÍA la solicitud. El Festival sale de la cola.
 *   5. La organización ve el número en su tarjeta, abre la ficha y encuentra cada nota pegada a su
 *      campo; entra a editar y las ve agrupadas por paso; marca una como atendida.
 *
 * <b>SE ENTRA POR LA API Y NO POR EL FORMULARIO DE ACCESO</b>, como el recorrido de Festivales:
 * `page.request` comparte el tarro de cookies del navegador, así que la sesión queda puesta y
 * recorrer la pantalla de entrada solo añadiría motivos ajenos por los que fallar.
 *
 * NECESITA EL ENTORNO LEVANTADO: `ng serve` en 4300 y la API en 8180, con la base sembrada. Vive
 * fuera de `npm test` y se corre con `npm run e2e`.
 */

const ORGANIZACION = {
  email: process.env.PNMC_EXTERNO_EMAIL ?? 'externo@pnmc.local',
  password: process.env.PNMC_EXTERNO_PASSWORD ?? 'admin',
};

const FUNCIONARIO = {
  email: process.env.PNMC_ADMIN_EMAIL ?? 'admin@pnmc.local',
  password: process.env.PNMC_ADMIN_PASSWORD ?? 'admin',
};

const DIALOGO = '[data-testid="ficha-festival-modal"]';

/** La base no se limpia entre corridas y los nombres se buscan por texto. */
const MARCA = new Date().toISOString().replace(/[^0-9]/g, '').slice(8, 14);

const NOMBRE = `Festival de Gaitas del Bajo Sinu ${MARCA}`;
const NOTA_DEL_FESTIVAL = 'Escribe el nombre completo del Festival, sin la sigla.';
const NOTA_DE_LA_EDICION = 'Falta el nombre propio de esta edicion: no puede ser el del Festival.';

/** Despliega un paso si está plegado. «El estado natural son todos cerrados». */
async function desplegar(page: Page, pasoId: string): Promise<void> {
  const boton = page.locator(`[data-testid="abrir-paso-${pasoId}"]`);
  await boton.scrollIntoViewIfNeeded();
  if ((await boton.getAttribute('aria-expanded')) === 'false') await boton.click();
}

async function anotar(page: Page, campo: string, texto: string): Promise<void> {
  await page.locator(`[data-testid="pedir-cambio-${campo}"]`).scrollIntoViewIfNeeded();
  await page.locator(`[data-testid="pedir-cambio-${campo}"]`).click();
  await page.locator(`[data-testid="caja-nota-${campo}"]`).fill(texto);
  await page.locator(`[data-testid="guardar-nota-${campo}"]`).click();
  await expect(page.locator(`[data-testid="nota-${campo}"]`)).toContainText(texto);
}

test.describe('La devolución campo por campo', () => {
  test.setTimeout(300000);

  // SESENTA SEGUNDOS POR ACCIÓN EN ESTE FICHERO, en vez de los 15 s de `playwright.config.ts`.
  //
  // ESTE RECORRIDO ENTRA Y SALE DEL PANEL DE LA ORGANIZACIÓN CUATRO VECES, y ese panel es hoy la
  // pantalla más cara del sitio: `GET /externo/organizaciones/115/festivales` tardó **26,1 s** en
  // frío y **1,7 a 2,6 s** en caliente. Casi todo es arranque —modelo de EF
  // y JIT—, pero debajo hay un N+1: `ADtoAsync` hace SEIS consultas por Festival, y la organización
  // de prueba acumula **86**, trece de ellos dejados por corridas anteriores de este mismo fichero.
  //
  // FALLÓ EN TRES PUNTOS DISTINTOS DEL PANEL, no en uno: el clic de «Enviar a revisión» y el
  // `scrollIntoViewIfNeeded` de dos filas. Subirlo clic a clic era perseguir el siguiente. Este
  // plazo NO resuelve la latencia; solo deja de esconderla detrás de un
  // rojo intermitente que parece un fallo de localizador.
  test.use({ actionTimeout: 60_000 });

  test('lo que el funcionario señala campo a campo llega a la organización junto a su campo', { tag: '@backend' }, async ({ page }) => {
    // ══ 1. La organización crea el Festival y lo manda a revisión ═══════════════════════════
    const entrada = await page.request.post('/api/v1/external/auth/login', {
      data: { email: ORGANIZACION.email, password: ORGANIZACION.password },
    });
    expect(entrada.status(), 'la cuenta externa de prueba tiene que poder entrar').toBe(200);

    await page.goto('/ecosistema/mi-panel?pestana=ecosistema');
    await page.locator('[data-testid="abrir-crear-festival"]').click();
    await expect(page.locator(DIALOGO)).toBeVisible();

    await page.locator('[data-testid="abrir-paso-generales"]').click();
    await page.fill('[name="nombre"]', NOMBRE);
    await page.fill('[name="descripcionFestival"]', 'Gaitas largas y cortas del bajo Sinu.');
    await page.selectOption('[name="nivelCobertura"]', 'nacional');

    // Una edición, para poder señalar también un campo suyo.
    await page.locator('[data-testid="pestana-edicion"]').click();
    await page.locator('[data-testid="ficha-crear-primera-edicion"]').click();
    await expect(page.locator('[data-testid="bloque-edicion"]')).toBeVisible();
    await page.fill('[name="nombreEdicion"]', NOMBRE);

    await page.locator('[data-testid="ficha-guardar"]').click();
    await expect(page.locator('[data-testid="ficha-mensaje"]')).toContainText(/qued/i, { timeout: 30000 });
    await page.locator('[data-testid="ficha-cerrar"]').click();
    await expect(page.locator(DIALOGO)).toHaveCount(0);

    await page.goto('/ecosistema/mi-panel?pestana=ecosistema');
    // ESTE ES EL PRIMER GOLPE CONTRA EL PANEL, y el primero es el caro: aquí se paga el arranque en
    // frío de la API. Lo cubre el `actionTimeout` de arriba, que se puso por este clic.
    await page.locator(`button[aria-label="Enviar a revisión: ${NOMBRE}"]`).click();
    await expect(page.locator(`button[aria-label="Enviar a revisión: ${NOMBRE}"]`))
      .toHaveCount(0, { timeout: 30000 });

    // ══ 2. El funcionario abre la ficha completa y señala dos campos ════════════════════════
    //
    // AQUI SE ENTRA POR EL FORMULARIO Y NO POR LA API, al reves que arriba: la consola pinta su
    // propia pantalla de acceso y no la retira por tener la cookie puesta, asi que su fondo
    // —`fixed inset-0 z-[8000]`— se queda encima y se traga todos los clics. Es el mismo camino que
    // sigue `panel-fidelidad.spec.ts`.
    //
    // Y SE MARCA EL TOUR COMO VISTO: la consola lo ensena la primera vez de cada cuenta y su fondo
    // cubre la pantalla entera. Una persona lo cierra una vez; el navegador de la prueba estrena
    // perfil en cada corrida y lo veria siempre.
    await page.addInitScript(email => {
      window.localStorage.setItem(`pnmc_tour_seen_${email}`, '1');
    }, FUNCIONARIO.email);

    await page.goto('/administracion');
    await page.locator('input[type="email"]').fill(FUNCIONARIO.email);
    await page.locator('input[type="password"]').fill(FUNCIONARIO.password);
    await page.getByRole('button', { name: 'Entrar al panel' }).click();
    await page.getByRole('button', { name: 'Revisión', exact: true }).click();
    // SE ANCLA EN EL PANEL Y NO EN EL TITULO: «Bandeja de revision» sale dos veces —la cabecera de
    // la consola y la del panel— y el localizador da error por ambiguedad.
    await expect(page.locator('app-admin-festival-review-panel'))
      .toContainText('Decisiones institucionales', { timeout: 30000 });
    const fila = page.locator('article').filter({ hasText: NOMBRE }).first();
    await fila.scrollIntoViewIfNeeded();
    await fila.locator('[data-testid^="abrir-ficha-completa-"]').click();
    await expect(page.locator(DIALOGO)).toBeVisible();

    // ES LA MISMA FICHA: las dos pestañas y los pasos, plegados.
    await expect(page.locator('[data-testid="pestana-festival"]')).toHaveAttribute('aria-selected', 'true');
    await expect(page.locator('[data-testid="pestana-edicion"]')).toBeVisible();
    await expect(page.locator('[data-testid^="abrir-paso-"]')).toHaveCount(3);
    // Y NO SE PUEDE EDITAR desde aquí: se pide que corrijan, no se corrige.
    await expect(page.locator('[data-testid="ficha-editar"]')).toHaveCount(0);
    await expect(page.locator('[data-testid="ficha-guardar"]')).toHaveCount(0);

    await desplegar(page, 'generales');
    await anotar(page, 'festival.nombre', NOTA_DEL_FESTIVAL);

    await page.locator('[data-testid="pestana-edicion"]').click();
    await desplegar(page, 'edicion');
    await anotar(page, 'edicion.nombre', NOTA_DE_LA_EDICION);

    await expect(page.locator('[data-testid="revision-recuento"]')).toContainText('2 campos señalados');
    await expect(page.locator('[data-testid="revision-recuento"]')).toContainText('sin guardar');

    // ══ 3. Guardar el borrador, cerrar y comprobar que quedó ESCRITO ════════════════════════
    await page.fill('[data-testid="revision-observacion-general"]', 'Dos cosas, y el resto está bien.');
    await page.locator('[data-testid="revision-guardar"]').click();
    await expect(page.locator('[data-testid="revision-mensaje"]'))
      .toContainText(/todavía no lo ve/i, { timeout: 30000 });
    await expect(page.locator('[data-testid="revision-recuento"]')).not.toContainText('sin guardar');
    await page.locator('[data-testid="revision-cerrar"]').click();
    await expect(page.locator(DIALOGO)).toHaveCount(0);

    await fila.locator('[data-testid^="abrir-ficha-completa-"]').click();
    await expect(page.locator(DIALOGO)).toBeVisible();
    // EL DISTINTIVO DEL ENCABEZADO, con todo plegado: es lo único que dice dónde mirar.
    await expect(page.locator('[data-testid="insignia-cambios-generales"]')).toContainText('1');
    await desplegar(page, 'generales');
    await expect(page.locator('[data-testid="nota-festival.nombre"]')).toContainText(NOTA_DEL_FESTIVAL);
    await expect(page.locator('[data-testid="revision-observacion-general"]'))
      .toHaveValue('Dos cosas, y el resto está bien.');

    // ══ 4. Enviar la solicitud ══════════════════════════════════════════════════════════════
    page.once('dialog', dialogo => {
      expect(dialogo.message()).toContain('2 campos señalados');
      void dialogo.accept();
    });
    await page.locator('[data-testid="revision-enviar"]').click();
    await expect(page.locator('text=La solicitud de cambios salió hacia la organización'))
      .toBeVisible({ timeout: 30000 });
    // EL FESTIVAL SALE DE LA COLA: pasó a `ajustes_solicitados`, así que ya no está en revisión.
    await expect(page.locator('article').filter({ hasText: NOMBRE })).toHaveCount(0, { timeout: 30000 });

    // ══ 5. La organización lo recibe ════════════════════════════════════════════════════════
    await page.goto('/ecosistema/mi-panel?pestana=ecosistema');
    const tarjeta = page.locator('li').filter({ hasText: NOMBRE }).first();
    await tarjeta.scrollIntoViewIfNeeded();
    await expect(tarjeta).toContainText('2 campos con cambios pedidos');

    await page.locator(`button[aria-label="Abrir la ficha completa: ${NOMBRE}"]`).click();
    await expect(page.locator(DIALOGO)).toBeVisible();
    await desplegar(page, 'generales');
    // LA NOTA, PEGADA A SU CAMPO. Es la mitad que hace útil el circuito.
    await expect(page.locator('[data-testid="nota-festival.nombre"]')).toContainText(NOTA_DEL_FESTIVAL);
    // Y LA ORGANIZACIÓN NO PUEDE QUITARLA: atiende lo que le pidieron.
    await expect(page.locator('[data-testid="quitar-nota-festival.nombre"]')).toHaveCount(0);

    // AL EDITAR, DONDE HAY UN `<input>` NO CABE LA NOTA: sube al principio del paso.
    await page.locator('[data-testid="ficha-editar"]').click();
    await desplegar(page, 'generales');
    const resumen = page.locator('[data-testid="cambios-paso-generales"]');
    await expect(resumen).toContainText('Nombre del festival');
    await expect(resumen).toContainText(NOTA_DEL_FESTIVAL);

    // Y SE PUEDE MARCAR COMO ATENDIDO, que es la otra punta de la trazabilidad.
    //
    // SE ESPERA LA RESPUESTA Y NO EL ESTADO DE LA CASILLA. `toBeChecked()` pasa en cuanto el
    // navegador marca la casilla, que es ANTES de que salga la peticion: marcar son dos viajes —el
    // testigo antiforgery y el POST—. La primera version de este recorrido navegaba justo despues y
    // abortaba la peticion en vuelo; el recorrido fallaba al final y el defecto parecia del
    // servidor. Esperar la respuesta es lo unico que distingue «se marco» de «se pinto marcado».
    const casilla = resumen.locator('input[type="checkbox"]').first();
    const [marcada] = await Promise.all([
      page.waitForResponse(r => r.url().includes('/cambios-pedidos/') && r.url().endsWith('/atender')),
      casilla.check(),
    ]);
    expect(marcada.status()).toBe(200);

    // AL RECARGAR SIGUE MARCADO: se escribió en el servidor, no solo en la pantalla.
    await page.goto('/ecosistema/mi-panel?pestana=ecosistema');
    await expect(page.locator('li').filter({ hasText: NOMBRE }).first())
      .toContainText('1 campo con un cambio pedido');

    // ══ 6. Con un cambio sin atender, el reenvío NO se puede pulsar ═════════════════════════
    //
    // «CUANDO TERMINO DE ATENDER LOS AJUSTES, SOLO CUANDO TERMINO DE ATENDERLOS, SE ME HABILITA
    // VOLVER A ENVIAR», lo pidió el usuario. Hasta ese día la tarjeta de un
    // Festival devuelto no tenía NINGÚN botón de reenviar: se corregía y se quedaba ahí.
    // EL SELECTOR ES POR PREFIJO: apagado, el nombre accesible arrastra el motivo detrás del
    // nombre del Festival, porque un `<button disabled>` no entra en el recorrido de tabulación y
    // un `aria-describedby` sobre él no se lee nunca.
    const reenviar = page.locator(`button[aria-label^="Volver a enviar a revisión: ${NOMBRE}"]`);
    await expect(reenviar).toBeVisible();
    await expect(reenviar).toBeDisabled();
    await expect(reenviar).toHaveAttribute('aria-label', /queda 1 campo con un cambio pedido sin marcar/);

    // ══ 7. Se atiende el que falta —el de la edición— y el botón se enciende ════════════════
    await page.locator(`button[aria-label="Abrir la ficha completa: ${NOMBRE}"]`).click();
    await expect(page.locator(DIALOGO)).toBeVisible();

    // EL RECUENTO, ARRIBA DEL TODO. «Me gustaría ver la cantidad de ajustes», del usuario el 30 de
    // agosto de 2026. Con los pasos plegados no había ningún sitio que dijera cuántos quedan.
    const recuento = page.locator('[data-testid="ficha-recuento-ajustes"]');
    await expect(recuento).toContainText('1 de 2 ajustes hechos');
    await expect(recuento).toHaveClass(/bg-amber-50/);

    await page.locator('[data-testid="pestana-edicion"]').click();
    // LA EDICIÓN SE ABRE POR SU TARJETA: la ficha arranca con la lista, no con una desplegada.
    const abrirPaso = page.locator('[data-testid="abrir-paso-edicion"]');
    if ((await abrirPaso.count()) === 0) {
      await page.locator('[data-testid^="ficha-edicion-"]').first().click();
    }
    await desplegar(page, 'edicion');
    const casillaEdicion = page.locator('[data-testid="atender-edicion.nombre"]');
    await casillaEdicion.scrollIntoViewIfNeeded();
    const [segunda] = await Promise.all([
      page.waitForResponse(r => r.url().includes('/cambios-pedidos/') && r.url().endsWith('/atender')),
      casillaEdicion.check(),
    ]);
    expect(segunda.status()).toBe(200);

    // DE ÁMBAR A VERDE, sin recargar y en la misma región viva: es el momento que se define
    // que se dijera. El texto cambia además del color, para quien no distingue esos dos tonos.
    await expect(recuento).toContainText('Todos los ajustes hechos: 2 de 2');
    await expect(recuento).toHaveClass(/bg-emerald-50/);

    await page.locator('[data-testid="ficha-cerrar"]').click();
    await expect(page.locator(DIALOGO)).toHaveCount(0);

    // AL CERRAR SE RECARGA LA LISTA: marcar una nota no guarda la ficha, así que sin esa recarga la
    // tarjeta seguiría con el número de antes y el botón seguiría apagado.
    const tarjetaLista = page.locator('li').filter({ hasText: NOMBRE }).first();
    await expect(tarjetaLista).not.toContainText('cambio pedido', { timeout: 30000 });
    await expect(tarjetaLista).toContainText('ya puedes volver a enviarlo a revisión');
    await expect(reenviar).toBeEnabled();

    // ══ 8. Y el reenvío llega ═══════════════════════════════════════════════════════════════
    await reenviar.click();
    await expect(page.locator('text=quedó en revisión del equipo del PNMC')).toBeVisible({ timeout: 30000 });
    await expect(page.locator('li').filter({ hasText: NOMBRE }).first())
      .toContainText('Estado: En revisión', { timeout: 30000 });
    await expect(reenviar).toHaveCount(0);
  });
});
