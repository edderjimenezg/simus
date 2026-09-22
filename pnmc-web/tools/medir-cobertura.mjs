/**
 * Cobertura real del CMS, medida en el navegador.
 *
 * La pregunta —«¿qué claves del panel llegan de verdad a una página?»— no se
 * puede responder leyendo el código, y se intentó: hay claves que se arman al
 * vuelo (`nav_${id}`, `eje0${n}_...`) y componentes que resuelven la clave y
 * luego **descartan** el resultado. Un detector por texto se equivoca en las dos
 * direcciones, y equivocarse aquí manda a arreglar lo que ya funciona.
 *
 * Así que se le pregunta a la realidad: se publica un marcador único en cada
 * clave, se renderizan las rutas públicas con un navegador de verdad, y se mira
 * cuáles aparecen. Lo que no sale en ninguna, no lo ve nadie.
 *
 * Esta es la medición DEFINITIVA y por eso es cara: necesita la base, la API y
 * Chrome, y **escribe en la base de datos**. Para el control de cada cambio está
 * `npm run cms:huerfanas`, que solo comprueba que alguien nombre la clave.
 *
 *   npm run cms:cobertura
 *
 * Al terminar repone el texto que estaba publicado —leído antes de empezar—,
 * incluso si algo falla a mitad. Lo que NO puede reponer es el estado «nunca
 * publicada»: esas claves quedan publicadas con su texto de fábrica y se listan
 * al final. Aun así: no ejecutarlo contra una base con contenido editado sin
 * respaldo.
 */
import { execFile } from 'node:child_process';
import { readFileSync, writeFileSync, mkdtempSync } from 'node:fs';
import { promisify } from 'node:util';
import { join, dirname } from 'node:path';
import { tmpdir } from 'node:os';
import { fileURLToPath } from 'node:url';
import { chromium } from 'playwright-core';

const ejecutar = promisify(execFile);
const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..');
const CATALOGO = join(ROOT, '../pnmc-api/src/PNMC.Infrastructure/Data/web-content-catalog.json');

const API = process.env.PNMC_API_URL ?? 'http://localhost:8180/api/v1';
const SITIO = process.env.PNMC_BASE_URL ?? 'http://127.0.0.1:4300';
const CREDENCIALES = {
  email: process.env.PNMC_ADMIN_EMAIL ?? 'admin@pnmc.local',
  password: process.env.PNMC_ADMIN_PASSWORD ?? 'admin',
};

/**
 * ESTA ORDEN ESCRIBE, Y HAY QUE DECIRLO EN VOZ ALTA ANTES DE QUE ESCRIBA.
 *
 * La cabecera lo explicaba desde el principio y aun asi paso: el 23 ago 2026 un
 * agente la ejecuto creyendo que solo media —se llama «medir-cobertura»— y dejo
 * 14 claves de `map_tutorial` publicadas que nadie habia publicado nunca, mas
 * 664 filas de historial. Se repuso a mano con SQL.
 *
 * La leccion no es «documentarlo mejor», porque ya estaba documentado: es que
 * una advertencia en un comentario no detiene nada. La confirmacion si.
 *
 * No se pide interactivamente a proposito: esto tiene que poder correr en un
 * guion. Se pide como bandera, que es igual de explicita y no cuelga.
 */
if (!process.argv.includes('--acepto-que-escribe-en-la-base')) {
  console.error('cms:cobertura ESCRIBE EN LA BASE DE DATOS. No es una medicion pasiva.\n');
  console.error('  · Publica un marcador en las 332 claves y repone el texto al terminar.');
  console.error('  · Lo que NO repone es el estado «nunca publicada»: esas claves quedan');
  console.error('    publicadas con su texto de fabrica. Volver atras exige SQL directo.');
  console.error('  · Deja dos entradas de historial por clave.\n');
  console.error('Si de verdad quiere medir, y contra una base de la que haya respaldo:');
  console.error('  npm run cms:cobertura -- --acepto-que-escribe-en-la-base\n');
  console.error('Para el control de cada cambio use `npm run cms:huerfanas`, que solo lee.');
  process.exit(2);
}


/**
 * Toda ruta pública, incluidas las que solo se alcanzan con un parámetro. Las
 * fichas de componente estaban fuera al principio y por eso `eje0N_cM_desc`
 * parecía muerta sin estarlo: una ruta que falta aquí produce un falso positivo.
 */
const RUTAS = [
  '/', '/pnmc', '/ejes', '/noticias', '/agenda', '/editorial', '/galeria', '/mapa',
  '/registro', '/estrategia/circulacion', '/estrategia/investigacion',
  '/ecosistema', '/ecosistema/escuelas', '/ecosistema/festivales', '/no-existe-404',
  '/ejes/componentes/c1-1', '/ejes/componentes/c2-1', '/ejes/componentes/c3-1',
];

/** Irrepetible y corto, para que quepa aunque el campo tenga un tope pequeño. */
const marcador = (clave) => `Z${clave.replace(/_/g, '')}Z`;

const temporal = mkdtempSync(join(tmpdir(), 'pnmc-cobertura-'));
const galletas = join(temporal, 'ck.txt');
const cuerpoJson = join(temporal, 'cuerpo.json');

async function api(ruta, opciones = {}) {
  const args = ['-s', '-i', '-b', galletas, '-c', galletas, `${API}${ruta}`];
  if (opciones.body) {
    // Se manda por archivo y no en línea: en Git Bash las tildes se estropean
    // y la API responde 400 por un fallo que no está en el código.
    writeFileSync(cuerpoJson, opciones.body, 'utf8');
    args.push('-X', 'POST', '-H', 'Content-Type: application/json', '--data-binary', `@${cuerpoJson}`);
  }
  if (opciones.token) args.push('-H', `X-CSRF-TOKEN: ${opciones.token}`);
  const { stdout } = await ejecutar('curl', args, { maxBuffer: 64e6 });
  const corte = stdout.indexOf('\r\n\r\n') >= 0 ? stdout.indexOf('\r\n\r\n') + 4 : stdout.indexOf('\n\n') + 2;
  return { estado: Number(stdout.slice(9, 12)), cuerpo: stdout.slice(corte) };
}

async function token() {
  const r = await api('/admin/contenido-web/csrf');
  const j = JSON.parse(r.cuerpo);
  return j.token ?? j.requestToken ?? Object.values(j)[0];
}

const catalogo = JSON.parse(readFileSync(CATALOGO, 'utf8'));
const entradas = Array.isArray(catalogo) ? catalogo : (catalogo.texts ?? catalogo.entries ?? []);
const grupos = new Map();
for (const entrada of entradas) {
  const grupo = entrada.groupId ?? entrada.GroupId;
  if (!grupos.has(grupo)) grupos.set(grupo, []);
  grupos.get(grupo).push({ key: entrada.key ?? entrada.Key, defecto: entrada.defaultValue ?? entrada.DefaultValue ?? '' });
}

async function escribirGrupos(valorDe) {
  const t = await token();
  for (const [grupo, campos] of grupos) {
    const cuerpo = JSON.stringify({ fields: campos.map((c) => ({ key: c.key, content: valorDe(c) })), publish: true });
    const r = await api(`/admin/contenido-web/groups/${grupo}`, { body: cuerpo, token: t });
    if (r.estado !== 200) throw new Error(`grupo ${grupo}: HTTP ${r.estado} — ${r.cuerpo.slice(0, 200)}`);
  }
}

const login = await api('/admin/auth/login', { body: JSON.stringify(CREDENCIALES) });
if (login.estado !== 200) {
  console.error(`No se pudo iniciar sesión en la API (HTTP ${login.estado}). ¿Está levantada en ${API}?`);
  process.exit(2);
}

// Fotografía del texto publicado ANTES de tocar nada. Es lo único que permite
// devolver la base a como estaba; sin esta lectura, la reposición del final no
// tiene contra qué comparar y acaba publicando el catálogo entero.
const lecturaPrevia = await api('/contenido-web');
if (lecturaPrevia.estado !== 200) {
  console.error(`No se pudo leer el estado publicado (HTTP ${lecturaPrevia.estado}). Sin esa foto no es seguro escribir.`);
  process.exit(2);
}
const publicadoAlEmpezar = JSON.parse(lecturaPrevia.cuerpo).texts ?? {};

let fallo = null;
let navegador = null;
const vistas = new Map();
try {
  console.log(`Publicando un marcador en cada una de las ${entradas.length} claves...`);
  await escribirGrupos((campo) => marcador(campo.key));

  // Chromium por Playwright y no Chrome por línea de órdenes: `--dump-dom` con
  // `--virtual-time-budget` se cuelga indefinidamente desde Chrome 151, y sin
  // ese presupuesto vuelca el armazón sin hidratar —cero marcadores en todas
  // las rutas, es decir, «todas las claves son huérfanas»—. Un falso positivo
  // masivo que mandaría a arreglar 313 claves que funcionan.
  navegador = await chromium.launch();
  const pagina = await navegador.newPage({ viewport: { width: 1280, height: 900 } });

  for (const ruta of RUTAS) {
    await pagina.goto(`${SITIO}${ruta}`, { waitUntil: 'networkidle', timeout: 45_000 });
    const html = await pagina.content();
    let encontradas = 0;
    for (const entrada of entradas) {
      const clave = entrada.key ?? entrada.Key;
      if (!html.includes(marcador(clave))) continue;
      if (!vistas.has(clave)) vistas.set(clave, []);
      vistas.get(clave).push(ruta);
      encontradas++;
    }
    console.log(`  ${ruta.padEnd(28)} ${String(encontradas).padStart(3)} claves visibles`);
  }
} catch (error) {
  fallo = error;
} finally {
  // Pase lo que pase hay que quitar los marcadores: un fallo a mitad dejaría el
  // sitio publicado con cadenas como «Zhomehero1titleZ».
  //
  // Lo que se repone es el texto QUE HABÍA PUBLICADO, leído antes de empezar, y
  // no el del catálogo. Reponer el catálogo era lo que hacía antes y es un
  // destrozo silencioso: publica los 313 valores de fábrica, pisando lo que
  // hubiera escrito una editora y —peor— dejando publicada toda clave que nadie
  // había publicado nunca. En esta base lo hizo: las 313 quedaron publicadas, y
  // con eso «publicado» dejó de significar «alguien lo decidió», que es
  // justamente el dato del que dependen las migraciones de claves.
  //
  // Lo que esta reposición NO puede deshacer es el estado «nunca publicada»: la
  // API del panel publica o retira, y retirar deja una marca distinta. Las
  // claves que estaban sin publicar se dejan con su texto de catálogo y se
  // listan al final, para que quien ejecute esto sepa exactamente qué quedó
  // tocado.
  // El navegador se cierra ANTES de reponer: si la reposición fallara, dejar
  // vivo un Chromium impediría que el proceso terminara y el fallo real se
  // vería como un cuelgue.
  if (navegador) await navegador.close();

  console.log('\nReponiendo el texto que estaba publicado...');
  const sinPublicarAntes = [];
  await escribirGrupos((campo) => {
    const previo = publicadoAlEmpezar[campo.key];
    if (previo === undefined) {
      sinPublicarAntes.push(campo.key);
      return campo.defecto;
    }
    return previo;
  });
  console.log(`Repuesto. ${Object.keys(publicadoAlEmpezar).length} claves volvieron a su texto publicado.`);
  if (sinPublicarAntes.length > 0) {
    console.log(
      `AVISO: ${sinPublicarAntes.length} claves no estaban publicadas y esta medición las deja publicadas ` +
      'con su texto de fábrica. Retírelas desde el panel si el estado importa:',
    );
    console.log(`  ${sinPublicarAntes.join(', ')}`);
  }
}

if (fallo) {
  console.error(`\nLa medición falló: ${fallo.message}`);
  process.exit(1);
}

const huerfanas = entradas.map((e) => e.key ?? e.Key).filter((k) => !vistas.has(k));
console.log(`\n${'='.repeat(62)}`);
console.log(`claves que SÍ llegan a alguna página : ${vistas.size} de ${entradas.length}`);
console.log(`claves que NO se vieron              : ${huerfanas.length}`);

if (!huerfanas.length) process.exit(0);

const porGrupo = new Map();
for (const entrada of entradas) {
  const clave = entrada.key ?? entrada.Key;
  if (vistas.has(clave)) continue;
  const grupo = entrada.groupLabel ?? entrada.GroupLabel ?? entrada.groupId ?? entrada.GroupId;
  if (!porGrupo.has(grupo)) porGrupo.set(grupo, []);
  porGrupo.get(grupo).push(clave);
}
console.log('\nNo verse aquí no siempre es un defecto: un texto de «cargando» o de');
console.log('«sin resultados» solo aparece en su estado, y el recorrido no lo provoca.');
console.log('Para saber si además NADIE la nombra, ejecute `npm run cms:huerfanas`.\n');
for (const [grupo, claves] of [...porGrupo].sort((a, b) => b[1].length - a[1].length)) {
  console.log(`  ${String(claves.length).padStart(3)}  ${grupo}`);
  console.log(`       ${claves.join(', ')}`);
}
