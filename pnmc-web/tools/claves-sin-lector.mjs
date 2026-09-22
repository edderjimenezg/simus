/**
 * ¿Ofrece el panel algún texto que ninguna página lee?
 *
 * Ese fue el defecto de fondo de todo este rescate: una editora escribía,
 * guardaba, publicaba, y la página seguía igual. No fallaba nada —por eso duró
 * tanto—. Se descubrió midiendo: publicando un marcador en cada clave y
 * renderizando el sitio. Esa medición es la prueba definitiva, pero necesita la
 * base, la API y un navegador; no sirve como puerta de cada cambio.
 *
 * Esta herramienta es la versión barata y permanente: no prueba que la clave se
 * PINTE, prueba que alguien la NOMBRA. Con eso basta para atrapar lo que de
 * verdad ocurre en el día a día —declarar un campo nuevo y olvidar conectarlo,
 * o renombrar una clave y dejar el otro lado sin tocar—.
 *
 *   node tools/claves-sin-lector.mjs
 *
 * Devuelve 1 si alguna clave no aparece citada ni está en la lista de patrones.
 */
import { readFileSync, readdirSync, statSync } from 'node:fs';
import { join, relative, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..');
const FUENTE = join(ROOT, 'src/app');
const CATALOGO = join(ROOT, '../pnmc-api/src/PNMC.Infrastructure/Data/web-content-catalog.json');

/**
 * Claves que ninguna búsqueda literal puede encontrar porque el código las
 * ARMA en tiempo de ejecución. Cada patrón dice quién las construye, para que
 * esta lista no se convierta en el desván donde se esconden las de verdad
 * huérfanas. Si añade una entrada, escriba también dónde se arma.
 *
 * CUANDO EL PATRÓN LLEVA UN NÚMERO, PONGA `cuantas`. Esa es la diferencia entre
 * una exención útil y un agujero: `about_timeline_\d+_title` daba por buena
 * cualquier posición, pero el componente solo dibuja las cinco que caben en su
 * lista de imágenes —`[11, 12, 13, 14, 15]`—. Añadir un sexto hito generaba tres
 * claves nuevas, el panel las ofrecía, alguien las publicaba y la página no
 * cambiaba; y esta puerta decía que todo estaba bien. Con `cuantas`, la sexta se
 * denuncia como huérfana, que es lo que es.
 */
const ARMADAS_AL_VUELO = [
  {
    patron: /^nav_([a-z-]+)$/,
    donde: 'core/services/navigation.service.ts · getResolvedNavigationLinks() arma `nav_${link.id}`',
    excepto: ['nav_components_title'],
    // Los identificadores de `navigationLinks`, uno por uno. El patrón por sí
    // solo eximía cualquier `nav_loquesea`, que es el mismo agujero que ya se
    // cerró en los hitos: renombrar `nav_galeria` a medias, o volver a declarar
    // `nav_mapa` tras el renombrado a `nav_ecosistema`, pasaba en verde.
    //
    // `simus` NO está, y es deliberado: ese enlace lleva rótulo fijo porque va
    // a una plataforma externa del Ministerio. Si alguien declarara `nav_simus`
    // en el registro, esta puerta lo denunciaría — que es justo lo que se
    // quiere, porque nadie lo leería.
    soloEstas: ['pnmc', 'ejes', 'editorial', 'galeria', 'noticias', 'agenda', 'ecosistema'],
  },
  {
    // Acotado a los tres ejes que existen. Con `\d` abierto, `eje04_title`
    // pasaba en verde: el panel ofrecería un cuarto eje que ninguna página
    // dibuja. Es el cuarto agujero de esta forma que se cierra en este archivo,
    // y el propio comentario de cabecera ya lo condenaba.
    //
    // El número de COMPONENTE se deja abierto a propósito: no son los mismos en
    // cada eje —el eje 2 tiene seis y los otros dos— y acotarlo aquí exigiría
    // repetir ese reparto en un segundo sitio, que es justo lo que produce las
    // desincronizaciones que esta puerta persigue. Lo que sí vigila la puerta es
    // que cada clave declarada tenga lector.
    patron: /^eje0([123])_(title|purpose|desc\d|c\d_(title|desc))$/,
    donde: 'core/cms/resolve-ejes.ts · arma las claves por posición del eje y del componente (3 ejes)',
  },
  {
    // La imagen de cada eje. La arma `resolve-ejes.ts` con `eje_0${n}_media`,
    // igual que las claves de texto de arriba y en la misma función.
    //
    // `cuantas: 3` y no un patrón abierto: la configuración tiene tres ejes, y
    // un cuarto declararía `eje_04_media` en el registro sin que nadie lo
    // leyera. Con el tope, esa cuarta se denuncia como huérfana, que es lo que
    // sería.
    patron: /^eje_0(\d)_media$/,
    cuantas: 3,
    donde: 'core/cms/resolve-ejes.ts · arma `eje_0${n}_media` (3 ejes)',
  },
  {
    // Las ocho fotos del carrusel de «Rutas de Accion Territorial». Las arma el propio
    // componente por POSICION dentro de `STRATEGIES_DATA`, igual que hacia el
    // `RANDOM_GALLERY_IMAGES[n]` que sustituyeron.
    //
    // `cuantas: 8` y no un patron abierto, por el mismo motivo que en los ejes y los hitos: sin
    // el tope, declarar `home_ruta_9` en el registro pasaria en verde y el panel ofreceria una
    // novena tarjeta que la portada no dibuja.
    patron: /^home_ruta_(\d+)$/,
    cuantas: 8,
    donde: 'features/home/.../home-strategies-section.component.ts · arma `home_ruta_${i + 1}` (8 tarjetas)',
  },
  {
    // Las seis fotos de los procesos del Ecosistema en la portada. Seis y no ocho: el
    // componente filtra `ECOSYSTEM_CATEGORIES` por `proceso`, y Agrupaciones y Agentes quedan
    // fuera porque no tienen directorio.
    patron: /^home_eco_(\d+)$/,
    cuantas: 6,
    donde: 'features/home/.../mapa-ecosistemico-preview.component.ts · arma `home_eco_${i + 1}` (6 procesos)',
  },
  {
    patron: /^strategy_celebra_[a-z_]+$/,
    donde: 'core/cms/resolve-strategy.ts · arma `${cmsPrefix}_${sufijo}`',
  },
  {
    // Territorios Sonoros, desde. Solo las DOS que su pagina pinta: no
    // tiene el relato de tres partes de Celebra la Musica, asi que `tieneRelato: false` y
    // `resolveStrategy` no arma `_section_title` ni `_mission` ni las tres de la edicion.
    //
    // `soloEstas` y no un patron abierto: sin el tope, declarar `strategy_territorios_mission` en
    // el registro pasaria en verde y el panel ofreceria un campo que ninguna pagina dibuja, que
    // es exactamente el defecto que esta puerta persigue.
    patron: /^strategy_territorios_([a-z_]+)$/,
    soloEstas: ['hero_desc', 'intro'],
    donde: 'core/cms/resolve-strategy.ts · arma `${cmsPrefix}_${sufijo}` (sin relato)',
  },
  {
    // Las cinco fotos de los hitos de `/pnmc`. Las arma el componente por POSICION de la
    // tarjeta, igual que las ocho Rutas de la portada, y con el mismo modo de fallar: un indice
    // corrido pinta la foto de otro hito.
    patron: /^about_hito_(\d+)$/,
    cuantas: 5,
    donde: 'features/pnmc/.../sobre-el-pnmc-page.component.ts · arma `about_hito_${n}` (5 hitos)',
  },
  {
    // Las seis del marco normativo, en el mismo componente y con la misma forma.
    patron: /^about_norma_(\d+)$/,
    cuantas: 6,
    donde: 'features/pnmc/.../sobre-el-pnmc-page.component.ts · arma `about_norma_${n}` (6 normas)',
  },
  {
    patron: /^about_team_\d+_(role|name|email)$/,
    donde: 'la nómina viaja como bloque propio (WebTeamSeeder), no como texto suelto',
  },
  {
    patron: /^about_timeline_(\d+)_(year|title|desc)$/,
    // Cinco: las que caben en `timelineEvents = [11, 12, 13, 14, 15]`.
    cuantas: 5,
    donde: 'features/pnmc/.../sobre-el-pnmc-page.component.ts · arma `about_timeline_${number}_...` (5 hitos)',
  },
  {
    patron: /^about_normative_(\d+)_(year|title|desc)$/,
    // Seis: las de `normativeStages = [16, 17, 18, 0, 1, 3]`.
    cuantas: 6,
    donde: 'features/pnmc/.../sobre-el-pnmc-page.component.ts · arma `about_normative_${number}_...` (6 normas)',
  },
  {
    // Los ocho identificadores de `keysMap`, uno por uno. Con `[a-z]+` abierto,
    // `strat_loquesea_title` pasaba en verde: una novena tarjeta declarada y no
    // cableada quedaba invisible para la puerta. Y aquí no hay red por otro
    // lado —a diferencia de los ejes y del tutorial del mapa, ninguna prueba
    // fija estas claves—, así que la puerta es lo único que hay.
    patron: /^strat_([a-z]+)_(tag|title|desc)$/,
    soloEstas: ['celebra', 'territorios', 'congreso', 'tempos', 'voces', 'jazz', 'mercados', 'mesas'],
    donde: 'features/home/.../home-strategies-section.component.ts · arma `strat_${shortKey}_...` (8 tarjetas)',
  },
  {
    // Siete: los de `ICONOS_TUTORIAL`, que tiene exactamente siete entradas.
    //
    // Aquí el tope SÍ se puede escribir sin duplicar nada —a diferencia del de
    // los ejes, donde el número de componentes cambia por eje—, así que la
    // regla de la cabecera se cumple sin excusa. `mapa-tutorial.spec.ts` ya fija
    // los siete por su lado; esto es el cinturón además de los tirantes, y
    // sobre todo evita que el siguiente copie el patrón flojo.
    patron: /^map_tutorial_(\d)_(title|desc)$/,
    cuantas: 7,
    donde: 'features/map/.../mapa-ecosistemico-page.component.ts · arma `map_tutorial_${paso}_...` sobre ICONOS_TUTORIAL (7 pasos)',
  },
];

const archivos = [];
(function recorrer(dir) {
  for (const entrada of readdirSync(dir)) {
    const ruta = join(dir, entrada);
    if (statSync(ruta).isDirectory()) { recorrer(ruta); continue; }
    // Se excluyen las pruebas, el propio registro y EL PANEL: citar una clave en
    // el sitio donde se declara, en un `expect`, o en el formulario que sirve
    // para escribirla, no es leerla.
    //
    // Lo del panel no es un detalle. La pregunta que responde esta puerta es
    // «¿puede una editora escribir, guardar y publicar este campo sin que la
    // página cambie?», y la previsualización del panel dibuja el texto que la
    // editora acaba de teclear. Contarla como lectora hacía que ~35 claves
    // `about_*` pasaran la puerta por aparecer en la previsualización, sin que
    // nadie comprobara que alguna página pública las lee.
    if (!/\.(ts|html)$/.test(entrada)) continue;
    // El registro NOMBRA todas las claves por definición: contarlo como lector
    // haría que cada clave se diera por conectada consigo misma. Vale para los
    // dos registros —el de textos y el de imágenes—, y olvidar el segundo fue un
    // falso verde real: las dieciséis ranuras nuevas pasaron la puerta el mismo
    // día que se declararon, sin que ninguna página las leyera.
    if (/\.spec\.ts$/.test(entrada) || /web-(text|image)-registry\.ts$/.test(entrada)) continue;
    if (/admin-web-texts-panel\.component\.(ts|html)$/.test(entrada)) continue;
    archivos.push(ruta);
  }
})(FUENTE);

/**
 * Quita los comentarios antes de buscar.
 *
 * UN COMENTARIO NO LEE UNA CLAVE. Sin esto, escribir «se llamó `nav_mapa`» en un
 * comentario bastaba para que la puerta diera esa clave por conectada: pasó de
 * verdad el 22 ago 2026, con tres comentarios que documentaban un renombrado.
 * La clave retirada volvía a pasar en verde por la explicación de su retirada.
 *
 * El recorte de `//` exige que no venga precedido de dos puntos, para no partir
 * un `https://` por la mitad y perder lo que venga después en esa línea.
 */
const sinComentarios = (texto) => texto
  .replace(/<!--[\s\S]*?-->/g, ' ')
  .replace(/\/\*[\s\S]*?\*\//g, ' ')
  .replace(/(^|[^:])\/\/[^\n]*/g, '$1');

const fuentes = archivos.map((f) => [
  relative(ROOT, f).split(/[\\/]/).join('/'),
  sinComentarios(readFileSync(f, 'utf8')),
]);

const catalogo = JSON.parse(readFileSync(CATALOGO, 'utf8'));
/**
 * LAS IMÁGENES ENTRAN POR AQUÍ, y esta línea es la extensión entera.
 *
 * El 29 de agosto de 2026 el catálogo ganó dieciséis ranuras de imagen. Sin
 * `catalogo.images`, esta herramienta las daba por buenas sin mirarlas: el
 * arreglo le era invisible y salía con código 0 aunque una clave de imagen no
 * la citara nadie. Es exactamente el defecto que esta puerta existe para
 * atrapar —declarar una ranura y olvidar conectarla—, y era el más probable en
 * un catálogo recién nacido.
 *
 * No hace falta enseñarle ningún patrón nuevo: la búsqueda es por SUBCADENA
 * (`texto.includes(clave)` más abajo), no por forma de llamada, así que
 * `imagen('hero_ejes')` y `[bgImage]="imagen('hero_ejes')"` le valen igual.
 */
const entradas = Array.isArray(catalogo)
  ? catalogo
  : [...(catalogo.texts ?? catalogo.entries ?? []), ...(catalogo.images ?? [])];

const sinLector = [];
const porPatron = new Map();

for (const entrada of entradas) {
  const clave = entrada.key ?? entrada.Key;
  const donde = fuentes.filter(([, texto]) => texto.includes(clave)).map(([f]) => f);
  if (donde.length) continue;

  const regla = ARMADAS_AL_VUELO.find((r) => {
    const casa = r.patron.exec(clave);
    if (!casa || (r.excepto ?? []).includes(clave)) { return false; }
    // Un patrón acotado solo exime las posiciones que el código dibuja de
    // verdad. La séptima norma casa con la expresión regular pero no la lee
    // nadie, así que no está exenta: está huérfana.
    if (r.cuantas !== undefined) {
      const posicion = Number(casa[1]);
      return Number.isInteger(posicion) && posicion >= 1 && posicion <= r.cuantas;
    }
    // Igual que `cuantas`, pero para trozos que no son números: solo exime los
    // que el código construye de verdad.
    //
    // Antes esto recortaba la clave por su primer `_`, y con eso solo servía
    // para `nav_*`. Ahora lee el MISMO grupo de captura que `cuantas`, así que
    // vale para cualquier patrón que marque con paréntesis la parte variable.
    if (r.soloEstas !== undefined) {
      return r.soloEstas.includes(casa[1]);
    }
    return true;
  });
  if (regla) {
    porPatron.set(regla.donde, (porPatron.get(regla.donde) ?? 0) + 1);
    continue;
  }
  sinLector.push({ clave, grupo: entrada.groupLabel ?? entrada.GroupLabel ?? entrada.groupId ?? entrada.GroupId });
}

console.log(`Catálogo: ${entradas.length} claves · ${fuentes.length} archivos de código revisados\n`);
for (const [donde, cuantas] of porPatron) {
  console.log(`  ${String(cuantas).padStart(3)} armadas al vuelo — ${donde}`);
}

if (!sinLector.length) {
  console.log('\n✓ Todas las claves del panel tienen quien las lea.');
  process.exit(0);
}

console.error(`\n✗ ${sinLector.length} claves que el panel ofrece y nadie nombra en el código:\n`);
for (const { clave, grupo } of sinLector) {
  console.error(`  ${clave.padEnd(34)} ${grupo}`);
}
console.error(
  '\nCada una es un campo que una editora puede escribir, guardar y publicar sin que\n' +
  'la página cambie. Conéctela, retírela del registro, o —si el código la arma en\n' +
  'tiempo de ejecución— añada su patrón a ARMADAS_AL_VUELO diciendo quién la arma.',
);
process.exit(1);
