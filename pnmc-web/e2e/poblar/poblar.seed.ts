import { test, expect, Page } from '@playwright/test';
import { CLAVE, ORGANIZACIONES, FESTIVALES, Organizacion, Festival } from './datos';

/**
 * Puebla la base local: 18 organizaciones y 18 Festivales, creados POR LA INTERFAZ.
 *
 * POR QUÉ POR LA INTERFAZ Y NO POR SQL. Sembrar por SQL llena la base y no comprueba nada: los
 * defectos que se buscan —un campo que no guarda, una regla que no se aplica, un botón que responde
 * 400— solo aparecen al pulsar. Este guion es a la vez el sembrado y la comprobación del circuito.
 *
 * DEJA CAPTURA DE CADA PASO en `e2e/capturas/poblar/`. No es adorno: es lo que se mira para saber
 * si la pantalla decía la verdad en el momento de guardar.
 *
 * Se corre con su propia configuración, nunca con `npm run e2e`:
 *
 *   npx playwright test -c playwright.poblar.config.ts
 *
 * Con `POBLAR_SOLO=1` hace solo la primera organización: es el piloto que se mira antes de soltar
 * las dieciocho. Es RE-EJECUTABLE: si la cuenta ya existe entra en vez de darla de alta, y si el
 * Festival ya existe no lo repite.
 */

const CAPTURAS = 'e2e/capturas/poblar';
const CUANTAS = Number(process.env.POBLAR_SOLO ?? '0');

/** Lo que se encuentre mal por el camino. Se cuenta al final y no se traga. */
const HALLAZGOS: string[] = [];

let paso = 0;

async function foto(page: Page, nombre: string): Promise<void> {
  paso += 1;
  await page.screenshot({
    path: `${CAPTURAS}/${String(paso).padStart(3, '0')}-${nombre}.png`,
    animations: 'disabled',
  });
}

const apodo = (org: Organizacion) => org.correo.split('@')[0];

/**
 * Espera a que se vaya la pantalla de error del servidor de desarrollo.
 *
 * `ng serve` RECOMPILA CUANDO ALGUIEN GUARDA UN FICHERO, y mientras tanto pinta
 * `<vite-error-overlay>`, que cubre la ventana entera y se traga los clics. En este repositorio hay
 * sesiones trabajando en paralelo, así que puede aparecer en cualquier momento y sin que este guion
 * haya tocado nada. Murió así en la segunda organización, con el botón «Guardar» debajo.
 */
async function sinOverlay(page: Page): Promise<void> {
  const overlay = page.locator('vite-error-overlay');
  if (await overlay.count() === 0) return;
  await expect(overlay).toHaveCount(0, { timeout: 180000 });
  await page.waitForTimeout(1500);
}

/** Pulsa esperando antes a que no haya nada encima. */
async function pulsar(page: Page, locator: ReturnType<Page['locator']>): Promise<void> {
  await sinOverlay(page);
  await locator.click();
}

/** Rellena un campo solo si la pantalla lo tiene. Un campo ausente es un hallazgo, no una excepción. */
async function rellenar(page: Page, selector: string, valor: string, donde: string): Promise<void> {
  const control = page.locator(selector).first();
  if (await control.count() === 0) {
    HALLAZGOS.push(`${donde}: no existe el campo ${selector}`);
    return;
  }
  await control.fill(valor);
}

async function elegir(page: Page, selector: string, valor: string, donde: string): Promise<void> {
  const control = page.locator(selector).first();
  if (await control.count() === 0) {
    HALLAZGOS.push(`${donde}: no existe el desplegable ${selector}`);
    return;
  }
  try {
    await control.selectOption(valor);
  } catch {
    HALLAZGOS.push(`${donde}: el desplegable ${selector} no admite «${valor}»`);
  }
}

/** ¿Hay sesión externa abierta? La cabecera pinta «MI CUENTA» cuando la hay. */
async function haySesion(page: Page): Promise<boolean> {
  return (await page.getByText('MI CUENTA', { exact: false }).count()) > 0;
}

async function salir(page: Page): Promise<void> {
  await page.context().clearCookies();
}

/**
 * Entra con la cuenta de la organización, y si no existe la da de alta.
 *
 * SE INTENTA ENTRAR PRIMERO para que el guion se pueda repetir. `UX_Entidades_CorreoContacto`
 * impide dos organizaciones con el mismo correo, así que un alta repetida responde 400 y el guion
 * moriría en la segunda corrida.
 */
async function entrarODarDeAlta(page: Page, org: Organizacion): Promise<'entro' | 'alta'> {
  await salir(page);
  const respuesta = await page.request.post('/api/v1/external/auth/login', {
    data: { email: org.correo, password: CLAVE },
    failOnStatusCode: false,
  });
  if (respuesta.status() === 200) {
    await page.goto('/ecosistema/mi-panel?pestana=organizacion');
    return 'entro';
  }

  await page.goto('/registro');
  await page.getByRole('tab', { name: 'Registrarse' }).click();
  await page.fill('[name="fullName"]', org.responsable);
  await page.fill('[name="numeroDocumento"]', org.documentoResponsable);
  await page.fill('[name="email"]', org.correo);
  await page.fill('[name="phone"]', org.telefono);
  await page.fill('[name="organizationName"]', org.nombre);
  await page.fill('[name="organizationIdentificationNumber"]', org.nit);
  await page.fill('[name="password"]', CLAVE);
  await page.fill('[name="confirmPassword"]', CLAVE);
  await page.check('[name="terms"]');
  await page.check('[name="dataPolicy"]');
  await page.getByRole('button', { name: /registrar organizaci/i }).first().click();

  await page.waitForTimeout(4000);
  if (!(await haySesion(page))) {
    await foto(page, `alta-fallida-${apodo(org)}`);
    HALLAZGOS.push(`alta de ${org.nombre}: no quedó sesión abierta después de registrar`);
  }
  return 'alta';
}

/**
 * Completa el perfil de la organización: los datos que el alta no pregunta.
 *
 * EL ALTA NO PIDE TERRITORIO NI REDES y la propia pantalla dice por qué: el territorio es de cada
 * proceso que la organización monte, no de ella. Aquí es donde se rellenan.
 */
async function completarPerfil(page: Page, org: Organizacion): Promise<void> {
  await page.goto('/ecosistema/mi-panel?pestana=organizacion', { waitUntil: 'domcontentloaded' });
  await sinOverlay(page);

  // SI YA ESTÁ COMPLETO, NO SE VUELVE A TOCAR. En una segunda corrida esto reabría los diecisiete
  // perfiles buenos para reescribir lo mismo: minutos de más y una oportunidad de romper algo que
  // ya estaba bien. El teléfono sirve de testigo porque el alta no lo escribe: si está, lo puso
  // esta función.
  const lectura = page.locator('[data-testid="perfil-lectura"]');
  if (await lectura.count() > 0 && (await lectura.innerText()).includes(org.telefono)) return;

  const editar = page.locator('[data-testid="perfil-editar"]');
  // SE ESPERA, NO SE CUENTA. Recién dada de alta la organización, el panel tarda en pintar; con
  // `count()` el botón «Editar» daba cero y la segunda organización se quedó SIN PERFIL —cobertura
  // «sin_definir», teléfono y redes en NULL— sin que nada lo dijera.
  try {
    await expect(editar).toBeVisible({ timeout: 120000 });
  } catch {
    await foto(page, `sin-perfil-${apodo(org)}`);
    HALLAZGOS.push(`${org.nombre}: no aparece «Editar» en el perfil de la organización`);
    return;
  }
  await pulsar(page, editar);

  const donde = `perfil de ${org.nombre}`;
  await rellenar(page, '[name="nombre"]', org.nombre, donde);
  await rellenar(page, '[name="nombreLegal"]', org.nombreLegal, donde);
  await rellenar(page, '[name="numeroIdentificacion"]', org.nit, donde);
  await rellenar(page, '[name="descripcion"]', org.descripcion, donde);
  await rellenar(page, '[name="correoContacto"]', org.correo, donde);
  await rellenar(page, '[name="telefonoContacto"]', org.telefono, donde);
  await rellenar(page, '[name="direccion"]', org.direccion, donde);
  await rellenar(page, '[name="sitioWeb"]', org.sitioWeb, donde);
  await rellenar(page, '[name="instagram"]', org.instagram, donde);
  await rellenar(page, '[name="facebook"]', org.facebook, donde);
  await rellenar(page, '[name="otroEnlace"]', org.otroEnlace, donde);

  await elegir(page, '[name="nivelCobertura"]', 'municipal', donde);
  await elegir(page, '[name="codigoDepartamento"]', org.departamento, donde);
  await page.waitForTimeout(600);
  await elegir(page, '[name="codigoMunicipio"]', org.municipio, donde);

  await foto(page, `perfil-lleno-${apodo(org)}`);
  await pulsar(page, page.getByRole('button', { name: /guardar/i }).first());

  // EL PERFIL GUARDADO VUELVE A LECTURA. Sin comprobarlo, un guardado rechazado se veía igual que
  // uno correcto: el guion seguía y la organización quedaba a medias en la base.
  try {
    await expect(page.locator('[data-testid="perfil-lectura"]')).toBeVisible({ timeout: 60000 });
  } catch {
    await foto(page, `perfil-NO-guardo-${apodo(org)}`);
    HALLAZGOS.push(`${org.nombre}: el perfil no volvió a lectura después de guardar`);
    return;
  }
  await foto(page, `perfil-guardado-${apodo(org)}`);
}

/**
 * Abre la pestaña de Festivales del panel y ESPERA a que la lista exista.
 *
 * `count()` NO ESPERA: devuelve lo que haya en ese instante. Con el panel tardando entre 2 y 26
 * segundos en pintar, preguntar por un botón nada más navegar da cero y parece que el botón no
 * existe. Fue el primer falso hallazgo de este guion.
 */
async function abrirFestivales(page: Page): Promise<void> {
  // SE REINTENTA PORQUE LA APLICACIÓN NAVEGA SOLA. Después de enviar un Festival a revisión el
  // panel se recarga por su cuenta, y un `goto` a la misma dirección en ese instante muere con
  // «Navigation ... is interrupted by another navigation». No es un fallo del sitio: son dos
  // navegaciones a la misma pantalla compitiendo.
  for (let intento = 1; intento <= 3; intento++) {
    try {
      await page.goto('/ecosistema/mi-panel?pestana=ecosistema', { waitUntil: 'domcontentloaded' });
      break;
    } catch (error) {
      if (intento === 3) throw error;
      await page.waitForTimeout(2000);
    }
  }
  await sinOverlay(page);
  await expect(page.locator('[data-testid="abrir-crear-festival"]')).toBeVisible({ timeout: 120000 });

  // Y SE ESPERA A QUE LA LISTA HAYA RESUELTO. El botón de crear aparece antes que los Festivales:
  // preguntar ahí si el Festival ya existe daba «no» y lo creaba otra vez. Así nació el duplicado
  // «Festival Francisco el Hombre» —dos filas, 206 y 207, en la misma organización—.
  await expect(
    page.getByText(/REGISTRADOS?|Todavía no|Aún no/i).first(),
  ).toBeVisible({ timeout: 120000 });
}

/**
 * Crea el Festival con su primera edición y lo manda a revisión.
 *
 * CREAR Y ENVIAR SON DOS COSAS. La primera versión de este guion se saltaba las DOS cuando el
 * Festival ya existía, y dejó «Francisco el Hombre» en borrador para siempre: se había creado en
 * una corrida y el envío había fallado en esa misma corrida. Ahora, si ya existe, se salta la
 * creación y se intenta el envío igual.
 */
async function yaExiste(page: Page, nombre: string): Promise<boolean> {
  // `GET /externo/organizaciones/mis` devuelve `[{ id, nombre }]`: comprobado contra la API local
  // con la cuenta `pruebas@pnmc.local`.
  const mias = await page.request.get('/api/v1/externo/organizaciones/mis', { failOnStatusCode: false });
  if (mias.status() !== 200) return false;
  const organizaciones = await mias.json() as { id: string }[];
  if (organizaciones.length === 0) return false;

  const lista = await page.request.get(
    `/api/v1/externo/organizaciones/${organizaciones[0].id}/festivales`,
    { failOnStatusCode: false, timeout: 180000 },
  );
  if (lista.status() !== 200) return false;
  const festivales = await lista.json() as { nombre: string }[];
  return festivales.some(f => f.nombre === nombre);
}

async function crearFestival(page: Page, fest: Festival): Promise<void> {
  const clave = fest.nombre.slice(0, 24);
  await abrirFestivales(page);

  // ¿YA EXISTE? SE LE PREGUNTA AL SERVIDOR, NO A LA PANTALLA.
  //
  // Se preguntaba a la pantalla y creó OCHO DUPLICADOS: mirar si hay un encabezado con el nombre
  // del Festival responde «no» mientras la lista sigue cargando, y el guion lo creaba otra vez. Se
  // intentó esperar al texto «N REGISTRADOS», y tampoco basta, porque ese recuento se pinta con
  // cero antes de que llegue la respuesta. La lista del servidor no tiene estados intermedios.
  //
  // Y CONVIENE SABERLO: la aplicación NO impide dos Festivales con el mismo nombre en la misma
  // organización. Los ocho duplicados se guardaron sin una sola advertencia.
  if (await yaExiste(page, fest.nombre)) {
    await enviarARevision(page, fest, clave);
    return;
  }

  await pulsar(page, page.locator('[data-testid="abrir-crear-festival"]'));
  await expect(page.locator('[data-testid="ficha-festival-modal"]')).toBeVisible();

  // ── Paso 1: datos generales ──────────────────────────────────────────────────────────────
  await page.locator('[data-testid="abrir-paso-generales"]').click();
  const donde = `ficha de ${clave}`;
  await rellenar(page, '[name="nombre"]', fest.nombre, donde);
  await rellenar(page, '[name="descripcionFestival"]', fest.descripcion, donde);
  await rellenar(page, '[name="periodicidad"]', fest.periodicidad, donde);
  // EL TERRITORIO DEPENDE DEL ALCANCE, y no es cosa de la pantalla: lo impone la base. El
  // `CHECK` de `NivelCobertura` —leído— dice que `nacional` exige
  // departamento y municipio en NULL, `departamental` exige departamento y municipio en NULL, y
  // solo `municipal` admite los dos. Y admite TRES valores: municipal, departamental y nacional.
  // «internacional» NO EXISTE: lo tenía escrito en los datos, la ficha lo ignoró, se quedó en
  // «Municipal» sin municipio y el guardado murió con «Revisa los campos marcados».
  await elegir(page, '[name="nivelCobertura"]', fest.nivelCobertura, donde);
  await page.waitForTimeout(500);
  if (fest.nivelCobertura !== 'nacional') {
    await elegir(page, '[name="codigoDepartamento"]', fest.departamento, donde);
    await page.waitForTimeout(800);
  }
  if (fest.nivelCobertura === 'municipal') {
    await elegir(page, '[name="codigoMunicipio"]', fest.municipio, donde);
  }
  await foto(page, `festival-generales-${clave}`);

  // ── Paso 2: contacto y redes del Festival ────────────────────────────────────────────────
  const contacto = page.locator('[data-testid="abrir-paso-contacto-festival"]');
  if (await contacto.count() === 0) {
    HALLAZGOS.push(`${donde}: no existe el paso «contacto-festival»`);
  } else {
    await contacto.click();
    await rellenar(page, '[name="correoContactoFestival"]', fest.correoContacto, donde);
    const apodoWeb = clave.toLowerCase().normalize('NFD').replace(/[^a-z]+/g, '');
    await rellenar(page, '[name="paginaWeb"]', `https://${apodoWeb}.org.co`, donde);
    await rellenar(page, '[name="instagramFestival"]', `https://instagram.com/${apodoWeb}`, donde);
    await rellenar(page, '[name="facebookFestival"]', `https://facebook.com/${apodoWeb}`, donde);
    await rellenar(page, '[name="otroEnlaceFestival"]', `https://youtube.com/@${apodoWeb}`, donde);
    await foto(page, `festival-contacto-${clave}`);
  }

  // ── Paso 3: la primera edición ───────────────────────────────────────────────────────────
  await page.locator('[data-testid="pestana-edicion"]').click();
  await page.locator('[data-testid="ficha-crear-primera-edicion"]').click();
  await expect(page.locator('[data-testid="bloque-edicion"]')).toBeVisible();
  await rellenar(page, '[name="nombreEdicion"]', fest.edicion.nombre, donde);
  await rellenar(page, '[name="fechaInicio"]', fest.edicion.fechaInicio, donde);
  await rellenar(page, '[name="fechaFin"]', fest.edicion.fechaFin, donde);
  await rellenar(page, '[name="descripcionEdicion"]', fest.descripcion, donde);
  await foto(page, `festival-edicion-${clave}`);

  await pulsar(page, page.locator('[data-testid="ficha-guardar"]'));
  try {
    await expect(page.locator('[data-testid="ficha-mensaje"]')).toContainText(/qued/i, { timeout: 60000 });
  } catch {
    // SE ANOTA LO QUE DICE LA PANTALLA, no «falló el guardado». El aviso rojo de la ficha nombra el
    // paso que hay que revisar, y esa frase es la que resuelve el problema en un minuto.
    const rojo = page.locator('[data-testid="ficha-error"]');
    const dice = await rojo.count() > 0 ? (await rojo.first().innerText()).trim() : 'sin mensaje en pantalla';
    await foto(page, `festival-NO-guardo-${clave}`);
    HALLAZGOS.push(`${fest.nombre}: no se guardó. La ficha dice: «${dice}»`);
    await page.locator('[data-testid="ficha-cancelar"]').click().catch(() => {});
    return;
  }
  await foto(page, `festival-guardado-${clave}`);
  await page.locator('[data-testid="ficha-cerrar"]').click();

  await enviarARevision(page, fest, clave);
}

/**
 * Manda el Festival a la cola institucional.
 *
 * SI NO HAY BOTÓN NO SIEMPRE ES UN FALLO: un Festival que ya está en revisión no lo ofrece, y eso
 * es correcto. Se distingue mirando el estado que pinta su tarjeta antes de dar nada por roto.
 */
async function enviarARevision(page: Page, fest: Festival, clave: string): Promise<void> {
  await abrirFestivales(page);
  const enviar = page.locator(`button[aria-label^="Enviar a revisión: ${fest.nombre}"]`);
  try {
    await expect(enviar).toHaveCount(1, { timeout: 120000 });
  } catch {
    const yaEnviado = await page.locator('li', { hasText: fest.nombre })
      .filter({ hasText: /Estado: En revisi|Estado: Publicado/ }).count();
    if (yaEnviado === 0) {
      await foto(page, `sin-enviar-${clave}`);
      HALLAZGOS.push(`${fest.nombre}: no salió el botón de enviar a revisión y tampoco está enviado`);
    }
    return;
  }
  await pulsar(page, enviar);
  await expect(enviar).toHaveCount(0, { timeout: 120000 });
  await foto(page, `festival-enviado-${clave}`);
}

test('poblar la base con organizaciones y Festivales reales', async ({ page }) => {
  const cuantas = CUANTAS > 0 ? CUANTAS : ORGANIZACIONES.length;

  for (const org of ORGANIZACIONES.slice(0, cuantas)) {
    const como = await entrarODarDeAlta(page, org);
    console.log(`${org.nombre}: ${como === 'alta' ? 'dada de alta' : 'ya existía, entré'}`);
    // NI EL PERFIL NI UN FESTIVAL DETIENEN A LOS DEMÁS. Con la excepción suelta, un tropiezo en la
    // organización número cuatro dejaba las catorce siguientes sin intentar.
    try {
      await completarPerfil(page, org);
    } catch (error) {
      HALLAZGOS.push(`perfil de ${org.nombre}: ${(error as Error).message.split('\n')[0]}`);
    }
    for (const fest of FESTIVALES.filter(f => f.organizacion === org.correo)) {
      // UN FESTIVAL QUE FALLA NO DETIENE A LOS DIECISIETE RESTANTES. Con la excepción suelta, la
      // primera corrida murió en el segundo Festival y las dieciséis organizaciones siguientes ni
      // se intentaron: un fallo escondía todos los demás. Se anota y se sigue.
      try {
        await crearFestival(page, fest);
        console.log(`  · ${fest.nombre}`);
      } catch (error) {
        HALLAZGOS.push(`${fest.nombre}: ${(error as Error).message.split('\n')[0]}`);
        console.log(`  x ${fest.nombre}`);
      }
    }
  }

  if (HALLAZGOS.length > 0) console.log('HALLAZGOS:\n' + HALLAZGOS.map(h => '  - ' + h).join('\n'));
  expect(HALLAZGOS, HALLAZGOS.join('\n')).toEqual([]);
});
