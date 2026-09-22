#!/usr/bin/env node
/**
 * Revisión previa a publicar: ¿qué cambiaría en el sitio si publico este grupo?
 *
 * El panel no puede responder esa pregunta. Muestra el borrador, y el borrador
 * casi siempre coincide con el texto compilado —porque de ahí se sembró—, así
 * que todo parece inofensivo. Pero si la base se sembró desde un catálogo viejo,
 * o si alguien dejó un valor a medias, publicar el grupo cambia el sitio sin que
 * nadie lo haya escrito, y el editor se entera cuando ya está publicado.
 *
 * Pasó dos veces el 21 ago 2026:
 *   - `nav_mapa` sembrado como «Mapa Ecosistémico» mientras el sitio compila
 *     «Ecosistema». Publicar Navegación y Footer habría cambiado el menú de las
 *     8 rutas.
 *   - El encabezado del Home se publicó con el título llevando dentro el texto
 *     del acento, y el sitio mostró «Diversidad Sonora» dos veces.
 *
 * La comparación es contra el REGISTRO TypeScript, no contra el catálogo JSON:
 * el registro es lo que el sitio compila y por tanto lo que el visitante ve hoy.
 * El catálogo es un intermediario que puede quedarse atrás —y se quedó—.
 *
 *   node tools/preflight-publicacion.mjs                  # todos los grupos
 *   node tools/preflight-publicacion.mjs general_nav_footer
 *
 * Sale con 1 si algún grupo revisado cambiaría el sitio.
 */
import { readFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import { join, dirname } from 'node:path';

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..');
const API = process.env.PNMC_API ?? 'http://localhost:8180/api/v1';
const EMAIL = process.env.PNMC_ADMIN_EMAIL ?? 'admin@pnmc.local';
const PASSWORD = process.env.PNMC_ADMIN_PASSWORD ?? 'admin';

/**
 * Lee los valores compilados del registro sin importar TypeScript: se ejecuta
 * con node a secas, igual que el resto de herramientas de `tools/`.
 */
async function leerRegistro() {
  const fuente = await readFile(join(ROOT, 'src/app/core/cms/registro-de-textos-web.ts'), 'utf8');
  const compilados = new Map();
  const grupos = new Map();
  let grupoActual = null;

  for (const linea of fuente.split('\n')) {
    const cabecera = linea.match(/id:\s*'([^']+)',\s*label:\s*'((?:[^'\\]|\\.)*)'/);
    if (cabecera) {
      grupoActual = cabecera[1];
      grupos.set(grupoActual, cabecera[2].replace(/\\'/g, "'"));
      continue;
    }
    // field('clave', 'etiqueta', limite, 'valor por defecto')
    const campo = linea.match(/field\('([^']+)',\s*'(?:[^'\\]|\\.)*',\s*\d+,\s*'((?:[^'\\]|\\.)*)'\)/);
    if (campo && grupoActual) {
      compilados.set(campo[1], {
        grupo: grupoActual,
        valor: campo[2].replace(/\\n/g, '\n').replace(/\\'/g, "'").replace(/\\\\/g, '\\'),
      });
    }
  }
  return { compilados, grupos };
}

async function pedir(ruta, opciones = {}, cookie = '') {
  const res = await fetch(`${API}${ruta}`, {
    ...opciones,
    headers: { 'Content-Type': 'application/json', ...(cookie ? { cookie } : {}), ...opciones.headers },
  });
  return res;
}

function recogerCookie(res, previa) {
  const cruda = res.headers.getSetCookie?.() ?? [];
  const nuevas = cruda.map((c) => c.split(';')[0]);
  const mapa = new Map(previa.split('; ').filter(Boolean).map((c) => [c.split('=')[0], c]));
  for (const c of nuevas) mapa.set(c.split('=')[0], c);
  return [...mapa.values()].join('; ');
}

const solicitados = process.argv.slice(2).filter((a) => !a.startsWith('--'));

const { compilados, grupos } = await leerRegistro();
if (compilados.size === 0) {
  console.error('No se pudo leer el registro. ¿Cambió el formato de registro-de-textos-web.ts?');
  process.exit(2);
}

const ingreso = await pedir('/admin/auth/login', {
  method: 'POST',
  body: JSON.stringify({ email: EMAIL, password: PASSWORD }),
});
if (!ingreso.ok) {
  console.error(`No fue posible iniciar sesión en ${API} (HTTP ${ingreso.status}).`);
  console.error('Levante la API y compruebe las credenciales (PNMC_ADMIN_EMAIL / PNMC_ADMIN_PASSWORD).');
  process.exit(2);
}
const cookie = recogerCookie(ingreso, '');

const aRevisar = solicitados.length > 0 ? solicitados : [...grupos.keys()];
console.log(`Revisión previa · ${API}`);
console.log(`Comparando el borrador de la base contra el texto que el sitio compila hoy.\n`);

let gruposConCambio = 0;
let camposConCambio = 0;

for (const grupoId of aRevisar) {
  const res = await pedir(`/admin/contenido-web/groups/${grupoId}`, {}, cookie);
  if (!res.ok) {
    console.log(`  ${grupoId.padEnd(26)} — no se pudo leer (HTTP ${res.status})`);
    continue;
  }
  const { fields = [] } = await res.json();
  const divergentes = fields
    .map((f) => ({ key: f.key, base: f.draft ?? '', sitio: compilados.get(f.key)?.valor }))
    .filter((f) => f.sitio !== undefined && f.base !== f.sitio);

  const etiqueta = grupos.get(grupoId) ?? grupoId;
  if (divergentes.length === 0) {
    console.log(`  ✓ ${etiqueta}`);
    continue;
  }

  gruposConCambio++;
  camposConCambio += divergentes.length;
  console.log(`  ✗ ${etiqueta} — publicar cambiaría ${divergentes.length} texto(s) del sitio:`);
  for (const d of divergentes) {
    console.log(`      ${d.key}`);
    console.log(`        hoy el visitante ve : ${JSON.stringify(d.sitio)}`);
    console.log(`        pasaría a ver       : ${JSON.stringify(d.base)}`);
  }
}

console.log();
if (gruposConCambio === 0) {
  console.log(`✓ Ningún grupo revisado cambiaría el sitio (${aRevisar.length} grupo(s)).`);
  process.exit(0);
}
console.log(`✗ ${gruposConCambio} grupo(s) cambiarían el sitio, ${camposConCambio} texto(s) en total.`);
console.log('  Revíselos antes de publicar. Si el cambio no es deliberado, corrija el borrador');
console.log('  en el panel, o regenere el catálogo y vuelva a sembrar.');
process.exit(1);
