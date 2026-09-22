/**
 * ¿Anuncia la consola alguna sección a la que no se pueda llegar?
 *
 * ES LA MISMA CLASE DE DEFECTO QUE `claves-sin-lector.mjs`, un piso más arriba: allí una clave
 * declarada que ninguna página lee; aquí una sección declarada que ningún panel pinta. Las dos
 * se descubren igual de tarde, porque ninguna falla: la barra lateral ofrece la entrada, alguien
 * la pulsa y el armazón contesta «Ruta administrativa inválida», que se lee como un fallo del
 * sistema y no como lo que es.
 *
 * PASO DE VERDAD. El rediseño de la consola declaró quince secciones en cinco familias y cuatro
 * no tenían nada detrás. Tres eran una decisión de producto —Agenda, Noticias y Galería se dejan
 * a la vista porque se van a construir— y la cuarta, «Calidad y coincidencias», sí tenía panel:
 * solo faltaba engancharlo. Esta herramienta distingue los dos casos.
 *
 * LO QUE MIDE, y lo que no: que cada sección tenga una rama que la pinte. No comprueba que esa
 * rama traiga datos; eso lo ve el barrido con navegador. Esta es la puerta barata que cabe en
 * cada cambio.
 *
 *   node tools/secciones-sin-panel.mjs
 */
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const RAIZ = join(dirname(fileURLToPath(import.meta.url)), '..');
const NAVEGACION = join(RAIZ, 'src/app/features/admin/domain/navegacion-administrativa.ts');
const ARMAZON = join(RAIZ, 'src/app/features/admin/admin-shell-page/admin-shell-page.component.html');

const navegacion = readFileSync(NAVEGACION, 'utf-8');
const armazon = readFileSync(ARMAZON, 'utf-8');

/** Cada sección con su id, su título y si está declarada como futura. */
const secciones = [...navegacion.matchAll(/id:\s*'([a-z-]+)',\s*\n\s*ruta:\s*'([^']*)',/g)]
  .map(coincidencia => {
    // El bloque de una sección termina en su llave de cierre: se corta ahí para que
    // `estadoDesarrollo` de la siguiente no se le atribuya a esta.
    const bloque = navegacion.slice(coincidencia.index).split(/\n\s{6}\},/)[0];
    const titulo = /titulo:\s*'([^']+)'/.exec(bloque);
    return {
      id: coincidencia[1],
      titulo: titulo ? titulo[1] : coincidencia[1],
      futura: /estadoDesarrollo:\s*'proximamente'/.test(bloque),
    };
  });

if (secciones.length === 0) {
  console.error('No se reconoció ninguna sección: el formato de la navegación cambió.');
  process.exit(1);
}

const pinta = id => armazon.includes(`activeSection() === '${id}'`);
const recogeFuturas = armazon.includes('seccionEnPreparacion()');

const sinPanel = secciones.filter(s => !s.futura && !pinta(s.id));
const futuras = secciones.filter(s => s.futura);

console.log(`SECCIONES DE LA CONSOLA — ${secciones.length} declaradas`);
console.log(`  con panel propio:   ${secciones.filter(s => pinta(s.id)).length}`);
console.log(`  en preparación:     ${futuras.length}${futuras.length ? ` (${futuras.map(s => s.titulo).join(', ')})` : ''}`);

if (futuras.length > 0 && !recogeFuturas) {
  console.error('\nROJO: hay secciones en preparación y el armazón no las recoge por su estado.');
  console.error('      Caerían en el comodín de «Ruta administrativa inválida».');
  process.exit(1);
}

if (sinPanel.length > 0) {
  console.error('\nROJO: estas secciones se anuncian y no tienen panel ni estado declarado:');
  for (const s of sinPanel) console.error(`  - ${s.id} (${s.titulo})`);
  console.error('\nO se les engancha su panel, o se marcan `estadoDesarrollo: \'proximamente\'`.');
  process.exit(1);
}

console.log('\nOK: ninguna sección anunciada lleva a un error.');
