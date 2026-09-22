import { test, expect, Page } from '@playwright/test';

/**
 * Tres Festivales de una misma organización, con tres, dos y cero ediciones.
 *
 * <b>QUÉ MIDE ESTO QUE NINGUNA PRUEBA DE KARMA PUEDE MEDIR.</b> Karma monta el diálogo solo, con un
 * doble del API: allí «guardar» es una llamada a un método que devuelve un objeto preparado. Aquí se
 * escribe en el formulario de verdad, sale una petición de verdad, y la fila queda en SQL Server. Lo
 * que se comprueba es la costura entera —formulario, contrato, servidor y base— y el recorrido
 * completo que pidió criterio: «creando 3 festivales, uno con 3 ediciones
 * otro con dos y uno sin ediciones».
 *
 * <b>DOS COSAS QUE ESTE RECORRIDO DESTAPÓ</b>, y que estaban mal antes de escribirlo:
 *
 *   1. Un Festival SIN ninguna edición no se podía crear. El alta encadenaba siempre las dos
 *      peticiones —cabecera y versión—, así que toda alta nacía con una edición vacía a la que el
 *      formulario le ponía el nombre del Festival. Ahora la versión solo se crea si hay algo escrito
 *      en los nueve pasos que la describen.
 *
 *   2. Una SEGUNDA edición no se podía añadir. El botón solo aparecía cuando no había ninguna, y
 *      «Editar el Festival» modifica la versión abierta en vez de crear otra: un Festival con una
 *      edición se quedaba con una para siempre.
 *
 * <b>LA REVISIÓN VISUAL NO ES UNA CAPTURA Y YA.</b> Las capturas quedan para mirarlas, pero lo que
 * falla la prueba es medible: que nadie pinte por encima del diálogo en sus cuatro esquinas, que el
 * cuerpo no se desplace en horizontal, y que el título del paso y el pie con «Guardar» estén dentro
 * de la ventana. Una captura sola no distingue «se ve bien» de «se ve bien en este tamaño».
 *
 * NECESITA EL ENTORNO LEVANTADO: `ng serve` en 4300 y la API en 8180, con la base sembrada. Vive
 * fuera de `npm test` y se corre con `npm run e2e`.
 */

const CUENTA = {
  email: process.env.PNMC_EXTERNO_EMAIL ?? 'externo@pnmc.local',
  password: process.env.PNMC_EXTERNO_PASSWORD ?? 'admin',
};

const DIALOGO = '[data-testid="ficha-festival-modal"]';

/**
 * Un sufijo distinto en cada corrida.
 *
 * LA BASE NO SE LIMPIA ENTRE CORRIDAS y los nombres se buscan por texto: sin esto, la segunda
 * corrida encontraría dos Festivales con el mismo nombre y el localizador fallaría por ambigüedad,
 * que es un fallo que no dice nada sobre el sistema.
 */
const MARCA = new Date().toISOString().replace(/[^0-9]/g, '').slice(8, 14);

/** Los tres Festivales del recorrido, con datos inventados pero coherentes entre sí. */
const FESTIVALES = [
  {
    clave: 'tambores',
    nombre: `Festival de Tambores del Bajo Cauca ${MARCA}`,
    descripcion: 'Encuentro de tamboras, alegres y llamadores de los municipios ribereños del Cauca.',
    correo: `tambores.${MARCA}@ejemplo.test`,
    celular: '3105558811',
    instagram: '@tamboresbajocauca',
    paginaWeb: 'https://tamboresbajocauca.example.org',
    alcance: 'nacional',
    ediciones: [
      { nombre: 'Tambores 2024 · Décima edición', inicio: '2024-08-15', fin: '2024-08-18' },
      { nombre: 'Tambores 2025 · Undécima edición', inicio: '2025-08-14', fin: '2025-08-17' },
      { nombre: 'Tambores 2026 · Duodécima edición', inicio: '2026-08-13', fin: '2026-08-16' },
    ],
  },
  {
    clave: 'cuerdas',
    nombre: `Encuentro de Cuerdas del Alto Magdalena ${MARCA}`,
    descripcion: 'Tiples, bandolas y guitarras de la región andina, con clínicas para agrupaciones jóvenes.',
    correo: `cuerdas.${MARCA}@ejemplo.test`,
    celular: '3216667722',
    instagram: '@cuerdasaltomagdalena',
    paginaWeb: 'https://cuerdasaltomagdalena.example.org',
    alcance: 'nacional',
    ediciones: [
      { nombre: 'Cuerdas 2025 · Primera edición', inicio: '2025-06-05', fin: '2025-06-08' },
      { nombre: 'Cuerdas 2026 · Segunda edición', inicio: '2026-06-04', fin: '2026-06-07' },
    ],
  },
  {
    clave: 'urbanas',
    nombre: `Muestra de Músicas Urbanas de Aguablanca ${MARCA}`,
    descripcion: 'Convocatoria de rap, dancehall y champeta urbana del oriente de Cali. Todavía sin fechas.',
    correo: `urbanas.${MARCA}@ejemplo.test`,
    celular: '3009994455',
    instagram: '@urbanasaguablanca',
    paginaWeb: 'https://urbanasaguablanca.example.org',
    alcance: 'nacional',
    ediciones: [],
  },
];

test.describe('Tres Festivales con tres, dos y cero ediciones', () => {
  test.describe.configure({ mode: 'serial', timeout: 240_000 });

  test('el recorrido completo, de punta a punta', { tag: '@backend' }, async ({ page }) => {
    // Se entra por la API y no por el formulario de acceso: lo que se mide es el recorrido del
    // Festival, y recorrer la pantalla de entrada solo añadiría motivos ajenos por los que fallar.
    // `page.request` comparte el tarro de cookies del navegador, así que la sesión queda puesta.
    const entrada = await page.request.post('/api/v1/external/auth/login', {
      data: { email: CUENTA.email, password: CUENTA.password },
    });
    expect(entrada.status(), 'la cuenta externa de prueba tiene que poder entrar').toBe(200);

    await page.goto('/ecosistema/mi-panel?pestana=ecosistema');
    await esperarLaAnimacionDeEntrada(page);

    for (const festival of FESTIVALES) {
      await test.step(`crear «${festival.nombre}»`, async () => {
        await crearFestival(page, festival);
      });
    }

    for (const festival of FESTIVALES) {
      await test.step(`comprobar «${festival.nombre}» con ${festival.ediciones.length} edición(es)`, async () => {
        await comprobarFestival(page, festival);
      });
    }
  });
});

// ─────────────────────────────── El recorrido ───────────────────────────────

type Festival = (typeof FESTIVALES)[number];

async function crearFestival(page: Page, festival: Festival): Promise<void> {
  await page.locator('[data-testid="abrir-crear-festival"]').click();
  const dialogo = page.locator(DIALOGO);
  await expect(dialogo).toBeVisible();
  await expect(page.locator('[data-testid="ficha-titulo"]')).toHaveText('Crear un Festival');

  // ── Los datos básicos, que son los únicos obligatorios ────────────────────────────────────
  // Desde crear, leer y editar son la misma pantalla: dos pestañas, y
  // dentro de cada una los pasos con su círculo. Al rellenar se despliega uno a la vez.
  await expect(page.locator('[data-testid="pestana-festival"]')).toHaveAttribute('aria-selected', 'true');
  await expect(page.locator('[data-testid="panel-insignia"]')).toHaveText('Dato del Festival');

  // LOS TRES ENCABEZADOS DEL FESTIVAL, TODOS A LA VISTA Y TODOS PLEGADOS. «El estado natural son
  // todos cerrados», del usuario: los encabezados son el índice de la ficha.
  await expect(page.locator('[data-testid^="abrir-paso-"]')).toHaveCount(3);
  for (const paso of ['generales', 'contacto-festival', 'musica-festival']) {
    await expect(page.locator(`[data-testid="abrir-paso-${paso}"]`))
      .toHaveAttribute('aria-expanded', 'false');
  }
  await expect(page.locator('[data-testid="bloque-generales"]')).toHaveCount(0);
  await expect(page.locator('[data-testid="bloque-material"]')).toHaveCount(0);

  await page.locator('[data-testid="abrir-paso-generales"]').click();
  await expect(page.locator('[data-testid="bloque-generales"]')).toBeVisible();

  // Y «GUARDAR BORRADOR» ESTÁ DESDE EL PRIMER MOMENTO: ningún paso bloquea al siguiente.
  await expect(page.locator('[data-testid="ficha-guardar"]')).toBeVisible();

  await page.fill('[name="nombre"]', festival.nombre);
  await page.fill('[name="descripcionFestival"]', festival.descripcion);
  await page.selectOption('[name="nivelCobertura"]', festival.alcance);

  await revisarLaVista(page, `${festival.clave}-datos-basicos`);

  // El contacto del Festival tiene su propio paso: son las siete columnas de contacto de
  // `ART_MUS_FESTIVALES`, y se confundían con las de la edición, que se llaman casi igual.
  await page.locator('[data-testid="abrir-paso-contacto-festival"]').click();
  await expect(page.locator('[data-testid="bloque-contacto-festival"]')).toBeVisible();

  // Y EL MISMO MANDO PLIEGA: «el hamburguesa también debe abrir con clic y retraerse igual con
  // clic», del usuario. Se comprueba sobre el paso que ya no hace falta, y se deja plegado.
  await page.locator('[data-testid="abrir-paso-generales"]').click();
  await expect(page.locator('[data-testid="bloque-generales"]')).toHaveCount(0);

  await page.fill('[name="correoContactoFestival"]', festival.correo);
  await page.fill('[name="telefonoCelular"]', festival.celular);
  await page.fill('[name="instagramFestival"]', festival.instagram);
  await page.fill('[name="paginaWeb"]', festival.paginaWeb);

  await revisarLaVista(page, `${festival.clave}-contacto`);

  // ── La pestaña de Ediciones: solo el botón hasta que se pide una ──────────────────────────
  await page.locator('[data-testid="pestana-edicion"]').click();
  await expect(page.locator('[data-testid="ficha-sin-ediciones"]')).toBeVisible();
  await expect(page.locator('[data-testid="bloque-edicion"]')).toHaveCount(0);
  await revisarLaVista(page, `${festival.clave}-pestana-ediciones`);

  const primera = festival.ediciones[0];
  if (primera) {
    await page.locator('[data-testid="ficha-crear-primera-edicion"]').click();
    // ABRE POR EL PRIMER PASO DE LOS SEIS, con los seis círculos a la vista.
    await expect(page.locator('[data-testid="bloque-edicion"]')).toBeVisible();
    await expect(page.locator('[data-testid^="abrir-paso-"]')).toHaveCount(6);
    await expect(page.locator('[data-testid="bloque-material"]')).toHaveCount(0);
    await expect(page.locator('[data-testid="panel-insignia"]')).toHaveText('Dato de la edición');
    await escribirLaEdicion(page, primera);
    await revisarLaVista(page, `${festival.clave}-edicion`);
  }
  // SIN EDICIÓN SE GUARDA IGUAL, y desde donde se esté: no es obligatoria ni para el borrador ni
  // para la revisión.

  await page.locator('[data-testid="ficha-guardar"]').click();

  if (primera) {
    await expect(page.locator('[data-testid="ficha-mensaje"]'))
      .toContainText('con su primera edición', { timeout: 20_000 });
  } else {
    // EL CASO QUE NO EXISTÍA. Sin nada escrito en las nueve secciones de la edición, el Festival
    // queda creado y sin ninguna.
    await expect(page.locator('[data-testid="ficha-mensaje"]'))
      .toContainText('Todavía no tiene ninguna edición', { timeout: 20_000 });
    await expect(page.locator('[data-testid="ficha-sin-ediciones"]')).toBeVisible();
  }

  // ── Las ediciones restantes, una a una ────────────────────────────────────────────────────
  for (const edicion of festival.ediciones.slice(1)) {
    await page.locator('[data-testid="pestana-edicion"]').click();
    await page.locator('[data-testid="ficha-anadir-edicion"]').click();
    await expect(page.locator('[data-testid="bloque-edicion"]')).toBeVisible();
    await expect(page.locator('[data-testid^="abrir-paso-"]')).toHaveCount(6);
    await escribirLaEdicion(page, edicion);
    await page.locator('[data-testid="ficha-guardar"]').click();
    await expect(page.locator('[data-testid="ficha-mensaje"]'))
      .toContainText('quedó creada como borrador', { timeout: 20_000 });
  }

  await revisarLaVista(page, `${festival.clave}-ficha`);
  await page.locator('[data-testid="ficha-cerrar"]').click();
  await expect(page.locator(DIALOGO)).toHaveCount(0);
}

async function escribirLaEdicion(
  page: Page,
  edicion: { nombre: string; inicio: string; fin: string },
): Promise<void> {
  await page.fill('[name="nombreEdicion"]', edicion.nombre);
  await page.fill('[name="fechaInicio"]', edicion.inicio);
  await page.fill('[name="fechaFin"]', edicion.fin);
  await page.fill(
    '[name="descripcionEdicion"]',
    `Programación de ${edicion.nombre}: conciertos, talleres y una rueda de gestores.`,
  );
}

async function comprobarFestival(page: Page, festival: Festival): Promise<void> {
  // Se recarga para leer del servidor y no de lo que quedó en memoria del diálogo anterior: lo que
  // se está comprobando es que las filas QUEDARON escritas.
  await page.goto('/ecosistema/mi-panel?pestana=ecosistema');
  await esperarLaAnimacionDeEntrada(page);

  const tarjeta = page.locator('article, li, div').filter({ hasText: festival.nombre }).last();
  await expect(tarjeta).toContainText(festival.nombre);

  // LOS TRES ESTÁN EN LA LISTA DE LA MISMA ORGANIZACIÓN. Es la mitad positiva de la regla que el
  // usuario pidió: «una organización puede tener muchos festivales».
  for (const otro of FESTIVALES) {
    await expect(page.locator(`button[aria-label="Abrir la ficha completa: ${otro.nombre}"]`)).toHaveCount(1);
  }

  await page.locator(`button[aria-label="Abrir la ficha completa: ${festival.nombre}"]`).click();
  await expect(page.locator(DIALOGO)).toBeVisible();

  // LOS DATOS GENERALES SE LEEN, y son los que se escribieron. HAY QUE DESPLEGARLOS: desde el 29 de
  // agosto de 2026 la ficha abre plegada también al leer, y los encabezados son su índice.
  for (const paso of ['generales', 'contacto-festival', 'musica-festival']) {
    await page.locator(`[data-testid="abrir-paso-${paso}"]`).click();
  }
  const generales = page.locator('[data-testid="panel-festival"]');
  await expect(generales).toContainText(festival.nombre);
  await expect(generales).toContainText(festival.descripcion);
  // Y EL CONTACTO DEL FESTIVAL SE LEE EN EL MISMO BLOQUE, aparte del de la edición.
  await expect(generales).toContainText(festival.correo);

  // LA LECTURA VA POR PESTAÑAS, con ediciones y sin ellas: es la misma pantalla que el
  // formulario. «Cuando se crea un festival también mantengamos esta estructura, así
  // homogenizamos», del usuario.
  if (festival.ediciones.length === 0) {
    await page.locator('[data-testid="pestana-edicion"]').click();
    await expect(page.locator('[data-testid="ficha-sin-ediciones"]')).toBeVisible();
    await expect(page.locator('[data-testid="ficha-tarjetas-edicion"]')).toHaveCount(0);
  } else {
    await expect(page.locator('[data-testid="pestana-edicion"]'))
      .toContainText(String(festival.ediciones.length));
    await page.locator('[data-testid="pestana-edicion"]').click();

    // LA FICHA DICE CUÁNTAS EDICIONES TIENE, con el número: es lo que ata
    // `ART_MUS_FESTIVALES_VERSION.ID_FESTIVAL`, y «varias» no se comprueba de un vistazo.
    await expect(page.locator('[data-testid="ficha-cuantas-ediciones"]'))
      .toContainText(String(festival.ediciones.length));

    // UNA TARJETA POR EDICIÓN, MÁS LA DE «REGISTRAR OTRA». Es la forma de navegar que pidió el
    // usuario: «un tipo tarjeta con el nombre, algo básico, y un botón
    // abajo, editar; y al lado todas las tarjetas si tiene más ediciones».
    const tarjetas = page.locator('[data-testid="ficha-tarjetas-edicion"] > li');
    await expect(tarjetas).toHaveCount(festival.ediciones.length + 1);
    await expect(page.locator('[data-testid="ficha-anadir-edicion"]')).toBeVisible();

    // LA TARJETA DESPLEGADA ES LA MÁS RECIENTE —el servidor las devuelve de mayor a menor número— y
    // se marca con `aria-current`. Sin esa marca, con tres tarjetas iguales nada diría cuál de las
    // tres es la que se está leyendo debajo.
    const desplegada = page.locator('[data-testid^="ficha-edicion-"][aria-current="true"]');
    await expect(desplegada).toHaveCount(1);
    await expect(desplegada).toContainText(`Edición ${festival.ediciones.length}`);

    // AL LEER TAMBIÉN NACE PLEGADA, y también se despliega con el mismo mando: los seis
    // encabezados están, y sus cuerpos no hasta que se piden.
    for (const paso of ['edicion', 'organizacion', 'contacto-edicion', 'practicas', 'localizacion', 'anexos']) {
      await expect(page.locator(`[data-testid="paso-${paso}"]`)).toBeVisible();
      await expect(page.locator(`[data-testid="abrir-paso-${paso}"]`))
        .toHaveAttribute('aria-expanded', 'false');
    }
    // SE DESPLAZA ANTES DE PULSAR: el cuerpo del diálogo es lo único que rueda, y con las tarjetas
    // de las ediciones arriba el encabezado del paso 1 queda por debajo del pliegue.
    const mando = page.locator('[data-testid="abrir-paso-edicion"]');
    await mando.scrollIntoViewIfNeeded();
    await mando.click();
    await expect(page.locator('[data-testid="cuerpo-paso-edicion"]')).toBeVisible();
    await expect(page.locator('[data-testid="seccion-edicion"]')).toBeVisible();

    // CADA TARJETA ENSEÑA SU PROPIO NOMBRE: si el alta hubiera reutilizado la misma fila, las tres
    // dirían lo mismo.
    for (const edicion of festival.ediciones) {
      await expect(tarjetas.filter({ hasText: edicion.nombre })).toHaveCount(1);
    }

    // Y CADA UNA SE PUEDE DESPLEGAR. Con una sola comprobación sobre la primera, dos ediciones que
    // compartieran fila pasarían igual.
    for (const edicion of festival.ediciones) {
      const tarjeta = tarjetas.filter({ hasText: edicion.nombre })
        .locator('[data-testid^="ficha-edicion-"]');
      await tarjeta.click();
      await expect(page.locator('[data-testid="panel-edicion"]')).toBeVisible();
      await expect(page.locator('[data-testid="panel-edicion"]')).toContainText(edicion.nombre);
    }
  }

  await revisarLaVista(page, `${festival.clave}-comprobacion`);
  await page.locator('[data-testid="ficha-cerrar"]').click();
}

// ─────────────────────────────── La revisión visual ───────────────────────────────

/**
 * Lo que se mira en cada parada, y por qué se mira así.
 *
 * UNA CAPTURA NO FALLA NUNCA. Por eso además de guardarla se comprueban cuatro cosas que sí pueden
 * ponerse en rojo, y que son las tres formas conocidas en que este diálogo se ha roto antes: algo
 * pintado por encima (la barra de navegación lo tapaba), el cuerpo desplazándose en horizontal, y
 * los botones fuera de la ventana.
 */
async function revisarLaVista(page: Page, nombre: string): Promise<void> {
  const dialogo = page.locator(DIALOGO);
  await expect(dialogo).toBeVisible();

  const tapado = await page.evaluate((selector) => {
    const nodo = document.querySelector(selector) as HTMLElement | null;
    if (!nodo) return ['no hay diálogo'];
    const r = nodo.getBoundingClientRect();
    const puntos: [string, number, number][] = [
      ['borde superior', r.left + r.width / 2, r.top + 3],
      ['borde izquierdo', r.left + 3, r.top + r.height / 2],
      ['borde derecho', r.right - 3, r.top + r.height / 2],
      ['borde inferior', r.left + r.width / 2, r.bottom - 3],
    ];
    return puntos
      .filter(([, x, y]) => {
        const encima = document.elementFromPoint(x, y);
        return !encima || !nodo.contains(encima);
      })
      .map(([donde, x, y]) => {
        const encima = document.elementFromPoint(x, y) as HTMLElement | null;
        return `${donde} (${Math.round(x)},${Math.round(y)}) lo tapa <${encima?.tagName.toLowerCase() ?? 'nada'}>`;
      });
  }, DIALOGO);
  expect(tapado, `algo se pinta por delante del diálogo en «${nombre}»`).toEqual([]);

  const medidas = await page.evaluate((selector) => {
    const nodo = document.querySelector(selector) as HTMLElement | null;
    if (!nodo) return null;
    const cuerpo = nodo.querySelector('.overflow-y-auto') as HTMLElement | null;
    const r = nodo.getBoundingClientRect();
    return {
      desbordaEnHorizontal: cuerpo ? cuerpo.scrollWidth > cuerpo.clientWidth + 1 : false,
      desbordaLaPagina: document.documentElement.scrollWidth > window.innerWidth + 1,
      dentroDeLaVentana: r.top >= 0 && r.bottom <= window.innerHeight + 1,
      alto: Math.round(r.height),
    };
  }, DIALOGO);

  expect(medidas, `no se pudo medir el diálogo en «${nombre}»`).not.toBeNull();
  expect(medidas!.desbordaEnHorizontal, `el cuerpo del diálogo se desplaza en horizontal en «${nombre}»`).toBe(false);
  expect(medidas!.desbordaLaPagina, `la página entera se desplaza en horizontal en «${nombre}»`).toBe(false);
  expect(medidas!.dentroDeLaVentana, `el diálogo se sale de la ventana en «${nombre}» (alto ${medidas!.alto}px)`).toBe(true);

  // El pie tiene que estar a la vista: es donde vive «Guardar», y el defecto que se arregló el 28 de
  // agosto era justamente que la cabecera del diálogo quedaba cortada.
  await expect(page.locator('[data-testid="ficha-cerrar"]')).toBeInViewport();

  await page.screenshot({ path: `e2e/capturas/festivales-${nombre}.png`, fullPage: false });
}

/**
 * `pageSlideUp` corre 600 ms sobre `<main>` y, mientras corre, lo convierte en un contexto de
 * apilamiento: el diálogo quedaría atrapado debajo de la barra igual que antes del arreglo. Fuera de
 * esa ventana ya no lo es. Sin esta espera, la comprobación de «quién pinta encima» sería una
 * carrera contra la animación.
 */
async function esperarLaAnimacionDeEntrada(page: Page): Promise<void> {
  await page.waitForFunction(() => {
    const principal = document.getElementById('contenido-principal');
    return !!principal
      && principal.getAnimations().every(a => a.playState === 'finished' || a.playState === 'idle');
  });
}
