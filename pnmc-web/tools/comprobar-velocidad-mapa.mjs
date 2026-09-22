#!/usr/bin/env node
/**
 * Cuánto tarda /mapa en dibujar territorios, y en qué se le va el hilo principal.
 *
 * POR QUE ESTE GUION Y NO UNA CIFRA DE LIGHTHOUSE. Lo que le importa a quien abre el
 * geovisor no es cuándo termina de descargar el HTML: es cuándo ve Colombia dibujada y
 * puede pulsar un departamento. Eso se mide contando polígonos interactivos en el DOM,
 * que es exactamente lo que el usuario tiene delante.
 *
 * LO QUE SE MIDIO, sobre el sitio COMPILADO y servido por el
 * propio API —no sobre `ng serve`, que multiplica por tres—:
 *
 *   territorios dibujados            2154 ms
 *   hilo bloqueado, en total         1953 ms en 4 tareas
 *   la tarea más larga               1588 ms
 *   dónde iba esa tarea              `_setupPainter`, 1359 ms de tiempo propio
 *
 * Aplazando el montaje de la capa base al primer hueco libre del hilo, las mismas cuatro
 * pasadas dan 803, 949, 827 y 965 ms de dibujado y 304-340 ms de tarea bloqueante. El
 * trabajo de MapLibre sigue costando lo mismo; lo que cambia es que ya no se paga con el
 * mapa en blanco.
 *
 * `_setupPainter` es la puesta en marcha del pintor WebGL de MapLibre: compilar los
 * programas de sombreado de la capa base vectorial. Se hacía ANTES de dibujar un solo
 * departamento, así que el mapa se quedaba en blanco durante todo ese rato. En un
 * navegador con GPU de verdad la misma medida daba 1583 ms y 502 ms: menos, pero seguía
 * siendo el gasto mayor de la carga.
 *
 * LA CONCURRENCIA NO ERA EL PROBLEMA, y conviene dejarlo escrito porque fue la primera
 * sospecha: disparando contra el API las siete consultas del arranque a la vez, las
 * siete responden en ~50 ms. El servidor no es el cuello de botella; el hilo principal
 * del navegador sí.
 *
 *   npm run mapa:velocidad
 *   node tools/comprobar-velocidad-mapa.mjs http://127.0.0.1:8081
 *
 * SE MIDE CONTRA EL SITIO COMPILADO. Contra `ng serve` los números no significan nada:
 * cientos de módulos sin empaquetar. El guion avisa si detecta que le han apuntado ahí.
 */

import { chromium } from 'playwright';

const BASE = process.argv[2] || 'http://127.0.0.1:8081';
const PASADAS = 4;

/**
 * La primera pasada se mide y se informa, PERO NO CUENTA PARA EL TECHO.
 *
 * Medido con navegador nuevo en cada pasada: 2454, 1391 y 1107
 * ms una vez, y 2184, 1215 y 1397 otra. La primera siempre se despega, y no porque el
 * sitio empeore: paga la lectura de disco de los paquetes y el calentamiento del
 * servidor, y las siguientes los encuentran en la memoria del sistema. Ese primer número
 * depende de qué más esté haciendo la máquina, así que como alarma de regresión no vale;
 * como retrato de la primera visita sí, y por eso se imprime.
 */
const PASADAS_QUE_CUENTAN = 1;

// Techos, fijados SOBRE LO MEDIDO DESPUES de aplazar la capa base, con holgura para el
// ruido de la máquina. No son un objetivo de rendimiento: son una alarma de regresión.
//
//   antes de aplazar   2282 · 1380 · 2426 · 1230 ms dibujado   ·  1578 · 712 · 666 · 515 bloqueando
//   después            803 · 949 · 827 · 965 ms                ·  304 · 322 · 307 · 340
//
// Los techos se ponen ~35 % por encima de la peor de las cuatro medidas buenas. Si esto
// se pone en rojo, algo volvió a bloquear el arranque; el desglose de abajo dice qué.
// -----------------------------------------------------------------------------
// LOS DOS TECHOS SE ELIGIERON CON UN MUTANTE DELANTE, Y NO SON SIMÉTRICOS.
//
// Se sembró el defecto de vuelta —montar la capa base en el acto, como antes— y se
// midieron los dos estados con cuatro pasadas cada uno:
//
//                            territorios dibujados        tarea bloqueante más larga
//   aplazado (correcto)       803 · 949 · 827 · 965        304 · 322 · 307 · 340
//                            1450 · 1081 · 1506 · 1106     299 · 312 · 318 · 343
//   en el acto (mutante)     2162 · 1061 · 1309 · 1329     1423 · 454 · 620 · 494
//                            (otra corrida: 2066)          (otra corrida: 565)
//
// LA CIFRA DE DIBUJADO NO SEPARA LOS DOS ESTADOS. Los rangos se solapan de sobra: una
// pasada correcta llegó a 1506 ms y una defectuosa bajó a 1061. Depende de lo que esté
// haciendo la máquina, no del código. Se sigue imprimiendo —es el número que describe la
// experiencia real— pero como guardia solo vale para una regresión catastrófica.
//
// LA TAREA BLOQUEANTE SÍ SEPARA: 299-343 ms correcto contra 454-1423 defectuoso. El
// techo va en 400, por encima de todo lo bueno y por debajo de todo lo malo. Con el
// primer techo que se puso —500— el mutante SOBREVIVIÓ una vez por seis milisegundos, y
// murió a la siguiente: un guardia que acierta la mitad de las veces no es un guardia.
// -----------------------------------------------------------------------------
const TECHO_DIBUJADO_MS = 2600;
const TECHO_TAREA_MAS_LARGA_MS = 400;

/**
 * Una pasada: NAVEGADOR NUEVO, y eso no es una precaucion, es la medida.
 *
 * La primera version reutilizaba el navegador entre pasadas y daba 2126, 984 y 809 ms.
 * No es que el sitio se hiciera mas rapido: es que Chromium guarda los programas de
 * sombreado ya compilados y la segunda vez no los compila. Con la mediana de esas tres
 * cifras, el techo lo pasaba cualquier cosa.
 *
 * Lo que hay que medir es la PRIMERA visita, que es la unica que paga la compilacion y
 * la descarga. Un navegador por pasada la reproduce entera, cada vez.
 */
async function medir() {
  const navegador = await chromium.launch({
    args: ['--use-gl=swiftshader', '--enable-unsafe-swiftshader'],
  });
  const pagina = await navegador.newPage({ viewport: { width: 1280, height: 639 } });

  // Se instala antes de que cargue nada: las tareas largas del arranque son justo las
  // que interesan, y un observador tardío no las ve.
  await pagina.addInitScript(() => {
    window.__tareasLargas = [];
    try {
      new PerformanceObserver((lista) => {
        for (const e of lista.getEntries()) {
          window.__tareasLargas.push({ inicio: Math.round(e.startTime), dura: Math.round(e.duration) });
        }
      }).observe({ entryTypes: ['longtask'] });
    } catch { /* navegador sin longtask */ }
  });

  const errores = [];
  pagina.on('pageerror', (e) => errores.push(e.message.slice(0, 140)));

  const t0 = Date.now();
  await pagina.goto(`${BASE}/mapa`, { waitUntil: 'commit', timeout: 120000 });

  let dibujado = null;
  while (Date.now() - t0 < 25000) {
    const listo = await pagina
      .evaluate(() => document.querySelectorAll('.leaflet-interactive').length > 10)
      .catch(() => false);
    if (listo) { dibujado = Date.now() - t0; break; }
    await pagina.waitForTimeout(50);
  }

  // Se deja un margen para que la capa base termine de montarse: si el aplazamiento
  // rompiera algo, se vería aquí y no después.
  await pagina.waitForTimeout(4000);

  const estado = await pagina.evaluate(() => ({
    tareas: window.__tareasLargas || [],
    territorios: document.querySelectorAll('.leaflet-interactive').length,
    lienzoGl: document.querySelectorAll('.leaflet-gl-layer canvas, canvas.maplibregl-canvas').length,
    rotulos: document.querySelectorAll('.department-label').length,
    atribucion: (document.querySelector('.leaflet-control-attribution')?.textContent || '').includes('OpenFreeMap'),
  }));

  await navegador.close();
  return { dibujado, errores, ...estado };
}

const pasadas = [];
for (let i = 0; i < PASADAS; i += 1) pasadas.push(await medir());

const mediana = (xs) => [...xs].sort((a, b) => a - b)[Math.floor(xs.length / 2)];
const dibujados = pasadas.map((p) => p.dibujado).filter((x) => x !== null);
/**
 * SOLO CUENTAN LAS TAREAS QUE BLOQUEAN ANTES DEL PRIMER DIBUJO.
 *
 * Desde que la capa base vectorial se monta aplazada, su tarea de ~1.400 ms sigue
 * existiendo —compilar sombreadores cuesta lo que cuesta—, pero ocurre DESPUES de que
 * los territorios estan en pantalla. Contarla aqui volveria a dar rojo por lo mismo que
 * se acaba de arreglar, y mediria justo lo contrario de lo que interesa: lo que retrasa
 * al usuario es lo que pasa ANTES de ver el mapa.
 */
const bloqueanteMasLarga = (p) =>
  Math.max(0, ...p.tareas.filter((t) => t.inicio < (p.dibujado ?? 0)).map((t) => t.dura));

const masLargas = pasadas.map(bloqueanteMasLarga);
const bloqueos = pasadas.map((p) =>
  p.tareas.filter((t) => t.inicio < (p.dibujado ?? 0)).reduce((s, t) => s + t.dura, 0));

const estables = pasadas.slice(PASADAS_QUE_CUENTAN);
const dibujadosEstables = estables.map((p) => p.dibujado).filter((x) => x !== null);
const masLargasEstables = estables.map(bloqueanteMasLarga);

const ultima = pasadas[pasadas.length - 1];

console.log('');
console.log(`  sitio:                          ${BASE}/mapa`);
console.log(`  pasadas:                        ${PASADAS}`);
console.log(`  territorios dibujados:          ${dibujados.join(' · ')} ms   (mediana sin la 1.ª: ${mediana(dibujadosEstables)})`);
console.log(`  tarea más larga ANTES de pintar: ${masLargas.join(' · ')} ms   (mediana sin la 1.ª: ${mediana(masLargasEstables)})`);
console.log(`  hilo bloqueado antes de pintar: ${bloqueos.join(' · ')} ms`);
console.log('');
console.log(`  polígonos interactivos:         ${ultima.territorios}`);
console.log(`  rótulos de departamento:        ${ultima.rotulos}`);
console.log(`  lienzos WebGL de la capa base:  ${ultima.lienzoGl}`);
console.log(`  atribución de OpenFreeMap:      ${ultima.atribucion ? 'presente' : 'AUSENTE'}`);
console.log(`  errores de consola:             ${ultima.errores.length}`);
ultima.errores.slice(0, 2).forEach((e) => console.log('      ' + e));
console.log('');

const fallos = [];

if (dibujados.length < PASADAS) {
  fallos.push('Alguna pasada no llegó a dibujar territorios en 25 s.');
}
if (mediana(dibujadosEstables) > TECHO_DIBUJADO_MS) {
  fallos.push(
    `Los territorios tardan ${mediana(dibujadosEstables)} ms en aparecer y el techo es ${TECHO_DIBUJADO_MS} ms.\n` +
      '  Este techo es de catástrofe, no de rendimiento: la cifra de dibujado se mueve con la\n' +
      '  máquina y no separa un código bueno de uno malo. El que sí lo hace es el de la tarea\n' +
      '  bloqueante. Si solo salta este, sospecha de la máquina antes que del código.',
  );
}
if (mediana(masLargasEstables) > TECHO_TAREA_MAS_LARGA_MS) {
  fallos.push(
    `La tarea más larga del hilo dura ${mediana(masLargasEstables)} ms y el techo es ${TECHO_TAREA_MAS_LARGA_MS} ms.\n` +
      '  Con el hilo bloqueado la página no responde a nada: ni a un clic ni a un desplazamiento.\n' +
      '  El sospechoso conocido es `_setupPainter` de MapLibre, que compila los sombreadores\n' +
      '  de la capa base vectorial. Tiene que montarse DESPUÉS del primer dibujo, no antes.',
  );
}
// LA CAPA BASE TIENE QUE SEGUIR APARECIENDO. Aplazarla es ganar tiempo; perderla es
// cambiar velocidad por cartografía, que no es lo que se pidió, y la licencia de
// OpenFreeMap exige además que su atribución esté a la vista.
if (ultima.lienzoGl === 0) {
  fallos.push(
    'La capa base vectorial no llegó a montarse en los 4 s posteriores al dibujo.\n' +
      '  Aplazarla es ganar tiempo; perderla es quedarse sin cartografía de fondo.',
  );
}
if (!ultima.atribucion) {
  fallos.push('Falta la atribución de OpenFreeMap, que su licencia exige visible.');
}
if (ultima.territorios < 30) {
  fallos.push(`Solo hay ${ultima.territorios} polígonos interactivos; deberían ser los 33 departamentos.`);
}
if (ultima.errores.length) {
  fallos.push(`Hay ${ultima.errores.length} error(es) en consola: ${ultima.errores[0]}`);
}

if (fallos.length) {
  for (const f of fallos) console.error('✗ ' + f);
  console.error('');
  process.exitCode = 1;
} else {
  console.log('✓ El mapa dibuja pronto, sin bloquear el hilo, y la capa base llega detrás.');
  console.log('');
}
