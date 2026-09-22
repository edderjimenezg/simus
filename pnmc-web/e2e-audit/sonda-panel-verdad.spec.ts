import { test, expect, type Page } from '@playwright/test';
import { RUTA_SESION } from './sesion-compartida';
import { dentroDeLaVentana } from './dentro-de-la-ventana';

/**
 * ¿El panel dice la verdad cuando el servidor falla?
 *
 * Estas pruebas **interceptan las peticiones y las obligan a fallar**. Esa es
 * la clave: reproducen el defecto sin escribir ni una fila en la base de datos
 * compartida. Ninguna peticion de escritura llega jamas al servidor — se
 * responde desde el propio navegador con un 500.
 *
 * El defecto que defienden, medido el 2026-08-21 en
 * `admin-governance-panel.component.ts-176`: el callback de `error`
 * llamaba al mismo `updateLocal*` que el de `next`. El usuario leia
 * «El registro duplicado ha sido fusionado correctamente en base de datos»
 * cuando el servidor habia devuelto un error, la fila cambiaba en pantalla, y
 * al recargar volvia a «pendiente».
 */

const CREDENCIALES = {
  email: process.env.PNMC_ADMIN_EMAIL ?? 'admin@pnmc.local',
  password: process.env.PNMC_ADMIN_PASSWORD ?? 'admin',
};

test.use({ storageState: RUTA_SESION });

const RUTAS_GOBERNANZA = [
  '**/api/v1/admin/record-link-requests**',
  '**/api/v1/admin/duplicates**',
  '**/api/v1/admin/data-quality**',
  '**/api/v1/admin/data-quality-flags**',
];


/**
 * Datos sinteticos servidos DESDE EL NAVEGADOR.
 *
 * Sin esto las dos pruebas siguientes se saltan por falta de duplicados en el
 * entorno, y el defecto que defienden —el peor del panel— quedaria sin cubrir.
 * La respuesta se fabrica en la interceptacion: el servidor no participa y la
 * base no se toca.
 */
const DUPLICADO_SINTETICO = [
  {
    id: 'dup_sintetico_auditoria',
    nameA: 'REGISTRO SINTETICO A (auditoria)',
    nameB: 'REGISTRO SINTETICO B (auditoria)',
    department: 'Antioquia',
    municipality: 'Medellin',
    similarity: 0.93,
    status: 'pendiente',
  },
];

async function servirDuplicadoSintetico(page: Page) {
  await page.route('**/api/v1/admin/duplicates**', (route) => {
    if (route.request().method() === 'GET') {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(DUPLICADO_SINTETICO),
      });
    }
    return route.continue();
  });
}

/**
 * Reutiliza la cookie del proyecto `preparacion`. Ver la nota del limitador de
 * tasa en `sesion.setup.ts`.
 *
 * Las pruebas que fuerzan el rol `lider` interceptan igualmente `/auth/me`, asi
 * que la sesion compartida no les estorba: el rol se reescribe en la respuesta.
 */
async function entrar(page: Page) {
  await page.addInitScript((email) => {
    window.localStorage.setItem(`pnmc_tour_seen_${email}`, '1');
  }, CREDENCIALES.email);
  await page.goto('/administracion', { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(3000);
}

/**
 * Abre el panel de gobernanza en CUALQUIER tamano de pantalla.
 *
 * La primera version decidia por el ancho («si es menor de 1024, abre el
 * cajon») y esperaba 400 ms fijos. Era intermitente: fallaba en escritorio una
 * corrida y en las dos tablets la siguiente, siempre en el mismo punto —el
 * boton del sidebar no visible todavia—.
 *
 * El problema es el planteamiento, no el numero: 400 ms es una apuesta sobre
 * una animacion de 300 ms mas un renderizado. Ahora se espera a la CONDICION
 * («el boton esta visible») y solo se abre el cajon si hace falta de verdad.
 */
async function abrirGobernanza(page: Page) {
  const boton = page
    .getByRole('button', { name: 'Gestión de solicitudes y vinculaciones', exact: true })
    .first();

  // El ancho SI decide si existe cajon: por debajo de `lg` (1024) la barra es
  // deslizante y hay hamburguesa; por encima es fija y la hamburguesa esta en
  // el DOM pero oculta con `lg:hidden` — intentar pulsarla agota el tiempo.
  const esCajon = (page.viewportSize()?.width ?? 1920) < 1024;

  if (esCajon) {
    const menu = page.getByRole('button', { name: /Abrir men/i }).first();
    await menu.waitFor({ state: 'visible', timeout: 20_000 });
    // `isVisible()` NO sirve aqui: un cajon cerrado esta desplazado fuera de
    // pantalla y sus botones siguen reportandose como visibles.
    if (!(await dentroDeLaVentana(page, boton))) {
      await menu.click({ timeout: 15_000 });
    }
  }

  // Se espera a la CONDICION, no a un reloj: 400 ms fijos sobre una animacion
  // de 300 ms mas renderizado era una apuesta, y fallaba en un dispositivo
  // distinto en cada corrida.
  await boton.waitFor({ state: 'visible', timeout: 30_000 });
  await boton.click({ timeout: 20_000 });

  await page
    .getByRole('heading', { name: /Gesti[oó]n de solicitudes y vinculaciones/i })
    .first()
    .waitFor({ state: 'visible', timeout: 20_000 })
    .catch(() => undefined);
  await page.waitForTimeout(600);
}

/**
 * El panel abre en la pestana de solicitudes; los duplicados viven en otra.
 * Sin este paso el boton de fusionar no existe en el DOM y la prueba fallaba
 * creyendo que faltaban datos.
 */
async function abrirPestanaDuplicados(page: Page) {
  const pestana = page.locator('button', { hasText: /duplicad/i }).first();
  if (await pestana.count()) {
    await pestana.waitFor({ state: 'visible', timeout: 15_000 });
    await pestana.click();
    // Espera al boton que la pestana debe revelar, no a un reloj.
    await page
      .getByRole('button', { name: /Fusionar/i })
      .first()
      .waitFor({ state: 'visible', timeout: 15_000 })
      .catch(() => undefined);
  }
}

test('con el servidor caido, gobernanza NO inventa datos', async ({ page }) => {
  test.setTimeout(3 * 60_000);
  await entrar(page);

  // A partir de aqui, toda consulta de gobernanza falla.
  for (const r of RUTAS_GOBERNANZA) {
    await page.route(r, (route) =>
      route.fulfill({ status: 500, contentType: 'application/json', body: '{"error":"caida simulada"}' }),
    );
  }

  await abrirGobernanza(page);

  const estado = await page.evaluate(() => {
    const texto = document.body.innerText;
    return {
      // Los nombres inventados que el panel llevaba dentro
      inventados: [
        'Vientos del Sur',
        'Lutería Rosero',
        'Guacharaca',
        'Escuela de Música Tradicional Paz',
      ].filter((n) => texto.includes(n)),
      declaraElFallo: /No se pudieron cargar los datos del servidor/i.test(texto),
      filas: document.querySelectorAll('tbody tr').length,
    };
  });

  console.log('  con la API caida:', JSON.stringify(estado));

  expect(
    estado.inventados,
    'ANTES el panel mostraba «Fundacion Vientos del Sur» y companyia como si fueran registros reales',
  ).toEqual([]);
  expect(estado.declaraElFallo, 'el panel debe DECIR que no pudo cargar').toBe(true);
});

test('una accion que el servidor rechaza NO se anuncia como exito', async ({ page }) => {
  test.setTimeout(3 * 60_000);
  await servirDuplicadoSintetico(page);
  await entrar(page);
  await abrirGobernanza(page);
  await abrirPestanaDuplicados(page);

  // Toda escritura de gobernanza se responde con 500 DESDE EL NAVEGADOR.
  // El servidor no llega a verla: la base no se toca.
  let escriturasInterceptadas = 0;
  await page.route('**/api/v1/admin/duplicates/**', (route) => {
    if (route.request().method() !== 'GET') {
      escriturasInterceptadas++;
      return route.fulfill({ status: 500, contentType: 'application/json', body: '{"error":"rechazo simulado"}' });
    }
    return route.continue();
  });

  // La confirmacion nueva se ACEPTA aqui a proposito: queremos que la peticion
  // salga para que el 500 simulado la rechace. La peticion nunca llega al
  // servidor real.
  page.on('dialog', (d) => d.accept());

  const boton = page.getByRole('button', { name: /Fusionar/i }).first();
  expect(await boton.count(), 'debe haber un duplicado que fusionar').toBeGreaterThan(0);
  await boton.click();
  await page.waitForTimeout(2500);

  const aviso = await page.evaluate(() => {
    const texto = document.body.innerText;
    return {
      diceCorrectamente: /fusionado correctamente en base de datos/i.test(texto),
      diceQueFallo: /No se pudo completar la accion|No se pudo completar la acción/i.test(texto),
    };
  });

  console.log('  escrituras interceptadas:', escriturasInterceptadas, '| aviso:', JSON.stringify(aviso));

  expect(
    aviso.diceCorrectamente,
    'ANTES decia «fusionado correctamente en base de datos» con el servidor devolviendo 500',
  ).toBe(false);
  expect(aviso.diceQueFallo, 'debe decir que la accion no se completo').toBe(true);
});

test('fusionar pide confirmacion antes de tocar nada', async ({ page }) => {
  test.setTimeout(3 * 60_000);
  await servirDuplicadoSintetico(page);
  await entrar(page);
  await abrirGobernanza(page);
  await abrirPestanaDuplicados(page);

  const boton = page.getByRole('button', { name: /Fusionar/i }).first();
  expect(await boton.count(), 'debe haber un duplicado que fusionar').toBeGreaterThan(0);

  // Se RECHAZA la confirmacion: nada debe salir hacia el servidor.
  let peticionesDeEscritura = 0;
  await page.route('**/api/v1/admin/**', (route) => {
    if (route.request().method() !== 'GET') peticionesDeEscritura++;
    return route.continue();
  });

  let textoDelDialogo = '';
  page.on('dialog', (d) => {
    textoDelDialogo = d.message();
    d.dismiss();
  });

  await boton.click();
  await page.waitForTimeout(1500);

  console.log('  dialogo:', JSON.stringify(textoDelDialogo.slice(0, 90)));
  expect(textoDelDialogo, 'ANTES no pedia confirmacion ninguna').toContain('FUSIONAR');
  expect(
    peticionesDeEscritura,
    'al cancelar la confirmacion no debe salir NINGUNA escritura',
  ).toBe(0);
});

test('el Dashboard no inventa cobertura territorial', async ({ page }) => {
  test.setTimeout(3 * 60_000);

  // El monitor no responde. Antes daba igual: `departmentStats` devolvia ocho
  // departamentos con cifras fijas pasara lo que pasara.
  await page.route('**/api/v1/admin/monitor**', (route) =>
    route.fulfill({ status: 500, contentType: 'application/json', body: '{"error":"caida simulada"}' }),
  );

  // La seccion con los departamentos vive bajo `@if (userRole === 'lider')`.
  // `lider` es un rol FANTASMA: no esta declarado en ADMIN_ROLES y sin embargo
  // se usa en cuatro sitios del codigo, incluido el usuario de respaldo de
  // `admin-shell-page.component.ts`. Se fuerza por interceptacion, que es
  // la unica forma de llegar a ese camino sin crear una cuenta.
  // Tambien el login: la sesion se fija con SU respuesta, no con la de /me.
  await page.route('**/api/v1/admin/auth/login**', async (route) => {
    const respuesta = await route.fetch();
    let cuerpo: any = {};
    try { cuerpo = await respuesta.json(); } catch { cuerpo = {}; }
    if (cuerpo && cuerpo.user) cuerpo.user.role = 'lider';
    return route.fulfill({ status: respuesta.status(), contentType: 'application/json', body: JSON.stringify(cuerpo) });
  });

  await page.route('**/api/v1/admin/auth/me**', async (route) => {
    const respuesta = await route.fetch();
    let cuerpo: any = {};
    try { cuerpo = await respuesta.json(); } catch { cuerpo = {}; }
    if (cuerpo && cuerpo.user) cuerpo.user.role = 'lider';
    return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(cuerpo) });
  });

  await entrar(page);
  await page.waitForTimeout(3000);

  const estado = await page.evaluate(() => {
    const texto = document.body.innerText;
    // Acotado a la TARJETA territorial: los nombres de departamento aparecen
    // tambien en desplegables de DIVIPOLA con datos reales, y buscarlos en toda
    // la pagina daba un falso positivo («Boyaca»).
    const titulo = Array.from(document.querySelectorAll('h3')).find((h) =>
      /Distribuci[oó]n de Registros por Departamento/i.test(h.textContent || ''),
    );
    const tarjeta = titulo?.closest('div')?.parentElement;
    const textoTarjeta = tarjeta ? (tarjeta as HTMLElement).innerText : '';
    const inventados = ['Cundinamarca', 'Valle del Cauca', 'Atlántico', 'Bolívar', 'Boyacá']
      .filter((d) => textoTarjeta.includes(d));
    return {
      departamentosInventados: inventados,
      declaraVacio: /Sin distribución territorial disponible/i.test(texto),
      // los valores de respaldo que se colaban como KPI
      rolPintado: (document.body.innerText.match(/(webmaster|lider|gestor[_ ]?interno)/i) || ['?'])[0],
      hayTarjetaTerritorial: /Distribuci[oó]n de Registros por Departamento/i.test(document.body.innerText),
      kpisInventados: ['93', '32'].filter((n) => {
        const re = new RegExp('>\s*' + n + '\s*<');
        return re.test(document.body.innerHTML);
      }),
    };
  });

  console.log('  Dashboard con el monitor caido:', JSON.stringify(estado));

  expect(
    estado.departamentosInventados,
    'ANTES el Dashboard mostraba «Cundinamarca 24, 80 %» y siete departamentos mas, todos inventados',
  ).toEqual([]);
  expect(estado.declaraVacio, 'debe declarar que no hay reparto territorial').toBe(true);
});

test('el rol fantasma «lider» no revienta los paneles de registros', async ({ page }) => {
  test.setTimeout(4 * 60_000);

  // `lider` no existe en ADMIN_ROLES, pero se usa en cuatro sitios del codigo
  // —incluido el usuario de respaldo de admin-shell-page.component.ts—.
  // Para ese rol `getModulesForRole` devuelve [], `getSelectedModule` devuelve
  // undefined, y la plantilla hacia `{{ module.label }}` sin proteger.
  const forzarLider = async (route: any) => {
    const respuesta = await route.fetch();
    let cuerpo: any = {};
    try { cuerpo = await respuesta.json(); } catch { cuerpo = {}; }
    if (cuerpo && cuerpo.user) cuerpo.user.role = 'lider';
    return route.fulfill({ status: respuesta.status(), contentType: 'application/json', body: JSON.stringify(cuerpo) });
  };
  await page.route('**/api/v1/admin/auth/login**', forzarLider);
  await page.route('**/api/v1/admin/auth/me**', forzarLider);

  const errores: string[] = [];
  page.on('pageerror', (e) => errores.push(String(e).slice(0, 160)));

  await entrar(page);

  // «Mapa ecosistemico» y «Comunicaciones» se le muestran a `lider` porque no
  // declaran allowedRoles. Son justo los que abren el panel de registros.
  for (const etiqueta of ['Mapa ecosistémico', 'Comunicaciones']) {
    const ancho = page.viewportSize()?.width ?? 1920;
    if (ancho < 1024) {
      const menu = page.getByRole('button', { name: /Abrir men/i }).first();
      if (await menu.count()) { await menu.click(); await page.waitForTimeout(400); }
    }
    const b = page.getByRole('button', { name: etiqueta, exact: true }).first();
    if (!(await b.count())) continue;
    await b.click({ timeout: 15_000 });
    await page.waitForTimeout(2000);
  }

  const estado = await page.evaluate(() => ({
    pintaAlgo: document.body.innerText.trim().length > 80,
    diceNoDisponible: /Módulo no disponible/i.test(document.body.innerText),
  }));

  console.log('  con rol lider:', JSON.stringify(estado), '| errores JS:', errores.length, errores.slice(0, 2));

  expect(errores, 'ANTES la plantilla reventaba al leer module.label de un undefined').toEqual([]);
  expect(estado.pintaAlgo, 'la pantalla no puede quedarse en blanco').toBe(true);
});

test('cuando la sesion caduca, el panel cierra sesion y lo dice', async ({ page }) => {
  test.setTimeout(4 * 60_000);
  await entrar(page);

  // Ya dentro. A partir de ahora TODA ruta con sesion responde 401 — salvo las
  // de autenticacion, que es justo la distincion que hace el interceptor.
  await page.route('**/api/v1/admin/**', (route) => {
    if (/\/auth\/(login|logout|me)\b/.test(route.request().url())) return route.continue();
    return route.fulfill({ status: 401, contentType: 'application/json', body: '{"error":"sesion caducada"}' });
  });

  // Provocar una peticion: el boton «Actualizar» de la cabecera.
  const actualizar = page.getByRole('button', { name: /Actualizar/i }).first();
  if (await actualizar.count()) await actualizar.click();

  // El sondeo de fondo tambien dispara cada 10 s; se le da margen.
  await page.waitForTimeout(6000);

  const estado = await page.evaluate(() => {
    const texto = document.body.innerText;
    return {
      diceQueCaduco: /sesión caducó mientras trabajabas/i.test(texto),
      hayFormularioDeAcceso: !!document.querySelector('input[type="password"]'),
      sigueLaConsola: /Admin Console/i.test(texto) && !!document.querySelector('aside'),
    };
  });

  console.log('  tras el 401:', JSON.stringify(estado));

  expect(
    estado.diceQueCaduco,
    'ANTES solo escribia «Error de conexion» en un texto de 9,3 px de la barra lateral',
  ).toBe(true);
  expect(
    estado.hayFormularioDeAcceso,
    'ANTES la consola seguia pintada con isAuthenticated() en true',
  ).toBe(true);
});

/**
 * Esta prueba NECESITA arrancar sin sesion: comprueba que un login rechazado no
 * se confunde con una caducidad. Con la cookie compartida ni siquiera veria el
 * formulario, asi que se anula el estado para este bloque.
 */
test.describe('sin sesion previa', () => {
  test.use({ storageState: { cookies: [], origins: [] } });

  test('un 401 al iniciar sesion NO se confunde con caducidad', async ({ page }) => {
  test.setTimeout(3 * 60_000);

  // Credenciales rechazadas: el servidor responde 401 en /auth/login. Tratarlo
  // como caducidad le diria a alguien que aun no ha entrado que su sesion
  // expiro. Es el falso positivo que el interceptor debe evitar.
  await page.route('**/api/v1/admin/auth/login**', (route) =>
    route.fulfill({ status: 401, contentType: 'application/json', body: '{"error":"credenciales invalidas"}' }),
  );

  await page.addInitScript((email) => {
    window.localStorage.setItem(`pnmc_tour_seen_${email}`, '1');
  }, CREDENCIALES.email);
  await page.goto('/administracion', { waitUntil: 'domcontentloaded' });
  await page.locator('input[type="email"]').fill(CREDENCIALES.email);
  await page.locator('input[type="password"]').fill('contrasena-incorrecta');
  await page.getByRole('button', { name: 'Entrar al panel' }).click();
  await page.waitForTimeout(3000);

  const texto = await page.evaluate(() => document.body.innerText);
  console.log('  tras credenciales malas, ¿dice caducidad?', /sesión caducó/i.test(texto));

  expect(
    /sesión caducó mientras trabajabas/i.test(texto),
    'un login rechazado NO es una sesion caducada',
  ).toBe(false);
});
});
