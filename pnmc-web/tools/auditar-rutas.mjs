#!/usr/bin/env node
/**
 * Contrasta las rutas que el API expone con las que el frontend llama, en los dos sentidos.
 *
 * POR QUE EXISTE. El 12 de septiembre de 2026, en la pausa de consolidación, hizo falta responder
 * «qué llamadas del frontend no las sirve nadie» y «qué rutas del API no las llama nadie». Hacerlo
 * leyendo 64 ficheros de endpoints y 34 servicios a mano no es repetible: la respuesta caduca con el
 * siguiente corte. Esto lo vuelve una comprobación de treinta segundos.
 *
 * NO ES UN TRINQUETE, y por eso no falla la construcción. Una ruta sin consumidor puede ser
 * legítima —una herramienta de operación sin pantalla, un archivo que el navegador pide como
 * imagen— y una llamada sin ruta puede ser una base a la que se concatena. Lo que da es EVIDENCIA
 * para decidir, no un veredicto.
 *
 * COMO SE LEE:
 *   A) llamadas del frontend que ninguna ruta sirve  → casi siempre código muerto o una errata
 *   B) rutas del API que nadie llama                 → candidatas a retirar, previa comprobación
 *
 * LOS FALSOS POSITIVOS YA COMPROBADOS, para no volver a investigarlos:
 *   · `/publico/archivos/{id}` la pide el navegador como `src` de una imagen, no un servicio.
 *   · (RESUELTOS EL 12 DE SEPTIEMBRE DE 2026, segunda vuelta) `/publico/categorias-contenido`,
 *     `/publico/proyectos-transversales` y `/institucional/reclamaciones-administracion` ya NO
 *     aparecen: el lector compone `MapGet(string.Empty, ...)` con su grupo, y distingue una cadena
 *     de consulta interpolada de un parámetro de ruta. Eran cuatro falsas alarmas por corrida, y
 *     una puerta que da cuatro falsas alarmas es una puerta que nadie lee.
 *   · Las cuatro de `MapEndpoints` —geojson, summary, drilldown— y las dos de normalización de
 *     versiones históricas no tienen pantalla hoy: se revisan en el bloque del mapa, no aquí.
 *   · `ParticipationEndpoints` no tiene pantalla ni filas; es candidata a retirarse.
 *
 *   node tools/auditar-rutas.mjs          desde pnmc-web/
 */
import { readFileSync, readdirSync, statSync } from 'node:fs';
import { join, relative } from 'node:path';
import { fileURLToPath } from 'node:url';

// `.pathname` deja los espacios como %20 y `readdirSync` no los deshace: la ruta de este proyecto
// los tiene. `fileURLToPath` es la conversión correcta.
const RAIZ = fileURLToPath(new URL('..', import.meta.url));
const API = join(RAIZ, '../pnmc-api/src/PNMC.Api/Endpoints');
const WEB = join(RAIZ, 'src/app');
const BASE = '/api/v1';

/** Normaliza una ruta para poder compararla: todo parámetro pasa a `{}`. */
const norm = r => r.replace(/\{[^}]*\}/g, '{}').replace(/\/{2,}/g, '/').replace(/\/$/, '');

function ficheros(dir, ext) {
  const salida = [];
  for (const nombre of readdirSync(dir)) {
    const ruta = join(dir, nombre);
    if (statSync(ruta).isDirectory()) salida.push(...ficheros(ruta, ext));
    else if (nombre.endsWith(ext) && !nombre.endsWith('.spec.ts')) salida.push(ruta);
  }
  return salida;
}

// ---------- Las rutas que el API expone, compuestas desde el código --------------------------
const rutasApi = new Map(); // ruta normalizada -> "VERBO · fichero"
for (const ruta of ficheros(API, '.cs')) {
  const txt = readFileSync(ruta, 'utf8');
  const grupos = {};
  for (const m of txt.matchAll(/var\s+(\w+)\s*=\s*(\w+)\.MapGroup\("([^"]+)"\)/g)) {
    grupos[m[1]] = (grupos[m[2]] ?? '') + m[3];
  }
  // `string.Empty` ES UNA RUTA HIJA VALIDA, y no leerla costaba tres falsos positivos por corrida.
  // `publico.MapGet(string.Empty, Listar)` sirve la raíz del grupo: `/publico/categorias-contenido`.
  // Sin esta alternativa, la ruta no se registraba y la llamada del frontend salía como «no
  // servida» —tres veces, corrida tras corrida, hasta que nadie leyó la lista—.
  for (const m of txt.matchAll(/(\w+)\.Map(Get|Post|Put|Patch|Delete)\(\s*(?:"([^"]*)"|string\.Empty)/g)) {
    const completa = norm(BASE + (grupos[m[1]] ?? '') + (m[3] ?? ''));
    if (!rutasApi.has(completa)) rutasApi.set(completa, []);
    rutasApi.get(completa).push(`${m[2].toUpperCase()} · ${relative(API, ruta)}`);
  }
}

// ---------- Las rutas que el frontend llama ---------------------------------------------------
const llamadas = new Map(); // ruta normalizada -> ficheros
for (const ruta of ficheros(WEB, '.ts')) {
  const txt = readFileSync(ruta, 'utf8');
  for (const m of txt.matchAll(/['"`](\/api\/v1\/[^'"`\n]*)['"`]/g)) {
    // UNA INTERPOLACION PEGADA AL ULTIMO SEGMENTO ES UNA CADENA DE CONSULTA, NO UN PARAMETRO DE
    // RUTA. Un parámetro de ruta siempre lleva barra delante —`/festivales/${id}`—; `${params}`
    // pegado a la palabra —`.../reclamaciones-administracion${params}`— solo puede ser `?a=b`.
    // Contarlo como parte de la ruta producía un cuarto falso positivo fijo.
    const limpia = norm(m[1].split('?')[0]
      .replace(/\$\{[^}]*\}/g, '{}')
      .replace(/([^/]){}$/, '$1'));
    if (!llamadas.has(limpia)) llamadas.set(limpia, new Set());
    llamadas.get(limpia).add(relative(WEB, ruta));
  }
}

const api = [...rutasApi.keys()];
console.log(`Rutas del API: ${api.length}   ·   rutas que llama el frontend: ${llamadas.size}\n`);

console.log('=== A) LLAMADAS DEL FRONTEND QUE NINGUNA RUTA SIRVE ===');
let rotas = 0;
for (const [r, fs] of [...llamadas].sort()) {
  if (rutasApi.has(r) || api.some(a => a.startsWith(r + '/'))) continue;
  console.log(`  ${r}\n      <- ${[...fs].slice(0, 2).join(', ')}`);
  rotas++;
}
console.log(`  TOTAL: ${rotas}\n`);

console.log('=== B) RUTAS DEL API QUE NADIE LLAMA ===');
const porFichero = new Map();
for (const [r, marcas] of rutasApi) {
  if (llamadas.has(r)) continue;
  if ([...llamadas.keys()].some(l => l.startsWith(r + '/') || r.startsWith(l + '/'))) continue;
  for (const marca of marcas) {
    const [verbo, fichero] = marca.split(' · ');
    if (!porFichero.has(fichero)) porFichero.set(fichero, []);
    porFichero.get(fichero).push(`${verbo} ${r}`);
  }
}
for (const [fichero, lista] of [...porFichero].sort((a, b) => b[1].length - a[1].length)) {
  console.log(`  ${fichero} (${lista.length})`);
  for (const l of lista) console.log(`      ${l}`);
}
console.log(`  TOTAL: ${[...porFichero.values()].reduce((n, l) => n + l.length, 0)}`);
