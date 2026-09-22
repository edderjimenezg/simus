/**
 * Ciclo completo de un grupo del CMS contra la base real.
 *
 * Comprueba lo que ninguna prueba unitaria puede comprobar: que escribir en el
 * panel llega de verdad a SQL Server, que **guardar no es publicar**, y que el
 * sitio público solo cambia cuando alguien publica.
 *
 *   npm run cms:ciclo -- home_hero
 *   npm run cms:ciclo -- home_hero home_title_accent
 *
 * Sin segundo argumento usa el primer campo del grupo.
 *
 * ES SEGURO PARA UN GRUPO YA PUBLICADO. Captura el estado previo ANTES de tocar
 * nada y lo repone al terminar, verificando que quedó idéntico. Si algo falla a
 * mitad, imprime lo capturado para reponerlo a mano: por eso el estado previo se
 * imprime SIEMPRE y al principio, no solo cuando hay error.
 *
 * NO ES SEGURO PARA UN GRUPO QUE NADIE HA PUBLICADO NUNCA, y por eso se planta
 * antes de tocarlo. El porqué está escrito junto a la comprobación, más abajo.
 *
 * Lo que NO puede deshacer: las entradas del historial. Quedan tres —guardado,
 * publicado y el republicado de la reposición— y es correcto que queden: son un
 * registro de auditoría y borrarlas sería justamente lo que un registro de
 * auditoría no debe permitir.
 */
import { execFile } from 'node:child_process';
import { promisify } from 'node:util';
import { writeFileSync, mkdtempSync } from 'node:fs';
import { join } from 'node:path';
import { tmpdir } from 'node:os';

const ejecutar = promisify(execFile);
const API = process.env.PNMC_API_URL ?? 'http://localhost:8180/api/v1';
const CREDENCIALES = {
  email: process.env.PNMC_ADMIN_EMAIL ?? 'admin@pnmc.local',
  password: process.env.PNMC_ADMIN_PASSWORD ?? 'admin',
};

const ARGUMENTOS = process.argv.slice(2);
const PRIMERA_VEZ = ARGUMENTOS.includes('--publicar-por-primera-vez');
const [GRUPO, CLAVE_PEDIDA] = ARGUMENTOS.filter((a) => !a.startsWith('--'));
if (!GRUPO) {
  console.error('Uso: node tools/prueba-ciclo-cms.mjs <grupoId> [clave]');
  console.error('Ej.: node tools/prueba-ciclo-cms.mjs home_hero');
  process.exit(2);
}

// El marcador lleva la hora para que dos corridas seguidas no se confundan entre
// sí: sin eso, la segunda pasaría en verde aunque no hubiera escrito nada.
const MARCADOR = `PRUEBA-CICLO-${new Date().toISOString().slice(11, 19)}`;

const temporal = mkdtempSync(join(tmpdir(), 'pnmc-ciclo-'));
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

const publico = async () => JSON.parse((await api('/contenido-web')).cuerpo).texts;

let paso = 0;
let fallos = 0;
const comprobar = (condicion, textoOk, textoMal) => {
  paso++;
  if (condicion) console.log(`  ${String(paso).padStart(2)}. OK    ${textoOk}`);
  else { console.error(`  ${String(paso).padStart(2)}. FALLA ${textoMal}`); fallos++; }
};

const login = await api('/admin/auth/login', { body: JSON.stringify(CREDENCIALES) });
if (login.estado !== 200) {
  console.error(`No se pudo iniciar sesión (HTTP ${login.estado}). ¿Está la API en ${API}?`);
  process.exit(2);
}
const csrf = await (async () => {
  const r = await api('/admin/contenido-web/csrf');
  const j = JSON.parse(r.cuerpo);
  return j.token ?? j.requestToken ?? Object.values(j)[0];
})();

const respuestaGrupo = await api(`/admin/contenido-web/groups/${GRUPO}`);
if (respuestaGrupo.estado !== 200) {
  console.error(`El grupo '${GRUPO}' no existe (HTTP ${respuestaGrupo.estado}).`);
  process.exit(2);
}
const previo = JSON.parse(respuestaGrupo.cuerpo).fields
  .map((f) => ({ key: f.key, draft: f.draft, published: f.published, state: f.state, version: f.version }));

const CLAVE = CLAVE_PEDIDA ?? previo[0].key;
const campo = previo.find((f) => f.key === CLAVE);
if (!campo) {
  console.error(`La clave '${CLAVE}' no pertenece al grupo '${GRUPO}'. Campos: ${previo.map((f) => f.key).join(', ')}`);
  process.exit(2);
}

/**
 * SI EL GRUPO NUNCA SE HA PUBLICADO, ESTA ORDEN NO PUEDE DESHACER LO QUE HACE.
 *
 * El paso 5 publica el GRUPO ENTERO, y la reposición del paso 8 devuelve el
 * VALOR de cada campo pero no su estado: `f.published ?? f.draft` publica el
 * borrador cuando nunca hubo publicado. El grupo queda publicado para siempre.
 *
 * Y no hay forma de deshacerlo por la API: `POST /{clave}/retire` responde 409
 * cuando la clave no está publicada —«no hay nada que retirar»— y, cuando sí lo
 * está, deja `FechaRetiro` puesta, que es un estado distinto de «nunca se
 * publicó». Volver atrás exige SQL directo.
 *
 * Pasó de verdad el 22 ago 2026 con `map_tutorial`, el único grupo del catálogo
 * que nadie había publicado nunca: la corrida lo publicó entero y el paso 9 lo
 * denunció con «Antes undefined, ahora "Bienvenido al Geovisor Ecosistémico"».
 * Se repuso con `UPDATE ContenidoWeb SET Publicado = NULL WHERE GrupoId = ...`.
 *
 * Que el texto se vea igual no significa que no haya pasado nada: coincide
 * porque el valor publicado es el mismo que el del registro, del que la página
 * ya tiraba. Lo que cambia es quién manda.
 */
// `state` distingue tres cosas que `published == null` confunde en dos:
// «publicado», «retirado» y «no_publicado». Solo la tercera es peligrosa.
//
// UN RETIRADO NO CORRE PELIGRO, y comprobarlo importa: cinco claves del catálogo
// están retiradas a propósito —`home_btn_about`, `home_btn_ejes`,
// `agenda_filter_fixed`, `agenda_filter_fixed_note`, `map_description`— y con la
// comprobación ingenua esta orden se plantaba ante `agenda_ui`, un grupo
// perfectamente publicado que solo arrastra dos retiradas. La API tampoco las
// republicaría: `canPublish = request.Publish && row.Retired is null`
// (`WebContentEndpoints.cs`).
const sinPublicar = previo.filter((f) => f.state === 'no_publicado');
if (sinPublicar.length && !PRIMERA_VEZ) {
  console.error(`El grupo '${GRUPO}' tiene ${sinPublicar.length} de ${previo.length} campos SIN PUBLICAR:`);
  for (const f of sinPublicar) console.error(`   ${f.key}`);
  console.error(
    '\nEsta orden publicaría el grupo entero y NO PUEDE deshacerlo: la reposición\n' +
    'devuelve el valor de cada campo, no su estado, y la API no sabe volver a\n' +
    '«nunca publicado» (retirar deja marca de retirado, que es otra cosa).\n' +
    '\nElija:\n' +
    '  · un grupo ya publicado, que es lo que esta orden sabe medir sin dejar rastro;\n' +
    '  · o --publicar-por-primera-vez, aceptando que el grupo quedará publicado.',
  );
  process.exit(2);
}
if (sinPublicar.length) {
  console.log(`AVISO: ${sinPublicar.length} campos sin publicar. Al terminar, el grupo quedará PUBLICADO.\n`);
}

console.log(`Grupo ${GRUPO} · clave de prueba ${CLAVE}\n`);
console.log('ESTADO PREVIO (por si hay que reponerlo a mano):');
for (const f of previo) {
  console.log(`   ${f.key}  v${f.version}  borrador=${JSON.stringify(f.draft)}  publicado=${JSON.stringify(f.published)}`);
}
console.log();

const publicadoAntes = (await publico())[CLAVE];

const escribir = (contenido, publicar) => api(`/admin/contenido-web/groups/${GRUPO}`, {
  token: csrf,
  body: JSON.stringify({
    fields: previo.map((f) => ({ key: f.key, content: f.key === CLAVE ? contenido : f.draft })),
    publish: publicar,
  }),
});

const guardado = await escribir(MARCADOR, false);
comprobar(guardado.estado === 200, 'guardar borrador responde 200', `guardar borrador responde ${guardado.estado}`);

const trasGuardar = JSON.parse((await api(`/admin/contenido-web/groups/${GRUPO}`)).cuerpo)
  .fields.find((f) => f.key === CLAVE);
comprobar(trasGuardar.draft === MARCADOR,
  'el borrador quedó escrito en la base',
  `el borrador dice ${JSON.stringify(trasGuardar.draft)}`);
comprobar(trasGuardar.version > campo.version,
  `la versión subió de ${campo.version} a ${trasGuardar.version} (token de concurrencia)`,
  `la versión no subió: sigue en ${trasGuardar.version}`);

// La comprobación que da sentido a todo el sistema.
comprobar((await publico())[CLAVE] === publicadoAntes,
  'el sitio público NO cambió: guardar no es publicar',
  'el sitio público cambió al guardar un borrador — la separación está rota');

const publicado = await escribir(MARCADOR, true);
comprobar(publicado.estado === 200, 'publicar responde 200', `publicar responde ${publicado.estado}`);
comprobar((await publico())[CLAVE] === MARCADOR,
  'el sitio público ya sirve el texto publicado',
  'el sitio público NO recogió la publicación');

const historial = JSON.parse((await api(`/admin/contenido-web/${CLAVE}/history`)).cuerpo);
const entradas = Array.isArray(historial) ? historial : (historial.items ?? historial.entries ?? []);
comprobar(entradas.length > 0,
  `el historial registró el cambio (${entradas.length} entradas)`,
  'el historial no registró nada');

const repuesto = await api(`/admin/contenido-web/groups/${GRUPO}`, {
  token: csrf,
  body: JSON.stringify({
    fields: previo.map((f) => ({ key: f.key, content: f.published ?? f.draft })),
    publish: true,
  }),
});
comprobar(repuesto.estado === 200, 'reposición responde 200', `reposición responde ${repuesto.estado}`);

const publicadoDespues = (await publico())[CLAVE];
comprobar(publicadoDespues === publicadoAntes,
  `el sitio quedó como estaba: ${JSON.stringify(publicadoDespues)}`,
  `el sitio NO quedó como estaba. Antes ${JSON.stringify(publicadoAntes)}, ahora ${JSON.stringify(publicadoDespues)}`);

console.log(fallos === 0
  ? '\n✓ el ciclo completo funciona y la base quedó como estaba'
  : `\n✗ ${fallos} comprobaciones fallaron — revise el estado previo de arriba`);
process.exit(fallos === 0 ? 0 : 1);
