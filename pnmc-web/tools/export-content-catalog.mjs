/**
 * Exporta el registro de contenido editable a un JSON que la API incrusta como
 * recurso y usa para sembrar la tabla [ContenidoWeb].
 *
 * El registro TypeScript (src/app/core/cms/registro-de-textos-web.ts) es la fuente de
 * verdad de las 238 claves: sus etiquetas, sus límites y sus textos por defecto.
 * Este script evita que exista una segunda copia mantenida a mano en C#.
 *
 * Se ejecuta con `npm run cms:catalog`. El JSON resultante se versiona en el
 * repositorio, y una prueba del backend comprueba que sigue coincidiendo con el
 * registro, para que nadie edite uno sin el otro.
 */
import { readFile, writeFile, mkdir } from 'node:fs/promises';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { createHash } from 'node:crypto';

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..');
const REGISTRY = join(ROOT, 'src/app/core/cms/registro-de-textos-web.ts');
const IMAGE_REGISTRY = join(ROOT, 'src/app/core/cms/registro-de-imagenes-web.ts');
const OUT = join(ROOT, '../pnmc-api/src/PNMC.Infrastructure/Data/web-content-catalog.json');

/**
 * El registro es TypeScript, así que se evalúa tras quitarle las anotaciones de
 * tipo. Se hace sobre un recorte acotado —del helper `field` al cierre del
 * arreglo— para que un cambio en el resto del archivo no altere el resultado
 * en silencio.
 */
const source = await readFile(REGISTRY, 'utf8');

const start = source.indexOf('const field =');
const end = source.indexOf('/** Compatibilidad con el editor');
if (start < 0 || end < 0) {
  throw new Error('No se ubicó el bloque de datos del registro; revise registro-de-textos-web.ts');
}

const block = source
  .slice(start, end)
  .replace('(key: string, label: string, limit: number, defaultValue: string): DefinicionDeCampoDeTexto', '(key, label, limit, defaultValue)')
  .replace('export const WEB_TEXT_GROUPS: DefinicionDeGrupoDeTexto[]', 'const WEB_TEXT_GROUPS')
  .replace(/export const TEAM_DEFAULTS: string\[\]\[\]/, 'const TEAM_DEFAULTS');

const groups = eval(`${block}\n;WEB_TEXT_GROUPS`);
const teamDefaults = eval(`${block}\n;TEAM_DEFAULTS`);

const entries = groups.flatMap((group) =>
  group.fields.map((field) => ({
    key: field.key,
    groupId: group.id,
    groupLabel: group.label,
    section: group.section,
    label: field.label,
    limit: field.limit,
    defaultValue: field.defaultValue,
  })),
);

/**
 * Nómina del equipo (incremento 2B). Se arma aquí, y no en C#, aplicando la misma
 * regla que el front-end usa cuando no hay registro propio: se descartan las
 * filas sin nombre —hay componentes con el liderazgo vacante— y el identificador
 * conserva la posición original, para que una vacante que se llene más adelante
 * no renumere a las demás. Las dos primeras son coordinación; el resto, liderazgo.
 */
const team = teamDefaults
  .map((member, position) => ({ member, position }))
  .filter(({ member }) => member[1].trim().length > 0)
  .map(({ member, position }, index) => ({
    id: `team-${position + 1}`,
    group: index < 2 ? 'coordination' : 'leadership',
    role: member[0],
    name: member[1],
    email: member[2],
    // Las fotografías no se compilan: entran una por una desde el panel.
    photo: '',
  }));

/**
 * Las imágenes administrables (29 ago 2026). Mismo mecanismo que los textos y
 * con MARCADORES PROPIOS, que es la parte que se puede hacer mal: este script
 * recorta entre dos literales, y reutilizar los del registro de textos daría un
 * catálogo de imágenes VACÍO sin ningún error visible. Los suyos son
 * `const image =` y el cierre del arreglo.
 */
const imageSource = await readFile(IMAGE_REGISTRY, 'utf8');
const imageStart = imageSource.indexOf('const image =');
const imageEnd = imageSource.indexOf('/** Todas las claves aplanadas');
if (imageStart < 0 || imageEnd < 0) {
  throw new Error('No se ubicó el bloque de datos del registro de imágenes; revise registro-de-imagenes-web.ts');
}

const imageBlock = imageSource
  .slice(imageStart, imageEnd)
  .replace(
    /\(\s*key: string,[\s\S]*?\): DefinicionDeCampoDeImagen/,
    '(key, label, use, editable, alt, suggestedWidth, suggestedHeight, defaultUrl)',
  )
  .replace('export const WEB_IMAGE_GROUPS: DefinicionDeGrupoDeImagenes[]', 'const WEB_IMAGE_GROUPS');

const imageGroups = eval(`${imageBlock}\n;WEB_IMAGE_GROUPS`);

const images = imageGroups.flatMap((group) =>
  group.images.map((img) => ({
    key: img.key,
    groupId: group.id,
    groupLabel: group.label,
    section: group.section,
    label: img.label,
    use: img.use,
    editable: img.editable,
    alt: img.alt,
    suggestedWidth: img.suggestedWidth,
    suggestedHeight: img.suggestedHeight,
    defaultUrl: img.defaultUrl,
  })),
);

// --- Comprobaciones que deben fallar aquí y no en producción ---
const problems = [];
const seen = new Set();
for (const entry of entries) {
  if (seen.has(entry.key)) { problems.push(`clave duplicada: ${entry.key}`); }
  seen.add(entry.key);
  if (entry.defaultValue.length > entry.limit) {
    problems.push(`${entry.key}: el texto por defecto (${entry.defaultValue.length}) excede su límite (${entry.limit})`);
  }
  if (entry.key.length > 160) { problems.push(`${entry.key}: clave más larga que la columna (160)`); }
  if (entry.label.length > 240) { problems.push(`${entry.key}: etiqueta más larga que la columna (240)`); }
}
// Los mismos topes que valida la API al guardar. Si un valor compilado los
// excediera, la siembra entraría un dato que después nadie podría volver a
// guardar desde el panel.
const TEAM_LIMITS = { id: 40, name: 120, role: 160, email: 180 };
const teamIds = new Set();
for (const member of team) {
  if (teamIds.has(member.id)) { problems.push(`equipo: identificador duplicado ${member.id}`); }
  teamIds.add(member.id);
  for (const [campo, limite] of Object.entries(TEAM_LIMITS)) {
    if (member[campo].length > limite) {
      problems.push(`equipo ${member.id}: ${campo} (${member[campo].length}) excede su límite (${limite})`);
    }
  }
  if (member.email && !member.email.includes('@')) {
    problems.push(`equipo ${member.id}: el correo "${member.email}" no tiene arroba`);
  }
}

// Los mismos topes que impone MediosWebContrato y que la base repite en sus CHECK.
const IMAGE_LIMITS = { key: 160, label: 240, alt: 300, groupId: 120, groupLabel: 160, section: 120 };
const ALLOWED_USES = ['fondo', 'logotipo'];
const imageKeys = new Set();
for (const img of images) {
  if (imageKeys.has(img.key)) { problems.push(`imagen: clave duplicada ${img.key}`); }
  imageKeys.add(img.key);
  if (seen.has(img.key)) {
    // Una clave que exista a la vez en textos y en imágenes rompería la puerta de
    // claves huérfanas, que busca por subcadena y no distingue de qué catálogo salió.
    problems.push(`imagen ${img.key}: la clave ya existe en el catálogo de textos`);
  }
  for (const [campo, limite] of Object.entries(IMAGE_LIMITS)) {
    if (String(img[campo]).length > limite) {
      problems.push(`imagen ${img.key}: ${campo} (${String(img[campo]).length}) excede su límite (${limite})`);
    }
  }
  if (!ALLOWED_USES.includes(img.use)) {
    problems.push(`imagen ${img.key}: uso "${img.use}" no está en CK_MediosWeb_Uso`);
  }
  if (!img.defaultUrl) {
    // Sin URL compilada, retirar esa imagen dejaría un <img src=""> — y un src
    // vacío hace que el navegador vuelva a pedir la página entera.
    problems.push(`imagen ${img.key}: no tiene URL compilada de respaldo`);
  }
}

if (problems.length) {
  problems.forEach((p) => console.error(`  ${p}`));
  throw new Error(`${problems.length} problemas en el catálogo; no se escribió nada.`);
}

/**
 * Huella del contenido del catálogo. La API la registra al sembrar, de modo que
 * un catálogo desactualizado se puede detectar comparando huellas en vez de
 * recorrer las 238 filas.
 */
const payload = { version: 1, entries, team, images };
const hash = createHash('sha256').update(JSON.stringify({ entries, team, images })).digest('hex').slice(0, 16);
payload.catalogHash = hash;

await mkdir(dirname(OUT), { recursive: true });
await writeFile(OUT, `${JSON.stringify(payload, null, 2)}\n`, 'utf8');

const groupCount = new Set(entries.map((e) => e.groupId)).size;
const sectionCount = new Set(entries.map((e) => e.section)).size;
const imageGroupCount = new Set(images.map((i) => i.groupId)).size;
console.log(`Catálogo exportado: ${entries.length} claves · ${groupCount} grupos · ${sectionCount} secciones · ${team.length} personas en la nómina`);
console.log(`  imágenes ${images.length} ranuras · ${imageGroupCount} grupos · ${images.filter((i) => !i.editable).length} no editables`);
console.log(`  huella  ${hash}`);
console.log(`  destino ${OUT}`);
