/**
 * Recorre los tres espacios y anota lo que el navegador rechaza.
 *
 * NO ES UNA PRUEBA UNITARIA. Las unitarias doblan el API y nunca ven una ruta mal escrita ni un
 * 404 real; esto abre el sitio de verdad, pulsa y registra cada petición que falla y cada error de
 * consola, con la página donde ocurrió.
 *
 * Cubre los TRES espacios —portal público, Espacio de Gestión Administrativa y Gestión de la
 * organización— porque un espacio sin recorrer es un espacio sin verificar: el 500 del Banco de
 * archivos y las dos consultas de notificaciones canceladas al entrar a la consola aparecieron
 * aquí y no en las 1 167 pruebas de frontend, que doblan el API y no navegan.
 *
 * Se ejecuta desde `pnmc-web/` (es donde está Playwright) con la pila local levantada:
 *   node tools/barrido-de-reparacion.mjs
 */
import { chromium } from 'playwright';

const BASE = 'http://localhost:4300';
const navegador = await chromium.launch();
const ctx = await navegador.newContext({ viewport: { width: 1600, height: 950 } });
const p = await ctx.newPage();

const fallos = [];
let pagina = '(arranque)';
const anota = (tipo, detalle) => fallos.push({ pagina, tipo, detalle });

const vigilar = pagina => {
  pagina.on('response', r => {
    if (r.status() < 400) return;
    const u = new URL(r.url());
    if (!u.pathname.startsWith('/api/')) return;
    // 401 en /auth/me es la forma normal de decir «no hay sesión».
    if (r.status() === 401 && /\/auth\/me$/.test(u.pathname)) return;
    anota(`HTTP ${r.status()}`, u.pathname + u.search);
  });
  pagina.on('requestfailed', r => {
    const u = r.url();
    if (u.includes('localhost:4747')) return;  // barra de desarrollo, no es del producto
    anota('petición fallida', `${r.failure()?.errorText} ${u.slice(0, 90)}`);
  });
  pagina.on('pageerror', e => anota('error de página', String(e).slice(0, 140)));
  pagina.on('console', m => { if (m.type() === 'error' && !m.text().includes('Failed to load resource')) anota('consola', m.text().slice(0, 140)); });
};
vigilar(p);

const visitar = async (ruta, etiqueta) => {
  pagina = etiqueta;
  await p.goto(BASE + ruta, { waitUntil: 'networkidle' }).catch(() => anota('navegación', ruta));
  await p.waitForTimeout(1500);
};

// ── Portal público
for (const [ruta, et] of [['/', 'Inicio'], ['/pnmc', 'Sobre el PNMC'], ['/ejes', 'Ejes'],
  ['/editorial', 'Catálogo Editorial'], ['/galeria', 'Galería'], ['/noticias', 'Noticias'],
  ['/agenda', 'Agenda'], ['/mapa-ecosistemico', 'Mapa'], ['/ecosistema/festivales', 'Festivales'],
  ['/ecosistema/territorios-sonoros', 'Territorios'], ['/ecosistema/practicas-musicales', 'Prácticas'],
  ['/registro', 'Registro'], ['/ingresar', 'Ingresar']]) await visitar(ruta, et);

// ── Consola administrativa
pagina = 'Consola · entrada';
await p.goto(BASE + '/administracion', { waitUntil: 'networkidle' });
await p.fill('#admin-email', 'admin@pnmc.local');
await p.fill('#admin-password', 'admin');
await p.click('button[type=submit]');
await p.waitForTimeout(4000);

const secciones = await p.locator('.foundation-sidebar button').allTextContents();
for (let i = 0; i < secciones.length; i++) {
  const nombre = secciones[i].replace(/\s+/g, ' ').trim();
  pagina = 'Consola · ' + nombre;
  await p.locator('.foundation-sidebar button').nth(i).click().catch(() => {});
  // El puntero se queda encima de lo último que se pulsó y activa su `:hover`. No altera lo que
  // aquí se mide, pero sí lo alteraría en cuanto alguien añada una comprobación visual.
  await p.mouse.move(2, 2);
  await p.waitForTimeout(2200);
}

// ── Gestión de la organización, en su propia sesión para no mezclar cookies con la consola.
const ctxOrg = await navegador.newContext({ viewport: { width: 1600, height: 950 } });
const q = await ctxOrg.newPage();
vigilar(q);
pagina = 'Gestión · ingreso';
await q.goto(BASE + '/ingresar', { waitUntil: 'networkidle' });
await q.fill('#acceso-correo', 'externo@pnmc.local');
await q.fill('#acceso-contrasena', 'admin');
await q.locator('form').filter({ has: q.locator('#acceso-correo') }).locator('button[type=submit]').click();
await q.waitForTimeout(4000);

for (const [ruta, et] of [['/gestion/resumen', 'Resumen'], ['/gestion/organizacion', 'Organización'],
  ['/gestion/responsable', 'Responsable'], ['/gestion/archivos', 'Mis archivos'],
  ['/gestion/procesos', 'Procesos'], ['/gestion/procesos/festivales', 'Festivales'],
  ['/gestion/eventos', 'Eventos'], ['/gestion/solicitudes', 'Solicitudes'],
  ['/gestion/cuenta-seguridad', 'Cuenta y seguridad']]) {
  pagina = 'Gestión · ' + et;
  await q.goto(BASE + ruta, { waitUntil: 'networkidle' }).catch(() => anota('navegación', ruta));
  await q.waitForTimeout(1800);
}

// ── El portal en un teléfono. Un fallo de red no depende del ancho, pero un error de página sí:
// hay componentes que solo se montan por debajo de cierto punto de corte.
const ctxMovil = await navegador.newContext({ viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true });
const m = await ctxMovil.newPage();
vigilar(m);
for (const [ruta, et] of [['/', 'Inicio'], ['/agenda', 'Agenda'], ['/noticias', 'Noticias'],
  ['/editorial', 'Catálogo Editorial'], ['/ecosistema/festivales', 'Festivales']]) {
  pagina = 'Teléfono · ' + et;
  await m.goto(BASE + ruta, { waitUntil: 'networkidle' }).catch(() => anota('navegación', ruta));
  await m.waitForTimeout(1500);
  const anchoDelCuerpo = await m.evaluate(() => document.documentElement.scrollWidth);
  if (anchoDelCuerpo > 390) anota('desborde horizontal', `${anchoDelCuerpo}px de ancho en una pantalla de 390`);
}

console.log(JSON.stringify({ total: fallos.length, fallos }, null, 1));
await navegador.close();
