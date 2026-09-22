/**
 * Devuelve a la copia de fábrica los grupos que se le nombren.
 *
 * La operación es la de un editor que dice «deshaz todo lo que se escribió en
 * esta sección y déjala como venía»: toma el texto compilado del catálogo
 * —el mismo que sirve el sitio cuando no hay nada publicado— y lo publica.
 *
 *   npm run cms:fabrica -- ecosistema_hero ecosistema_about
 *   npm run cms:fabrica -- --seccion Ecosistema
 *
 * POR QUÉ EXISTE. Al pasar la sección «SIMUS» a «Ecosistema» hubo que corregir
 * 59 claves de texto que presentaban una plataforma ajena como propia. El
 * traslado de claves del arranque (RenombradoContenidoWeb) conserva a propósito
 * lo que haya escrito una persona, y hace bien: en un despliegue de verdad,
 * borrar el trabajo de una editora desde un arranque de servicio sería
 * inaceptable. Pero eso deja el texto viejo bajo la clave nueva, y alguien tiene
 * que poder decir explícitamente «esta sección vuelve a fábrica». Esto es ese
 * alguien: una orden manual, nombrando los grupos, con la lista de lo que
 * cambió impresa al terminar.
 *
 * ES DESTRUCTIVO Y NO PIDE CONFIRMACIÓN, así que exige que se nombre al menos un
 * grupo. No hay forma de decirle «todos»: un reseteo del sitio entero debe
 * costar escribir los doce nombres.
 */
import { execFile } from 'node:child_process';
import { readFileSync, writeFileSync, mkdtempSync } from 'node:fs';
import { promisify } from 'node:util';
import { join, dirname } from 'node:path';
import { tmpdir } from 'node:os';
import { fileURLToPath } from 'node:url';

const ejecutar = promisify(execFile);
const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..');
const CATALOGO = join(ROOT, '../pnmc-api/src/PNMC.Infrastructure/Data/web-content-catalog.json');

const API = process.env.PNMC_API_URL ?? 'http://localhost:8180/api/v1';
const CREDENCIALES = {
  email: process.env.PNMC_ADMIN_EMAIL ?? 'admin@pnmc.local',
  password: process.env.PNMC_ADMIN_PASSWORD ?? 'admin',
};

const argumentos = process.argv.slice(2);
const porSeccion = argumentos.includes('--seccion');
const nombres = argumentos.filter((a) => a !== '--seccion');

if (nombres.length === 0) {
  console.error('Uso: node tools/refrescar-copia-de-fabrica.mjs <grupo...>');
  console.error('     node tools/refrescar-copia-de-fabrica.mjs --seccion <Sección...>');
  console.error('\nPublica el texto compilado del catálogo en los grupos indicados.');
  console.error('Hay que nombrarlos: no existe la opción de hacerlo con el sitio entero.');
  process.exit(2);
}

const temporal = mkdtempSync(join(tmpdir(), 'pnmc-fabrica-'));
const galletas = join(temporal, 'ck.txt');
const cuerpoJson = join(temporal, 'cuerpo.json');

async function api(ruta, opciones = {}) {
  const args = ['-s', '-i', '-b', galletas, '-c', galletas, `${API}${ruta}`];
  if (opciones.body) {
    // Por archivo y no en línea: en Git Bash las tildes se estropean y la API
    // responde 400 por un fallo que no está en el código.
    writeFileSync(cuerpoJson, opciones.body, 'utf8');
    args.push('-X', 'POST', '-H', 'Content-Type: application/json', '--data-binary', `@${cuerpoJson}`);
  }
  if (opciones.token) args.push('-H', `X-CSRF-TOKEN: ${opciones.token}`);
  const { stdout } = await ejecutar('curl', args, { maxBuffer: 64e6 });
  const corte = stdout.indexOf('\r\n\r\n') >= 0 ? stdout.indexOf('\r\n\r\n') + 4 : stdout.indexOf('\n\n') + 2;
  return { estado: Number(stdout.slice(9, 12)), cuerpo: stdout.slice(corte) };
}

const catalogo = JSON.parse(readFileSync(CATALOGO, 'utf8'));
const entradas = Array.isArray(catalogo) ? catalogo : (catalogo.texts ?? catalogo.entries ?? []);

const grupos = new Map();
for (const entrada of entradas) {
  const grupo = entrada.groupId ?? entrada.GroupId;
  const seccion = entrada.section ?? entrada.Section;
  if (!grupos.has(grupo)) grupos.set(grupo, { seccion, campos: [] });
  grupos.get(grupo).campos.push({
    key: entrada.key ?? entrada.Key,
    defecto: entrada.defaultValue ?? entrada.DefaultValue ?? '',
  });
}

const elegidos = porSeccion
  ? [...grupos.entries()].filter(([, g]) => nombres.includes(g.seccion))
  : [...grupos.entries()].filter(([id]) => nombres.includes(id));

if (elegidos.length === 0) {
  console.error(`Ninguno de ${JSON.stringify(nombres)} corresponde a ${porSeccion ? 'una sección' : 'un grupo'} del catálogo.`);
  console.error(porSeccion
    ? `Secciones: ${[...new Set([...grupos.values()].map((g) => g.seccion))].join(', ')}`
    : `Grupos: ${[...grupos.keys()].join(', ')}`);
  process.exit(2);
}

// El estado previo se lee ANTES de escribir, para poder decir al final qué
// cambió de verdad. Sin esta lectura el informe diría «12 grupos repuestos» sin
// distinguir los que ya estaban en fábrica, y una orden destructiva debe dejar
// claro qué destruyó.
const antes = await api('/contenido-web');
const publicadoAntes = antes.estado === 200 ? (JSON.parse(antes.cuerpo).texts ?? {}) : {};

const login = await api('/admin/auth/login', { body: JSON.stringify(CREDENCIALES) });
if (login.estado !== 200) {
  console.error(`No se pudo iniciar sesión en la API (HTTP ${login.estado}). ¿Está levantada en ${API}?`);
  process.exit(2);
}
const csrf = await (async () => {
  const r = await api('/admin/contenido-web/csrf');
  const j = JSON.parse(r.cuerpo);
  return j.token ?? j.requestToken ?? Object.values(j)[0];
})();

const cambiadas = [];
for (const [id, grupo] of elegidos) {
  const cuerpo = JSON.stringify({
    fields: grupo.campos.map((c) => ({ key: c.key, content: c.defecto })),
    publish: true,
  });
  const r = await api(`/admin/contenido-web/groups/${id}`, { body: cuerpo, token: csrf });
  if (r.estado !== 200) {
    console.error(`grupo ${id}: HTTP ${r.estado} — ${r.cuerpo.slice(0, 200)}`);
    process.exit(1);
  }
  for (const campo of grupo.campos) {
    if (publicadoAntes[campo.key] !== campo.defecto) {
      cambiadas.push({ clave: campo.key, antes: publicadoAntes[campo.key], ahora: campo.defecto });
    }
  }
  console.log(`  ${id.padEnd(30)} ${String(grupo.campos.length).padStart(3)} campos`);
}

console.log(`\n${elegidos.length} grupos repuestos · ${cambiadas.length} textos cambiaron de verdad`);
for (const c of cambiadas) {
  console.log(`\n  ${c.clave}`);
  console.log(`    antes: ${c.antes === undefined ? '(sin publicar)' : JSON.stringify(String(c.antes).slice(0, 120))}`);
  console.log(`    ahora: ${JSON.stringify(c.ahora.slice(0, 120))}`);
}
