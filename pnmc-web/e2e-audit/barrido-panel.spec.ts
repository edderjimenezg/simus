import { test, expect, type Page } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';
import * as os from 'os';
import { dentroDeLaVentana } from './dentro-de-la-ventana';
import { AUDITORIA_DOM } from './auditoria-dom';
import { RUTA_SESION } from './sesion-compartida';

/**
 * Barrido del PANEL ADMINISTRATIVO por dispositivo.
 *
 * REGLA INNEGOCIABLE DE ESTA SUITE: solo se navega y se mide. No se pulsa
 * NINGUNA accion que escriba. El panel comparte base de datos con la sesion de
 * desarrollo que trabaja en paralelo, y catorce de sus dieciseis acciones
 * destructivas no piden confirmacion. Un clic equivocado aqui le fusiona
 * registros a otra persona.
 *
 * Lo unico que escribe este barrido es la cookie de sesion del inicio de
 * sesion. Se deja constancia a proposito.
 */

const CREDENCIALES = {
  email: process.env.PNMC_ADMIN_EMAIL ?? 'admin@pnmc.local',
  password: process.env.PNMC_ADMIN_PASSWORD ?? 'admin',
};

const SALIDA =
  process.env.PNMC_AUDIT_OUT || path.join(os.tmpdir(), 'pnmc-auditoria-panel');

// Los DIECISEIS apartados hoja de `GRUPOS_DE_GESTION_ADMINISTRATIVA`, con su título exacto.
//
// ANTES ERAN NUEVE Y DOS YA NO EXISTIAN. La navegación pasó a estar agrupada —«Ecosistema musical»
// y «Sitio web y comunicación» son ahora cabeceras de grupo, no apartados—, y el barrido siguió
// buscándolas como si fueran botones: las informaba como «inalcanzables» en las seis resoluciones.
// Un barrido que nombra a mano la arquitectura tiene que actualizarse con ella, y mientras no se
// haga miente en las dos direcciones: inventa defectos donde no los hay y deja sin mirar los
// apartados nuevos.
const PANELES = [
  'Resumen operativo',
  'Solicitudes y revisiones',
  'Festivales y ediciones',
  'Mercados musicales',
  'Organizaciones y responsables',
  'Catálogo Editorial',
  'Agenda y eventos',
  'Noticias y prensa',
  'Categorías y proyectos',
  'Banco de archivos',
  'Páginas y bloques',
  'Boletín informativo',
  'Análisis y consultas',
  'Auditoría y trazabilidad',
  'Usuarios y roles',
  'Salud del sistema',
];

/**
 * La cookie viene del proyecto `preparacion`, que inicia sesion UNA vez para
 * toda la corrida. Antes cada prueba entraba por su cuenta y el conjunto
 * agotaba el limitador de tasa del API, lo que hacia que los ultimos
 * dispositivos informaran de «paneles inalcanzables» por un fallo de acceso que
 * provocaba el propio arnes.
 */
async function entrar(page: Page) {
  await page.addInitScript((email) => {
    window.localStorage.setItem(`pnmc_tour_seen_${email}`, '1');
  }, CREDENCIALES.email);
  await page.goto('/administracion', { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(3500);

  // Si la sesion compartida no sirvio, hay que DECIRLO: sin esto el barrido
  // informaria de diez paneles inalcanzables sin mencionar que ni siquiera se
  // llego a entrar.
  const hayFormulario = await page.locator('input[type="password"]').count();
  if (hayFormulario) {
    throw new Error(
      'No hay sesion activa al abrir /administracion. La cookie compartida no se aplico ' +
      '(revisa el proyecto `preparacion`) o el API rechazo el acceso. ' +
      'Esto NO significa que los paneles sean inalcanzables.',
    );
  }
}

/** Medidas propias del panel que el barrido publico no necesita. */
const MEDIDAS_PANEL = () => {
  const aside = document.querySelector('aside');
  const rAside = aside?.getBoundingClientRect();
  const dialogos = Array.from(
    document.querySelectorAll('[class*="fixed"][class*="inset-0"]'),
  ).filter((d) => {
    const r = d.getBoundingClientRect();
    return r.width > window.innerWidth * 0.5 && r.height > window.innerHeight * 0.5;
  });
  const textoMinusculo = Array.from(document.querySelectorAll('*')).filter((e) => {
    const propio = Array.from(e.childNodes)
      .filter((n) => n.nodeType === 3)
      .map((n) => (n.textContent || '').trim())
      .join('')
      .trim();
    if (propio.length < 2) return false;
    const r = e.getBoundingClientRect();
    if (r.width === 0 || r.height === 0) return false;
    return parseFloat(getComputedStyle(e).fontSize) < 10;
  });
  return {
    // Un elemento desplazado fuera de pantalla CONSERVA su anchura, asi que
    // medir `width` no dice nada sobre cuanto invade. Lo que cuenta es hasta
    // donde llega su borde derecho dentro de la ventana.
    sidebar: rAside
      ? {
          ancho: Math.round(rAside.width),
          bordeDerecho: Math.round(rAside.right),
          invade: Math.round(Math.max(0, Math.min(rAside.right, window.innerWidth))),
          fueraDePantalla: rAside.right <= 1,
        }
      : null,
    anchoUtilRestante: rAside
      ? Math.round(window.innerWidth - Math.max(0, Math.min(rAside.right, window.innerWidth)))
      : window.innerWidth,
    dialogos: dialogos.length,
    dialogosConRol: dialogos.filter((d) => d.querySelector('[role="dialog"]') || d.getAttribute('role') === 'dialog').length,
    dialogosConAriaModal: dialogos.filter((d) => d.querySelector('[aria-modal]') || d.hasAttribute('aria-modal')).length,
    textoBajo10px: textoMinusculo.length,
    textoMasPequeno: textoMinusculo.length
      ? Math.min(...textoMinusculo.map((e) => parseFloat(getComputedStyle(e).fontSize)))
      : null,
    tablas: document.querySelectorAll('table').length,
    tablasConScrollHorizontal: Array.from(document.querySelectorAll('table')).filter((t) => {
      let p: Element | null = t.parentElement;
      let n = 0;
      while (p && n < 4) {
        const o = getComputedStyle(p).overflowX;
        if (o === 'auto' || o === 'scroll') return true;
        p = p.parentElement;
        n++;
      }
      return false;
    }).length,
    columnasOcultas: document.querySelectorAll('.hidden.md\\:table-cell, [class*="hidden"][class*="table-cell"]').length,
  };
};

test.describe.configure({ mode: 'serial' });
test.use({ storageState: RUTA_SESION });

test('recorrido de los paneles', async ({ page }, testInfo) => {
  test.setTimeout(8 * 60_000);
  const dispositivo = testInfo.project.name;
  fs.mkdirSync(SALIDA, { recursive: true });
  fs.mkdirSync(path.join(SALIDA, 'capturas'), { recursive: true });

  const erroresDeConsola: string[] = [];
  page.on('pageerror', (e) => erroresDeConsola.push(String(e).slice(0, 200)));

  await entrar(page);

  const resultados: Record<string, unknown>[] = [];

  const anchoVentana = page.viewportSize()?.width ?? 1920;
  // EL CORTE ESTA EN 800 px, NO EN 1024. Lo dice `admin-shell-layout.component.css`: por debajo de
  // 800 la columna desaparece y su lugar lo ocupa el disparador «Secciones». Con 1024 el barrido
  // buscaba un cajón que a 1024 no existe y lo daba por roto.
  const esCajon = anchoVentana <= 800;

  for (const etiqueta of PANELES) {
    // Por debajo de `lg` la navegacion vive en un cajon cerrado por defecto.
    // Abrirlo es parte del recorrido de una persona, asi que forma parte de la
    // medicion: si el boton de abrir no existe, el panel es INALCANZABLE de
    // verdad y hay que decirlo.
    // Se decide por la CONDICION, no por el ancho: si el boton del panel ya se
    // ve, el cajon esta abierto (o no existe, porque es escritorio). Esperar un
    // numero fijo de milisegundos sobre una animacion era intermitente.
    // «Solicitudes» puede llevar un contador dentro del botón. Su nombre accesible pasa a ser,
    // por ejemplo, «Solicitudes 2», aunque la etiqueta de navegación siga siendo Solicitudes.
    const boton = page.locator('aside button').filter({ hasText: etiqueta }).first();
    // Solo por debajo de `lg` existe cajon. Por encima la hamburguesa esta en
    // el DOM pero oculta (`lg:hidden`) y pulsarla agota el tiempo.
    if (esCajon && !(await dentroDeLaVentana(page, boton))) {
      // EL DISPARADOR DE LA CONSOLA NO ES UNA HAMBURGUESA CON ESE NOMBRE. La consola abre su
      // navegación con un botón rotulado «Secciones», que además dice en qué apartado se está.
      // Mientras el barrido buscó «Abrir menú» —que es del sitio público— informó de los dieciséis
      // apartados como inalcanzables en las tres resoluciones pequeñas: dieciséis defectos
      // inventados, tres veces, y ningún apartado realmente mirado.
      const hamburguesa = page.locator('button.sidebar-disparador').first();
      try {
        await hamburguesa.waitFor({ state: 'visible', timeout: 10_000 });
        await hamburguesa.click({ timeout: 8000 });
        await boton.waitFor({ state: 'visible', timeout: 12_000 });
      } catch {
        /* se informa abajo como inalcanzable, con su motivo */
      }
    }
    const existe = await boton.count();
    if (!existe) {
      resultados.push({ panel: etiqueta, alcanzable: false, motivo: 'el boton del sidebar no existe o no es visible' });
      continue;
    }
    let visible = false;
    try {
      visible = await boton.isVisible({ timeout: 2000 });
    } catch {
      visible = false;
    }
    if (!visible) {
      resultados.push({ panel: etiqueta, alcanzable: false, motivo: 'el boton existe en el DOM pero no es visible en este viewport' });
      continue;
    }

    try {
      await boton.click({ timeout: 8000 });
    } catch (e) {
      resultados.push({ panel: etiqueta, alcanzable: false, motivo: 'clic bloqueado: ' + String(e).slice(0, 120) });
      continue;
    }
    await page.waitForTimeout(2200);

    const dom = (await page.evaluate(AUDITORIA_DOM)) as Record<string, unknown>;
    const panel = (await page.evaluate(MEDIDAS_PANEL)) as Record<string, unknown>;
    const clave = etiqueta.normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/[^a-zA-Z0-9]+/g, '-').toLowerCase();

    await page.screenshot({
      path: path.join(SALIDA, 'capturas', dispositivo + '__' + clave + '.png'),
      fullPage: false,
    });

    resultados.push({ panel: etiqueta, alcanzable: true, dom, panelEspecifico: panel });

    const rec = dom['recorte'] as { hay: boolean; elementos: unknown[] };
    const con = dom['contraste'] as { fallos: number };
    const tac = dom['tactiles'] as { total: number; pequenos: number };
    console.log(
      '  ' + dispositivo.padEnd(18) + etiqueta.padEnd(42) +
      ' invade ' + String((panel['sidebar'] as { invade: number } | null)?.invade ?? '-').padStart(4) + 'px' +
      ' | util ' + String(panel['anchoUtilRestante']).padStart(4) + 'px' +
      ' | contraste ' + String(con.fallos).padStart(2) +
      ' | tactiles ' + String(tac.pequenos).padStart(3) + '/' + String(tac.total).padEnd(3) +
      ' | <10px ' + String(panel['textoBajo10px']).padStart(3) +
      (rec.hay ? ' | RECORTA ' + rec.elementos.length : ''),
    );
  }

  fs.writeFileSync(
    path.join(SALIDA, dispositivo + '__panel.json'),
    JSON.stringify({ dispositivo, viewport: page.viewportSize(), erroresDeConsola, resultados }, null, 2),
    'utf8',
  );

  const inalcanzables = resultados.filter((r) => !r.alcanzable);
  if (inalcanzables.length) {
    console.log('  ' + dispositivo + ' -> ' + inalcanzables.length + ' paneles INALCANZABLES: ' + inalcanzables.map((r) => r.panel).join(', '));
  }
  expect(resultados.length).toBeGreaterThan(0);
  expect(inalcanzables).toEqual([]);
});
