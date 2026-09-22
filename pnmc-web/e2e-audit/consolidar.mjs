#!/usr/bin/env node
/**
 * Consolida los informes por (dispositivo, ruta) en un veredicto legible.
 *
 * Uso:  node e2e-audit/consolidar.mjs [directorio]
 *
 * No inventa nada: solo agrega lo que el barrido midio. Cuando una cifra varia
 * entre dispositivos lo dice, porque esa variacion ES el hallazgo.
 */
import fs from 'node:fs';
import path from 'node:path';

const DIR = process.argv[2] || process.env.PNMC_AUDIT_OUT || path.join(process.cwd(), 'e2e-audit', '.informe');

const ficheros = fs.readdirSync(DIR).filter((f) => f.endsWith('.json'));
if (!ficheros.length) {
  console.error('No hay informes en ' + DIR);
  process.exit(1);
}

const datos = ficheros.map((f) => JSON.parse(fs.readFileSync(path.join(DIR, f), 'utf8')));
const dispositivos = [...new Set(datos.map((d) => d.dispositivo))];
const rutas = [...new Set(datos.map((d) => d.id))];
const buscar = (disp, id) => datos.find((d) => d.dispositivo === disp && d.id === id);

const linea = (s) => console.log(s);
const orden = ['movil-360', 'movil-414', 'tablet-vertical', 'tablet-horizontal', 'portatil', 'escritorio'];
const disps = orden.filter((d) => dispositivos.includes(d));

linea('# Barrido de dispositivos — resultado medido');
linea('');
linea('Informes: ' + datos.length + ' (' + rutas.length + ' rutas x ' + disps.length + ' dispositivos)');
linea('');

// ---------- 1. Recorte y desborde -------------------------------------------
linea('## 1. Contenido cortado o desbordado');
linea('');
linea('| Ruta | ' + disps.join(' | ') + ' |');
linea('|---|' + disps.map(() => '---').join('|') + '|');
for (const id of rutas) {
  const celdas = disps.map((d) => {
    const x = buscar(d, id);
    if (!x) return '—';
    const marcas = [];
    if (x.desborde?.hay) marcas.push('DESBORDA +' + x.desborde.exceso + 'px');
    if (x.recorte?.hay) marcas.push('corta ' + x.recorte.elementos.length);
    if (x.recorte?.paginaNoDesplazable) marcas.push('SIN SCROLL');
    return marcas.length ? marcas.join(', ') : 'ok';
  });
  linea('| `' + id + '` | ' + celdas.join(' | ') + ' |');
}
linea('');

// ---------- 2. Objetivos tactiles -------------------------------------------
linea('## 2. Objetivos tactiles por debajo de 24x24 px (WCAG 2.5.8)');
linea('');
linea('| Ruta | ' + disps.map((d) => d.replace('tablet-', 'tab-').replace('movil-', 'mov-')).join(' | ') + ' |');
linea('|---|' + disps.map(() => '---').join('|') + '|');
let totalPeq = 0;
for (const id of rutas) {
  const celdas = disps.map((d) => {
    const x = buscar(d, id);
    if (!x) return '—';
    totalPeq += x.tactiles.pequenos;
    const pct = x.tactiles.total ? Math.round((x.tactiles.pequenos / x.tactiles.total) * 100) : 0;
    return x.tactiles.pequenos + '/' + x.tactiles.total + ' (' + pct + '%)';
  });
  linea('| `' + id + '` | ' + celdas.join(' | ') + ' |');
}
linea('');

// ---------- 3. Contraste -----------------------------------------------------
linea('## 3. Contraste medido sobre el DOM');
linea('');
const porColor = new Map();
for (const d of datos) {
  for (const f of d.contraste.peores) {
    const k = f.color + ' sobre ' + f.fondo;
    if (!porColor.has(k)) porColor.set(k, { ...f, veces: 0, rutas: new Set() });
    const e = porColor.get(k);
    e.veces++;
    e.rutas.add(d.id);
    if (f.ratio < e.ratio) e.ratio = f.ratio;
  }
}
const peores = [...porColor.entries()].sort((a, b) => a[1].ratio - b[1].ratio).slice(0, 20);
linea('| Color sobre fondo | Ratio | Minimo | Apariciones | Rutas | Ejemplo |');
linea('|---|---|---|---|---|---|');
for (const [k, v] of peores) {
  linea(
    '| `' + k + '` | **' + v.ratio + ':1** | ' + v.minimo + ' | ' + v.veces + ' | ' + v.rutas.size + ' | ' +
    (v.texto || '').replace(/\|/g, ' ').slice(0, 32) + ' |',
  );
}
linea('');

// ---------- 4. Estructura ----------------------------------------------------
linea('## 4. Estructura por ruta (constante entre dispositivos salvo aviso)');
linea('');
linea('| Ruta | h1 | main | main anidados | aria-live | salto | img sin dimension | sin nombre |');
linea('|---|---|---|---|---|---|---|---|');
for (const id of rutas) {
  const x = buscar(disps[0], id);
  if (!x) continue;
  const variosH1 = new Set(disps.map((d) => buscar(d, id)?.encabezados.h1)).size > 1;
  linea(
    '| `' + id + '` | ' + x.encabezados.h1 + (variosH1 ? ' (varia)' : '') +
    ' | ' + x.landmarks.main +
    ' | ' + x.landmarks.mainAnidados +
    ' | ' + x.landmarks.ariaLive +
    ' | ' + (x.landmarks.enlaceDeSalto ? 'si' : 'NO') +
    ' | ' + x.controles.imagenesSinDimension + '/' + x.controles.imagenes +
    ' | ' + x.sinNombreAccesible.total + ' |',
  );
}
linea('');

// ---------- 5. Enlaces vs botones -------------------------------------------
linea('## 5. Enlaces reales frente a botones que navegan');
linea('');
linea('| Ruta | botones | enlaces | de ellos internos | role=button | role=link |');
linea('|---|---|---|---|---|---|');
for (const id of rutas) {
  const x = buscar('escritorio', id) || buscar(disps[0], id);
  if (!x) continue;
  const c = x.controles;
  linea('| `' + id + '` | ' + c.botones + ' | ' + c.enlaces + ' | **' + c.enlacesInternos + '** | ' + c.rolBoton + ' | ' + c.rolEnlace + ' |');
}
linea('');

// ---------- 6. Errores de consola -------------------------------------------
const conErrores = datos.filter((d) => d.erroresDeConsola.length);
linea('## 6. Errores de JavaScript en consola');
linea('');
if (!conErrores.length) {
  linea('Ninguno en ninguna ruta ni dispositivo.');
} else {
  for (const d of conErrores) {
    linea('- `' + d.dispositivo + '` `' + d.ruta + '`: ' + d.erroresDeConsola.join(' | '));
  }
}
linea('');

// ---------- 7. Titulos -------------------------------------------------------
linea('## 7. Titulo del documento por ruta');
linea('');
linea('| Ruta | Titulo |');
linea('|---|---|');
for (const id of rutas) {
  const x = buscar(disps[0], id);
  if (x) linea('| `' + id + '` | ' + x.titulo + ' |');
}
