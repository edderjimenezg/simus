#!/usr/bin/env node
/**
 * Comprueba que el directorio del panel derecho es un ATAJO DEL MAPA, y no una puerta
 * al modal que tapaba el mapa.
 *
 * LO QUE HACIA. Cada tarjeta del directorio ejecutaba `selectedRecordDetail.set(item)`,
 * que abre un dialogo `fixed inset-0` con velo y `backdrop-blur`. Medido con este mismo
 * guion contra el codigo anterior, a 1280x639: al pulsar la primera tarjeta aparecia un
 * dialogo de 1280x639 con el texto «Detalle de Redes de Documentacion», y el mapa
 * quedaba entero detras del velo. El directorio esta pegado al mapa, en la columna de
 * al lado: usarlo para tapar el mapa gasta la pantalla en negarse a si misma.
 *
 * LO QUE SE COMPRUEBA AQUI, y por que hace falta un navegador para ello:
 *
 *   1. Que NO se abre el modal a pantalla completa.
 *   2. Que se abre la tarjeta del municipio, y que su titulo es el municipio que el API
 *      da para ese registro. No «un municipio»: el suyo.
 *   3. Que el registro pulsado sale marcado dentro de la tarjeta, y que es EL UNICO
 *      marcado (`article[data-resaltado="si"]`). Sin lo primero, pulsar uno de los cinco
 *      procesos de un municipio abre una lista de cinco sin decir cual era. Sin lo
 *      segundo se marcan los cinco, que es lo mismo pero peor, y es lo que pasaba: el
 *      `id` del API se repite entre capas y «1» nombra un festival, una escuela, un
 *      mercado, una red y un lutier.
 *   4. Que el mapa dibuja el ROTULO de ese municipio. Ese rotulo solo aparece por
 *      encima del zoom 7 y solo con el departamento abierto: verlo prueba de una vez
 *      que la camara bajo al municipio y que el departamento se desplego.
 *   5. A cuantos pixeles del centro del lienzo quedo ese rotulo. Estar en el departamento
 *      correcto no es estar en el municipio; esto ultimo es lo que se pidio.
 *   6. Que lo unico que tapa el mapa es la propia tarjeta, y no un velo.
 *
 * Nada de esto lo puede ver una prueba unitaria: `resolverDestinoDeNavegacion` tiene
 * diez pruebas de dominio y todas pasan aunque la plantilla siga llamando al modal.
 *
 *   npm run mapa:atajo
 *   node tools/comprobar-atajo-directorio.mjs http://127.0.0.1:4300 http://127.0.0.1:8180
 *
 * Necesita el sitio levantado Y el API accesible: el municipio de cada registro se
 * comprueba contra el API, no contra lo que la propia pantalla dice de si misma.
 */

import { chromium } from 'playwright';

const BASE = process.argv[2] || 'http://127.0.0.1:4300';
const API = process.argv[3] || 'http://127.0.0.1:8081';

const MODULOS = [
  '/api/v1/festivals',
  '/api/v1/music-schools',
  '/api/v1/music-markets',
  '/api/v1/redes-documentacion',
  '/api/v1/lutieres',
];

/** Los 155 registros publicados, indexados por nombre. */
async function registrosDelApi() {
  const porNombre = new Map();
  for (const ruta of MODULOS) {
    const r = await fetch(`${API}${ruta}?limit=500&offset=0`);
    if (!r.ok) throw new Error(`El API respondió ${r.status} a ${ruta}`);
    const pagina = await r.json();
    for (const fila of pagina.items || []) {
      porNombre.set(String(fila.name || '').trim(), {
        municipio: String(fila.municipalityName || '').trim(),
        codigo: String(fila.municipalityCode || '').trim(),
        departamento: String(fila.departmentName || '').trim(),
      });
    }
  }
  return porNombre;
}

const registros = await registrosDelApi();

const navegador = await chromium.launch({
  args: ['--use-gl=swiftshader', '--enable-unsafe-swiftshader'],
});
const pagina = await navegador.newPage({ viewport: { width: 1280, height: 639 } });

const errores = [];
pagina.on('pageerror', (e) => errores.push(e.message.slice(0, 160)));
pagina.on('console', (m) => {
  if (m.type() === 'error' && !m.text().includes('401')) errores.push(m.text().slice(0, 160));
});

// Se anota cada peticion a la tabla de puntos municipales. SIN ESTO, UN FALLO DE ESTA
// COMPROBACION NO SE PUEDE DIAGNOSTICAR: si la tabla no llega, el atajo abre la tarjeta
// pero deja la camara en el encuadre del departamento —que es lo que esta disenado que
// haga— y el sintoma es identico al de un atajo roto. Con este registro se distingue en
// una linea.
const peticionesDivipola = [];
pagina.on('response', (r) => {
  if (r.url().includes('/divipola/locations')) {
    peticionesDivipola.push(`${r.status()}@${new URL(r.url()).searchParams.get('offset')}`);
  }
});

await pagina.goto(`${BASE}/mapa`, { waitUntil: 'networkidle', timeout: 120000 });
await pagina.waitForTimeout(11000);
try {
  await pagina.locator('[aria-label*="errar"], .tutorial-close').first().click({ timeout: 4000 });
} catch {
  await pagina.keyboard.press('Escape');
}
await pagina.waitForTimeout(1500);

/**
 * Cuantos de 240 puntos repartidos por el lienzo del mapa se ven de verdad.
 *
 * SE PREGUNTA POR `elementFromPoint`, Y NO POR LA CAJA DEL LIENZO. La primera version
 * media el rectangulo del contenedor y lo daba por tapado si existia algun `div` fijo
 * del tamano de la pantalla. Devolvia CERO tanto antes como despues del clic —medido el
 * 29 de agosto de 2026: «antes 0, despues 0»— porque la propia armazon de la pagina
 * tiene un contenedor fijo a pantalla completa. Con las dos medidas en cero la
 * comprobacion pasaba sin comparar nada: verde por no mirar.
 *
 * Preguntando quien esta ENCIMA de cada punto, un velo modal se cuenta como lo que es y
 * un contenedor transparente no.
 */
const puntosVisiblesDelMapa = () =>
  pagina.evaluate(() => {
    const lienzo = document.querySelector('#mapa-workspace');
    if (!lienzo) return { vistos: 0, tapadosPorLaTarjeta: 0, tapadosPorOtraCosa: 0 };
    const tarjeta = document.querySelector('section[role="dialog"][aria-label^="Procesos registrados"]');
    const caja = lienzo.getBoundingClientRect();
    let vistos = 0;
    let tapadosPorLaTarjeta = 0;
    let tapadosPorOtraCosa = 0;

    for (let fila = 1; fila <= 12; fila += 1) {
      for (let columna = 1; columna <= 20; columna += 1) {
        const x = caja.left + (caja.width * columna) / 21;
        const y = caja.top + (caja.height * fila) / 13;
        if (x < 0 || y < 0 || x > window.innerWidth || y > window.innerHeight) continue;
        const encima = document.elementFromPoint(x, y);
        if (encima && lienzo.contains(encima)) vistos += 1;
        else if (encima && tarjeta?.contains(encima)) tapadosPorLaTarjeta += 1;
        else tapadosPorOtraCosa += 1;
      }
    }
    return { vistos, tapadosPorLaTarjeta, tapadosPorOtraCosa };
  });

const puntosAntes = await puntosVisiblesDelMapa();

const primera = pagina.locator('#panel-directorio button:has(h4)').first();
const nombreDelRegistro = (await primera.locator('h4').textContent())?.trim() || '';
const esperado = registros.get(nombreDelRegistro);

await primera.click();

// -----------------------------------------------------------------------------
// SONDA DEL HILO PRINCIPAL. Va antes que nada porque un hilo bloqueado deja colgado
// todo lo de abajo: la primera version de este atajo usaba `map.flyTo(...)` y la
// pagina dejaba de responder para siempre a partir del segundo siguiente al clic.
// Medido con las dos capas base y en ventana de verdad con
// GPU. Sin esta sonda, el guion se quedaba sin salida y no habia forma de saber por
// que. Se pregunta algo trivial y se le da cinco segundos.
// -----------------------------------------------------------------------------
const sondas = [];
for (let i = 0; i < 4; i += 1) {
  try {
    sondas.push(
      await Promise.race([
        pagina.evaluate(() => document.querySelectorAll('button').length),
        new Promise((_, rechazar) => setTimeout(() => rechazar(new Error('sin respuesta')), 5000)),
      ]),
    );
  } catch {
    sondas.push(null);
  }
  await pagina.waitForTimeout(2000);
}
const hiloVivo = sondas.every((s) => s !== null);

if (hiloVivo) await pagina.waitForTimeout(6000);

if (!hiloVivo) {
  console.error('');
  console.error('✗ La página dejó de responder después del clic.');
  console.error(`  Sondas al hilo principal: ${JSON.stringify(sondas)} — null es «sin respuesta en 5 s».`);
  console.error('  Es lo que hacía «map.flyTo(...)». Si vuelve a aparecer, el atajo congela el geovisor.');
  console.error('');
  process.exitCode = 1;
  await navegador.close();
  process.exit(1);
}

/**
 * Se espera a que el municipio quede centrado, y se anota CUANTO TARDO.
 *
 * NO SE MIRA UNA SOLA VEZ, y esa fue una leccion cara. El viaje pasa por dos estados:
 * primero el mapa encuadra el DEPARTAMENTO —que es lo que se puede hacer sin saber
 * todavia donde cae el municipio— y despues, cuando llegan los puntos de
 * `/api/v1/divipola/locations`, salta al MUNICIPIO. Medido
 * siguiendo el rotulo de ABEJORRAL cada 400 ms: a los 400 ms estaba a 124 px del centro
 * del lienzo y a los 800 ms a 9 px, donde se quedo.
 *
 * Una sola foto daba rojo o verde segun cuanto hubiera tardado la red ese dia. Lo que
 * hay que comprobar no es el instante sino que LLEGA, y en cuanto tiempo.
 */
const TOPE_DE_ESPERA_MS = 20000;
const leerCentrado = () =>
  pagina.evaluate((municipio) => {
    const lienzo = document.querySelector('#mapa-workspace')?.getBoundingClientRect();
    const rotulo = [...document.querySelectorAll('.municipality-label')].find(
      (n) => (n.textContent || '').trim().toUpperCase() === municipio,
    );
    if (!lienzo || !rotulo) return null;
    const c = rotulo.getBoundingClientRect();
    return Math.round(
      Math.hypot(
        c.left + c.width / 2 - (lienzo.left + lienzo.width / 2),
        c.top + c.height / 2 - (lienzo.top + lienzo.height / 2),
      ),
    );
  }, (esperado?.municipio || '').toUpperCase());

const toleranciaDeCentrado = await pagina.evaluate(() => {
  const l = document.querySelector('#mapa-workspace')?.getBoundingClientRect();
  // Un cuarto del lado menor del lienzo. Con 415 px de alto son ~104 px: holgado para el
  // desfase entre el punto de referencia del municipio y el centro de su poligono, y
  // estrecho para no dar por bueno el encuadre del departamento entero, que el 29 de
  // agosto de 2026 dejaba el rotulo a 124 px.
  return l ? Math.round(Math.min(l.width, l.height) / 4) : 0;
});

const arranque = Date.now();
let distanciaAlCentro = await leerCentrado();
while (
  Date.now() - arranque < TOPE_DE_ESPERA_MS
  && !(distanciaAlCentro !== null && distanciaAlCentro <= toleranciaDeCentrado)
) {
  await pagina.waitForTimeout(500);
  distanciaAlCentro = await leerCentrado();
}
const tardanzaEnCentrar = Date.now() - arranque;
const enElCentro = distanciaAlCentro !== null && distanciaAlCentro <= toleranciaDeCentrado;

const puntosDespues = await puntosVisiblesDelMapa();

const medida = await pagina.evaluate(() => {
  const tarjeta = document.querySelector('section[role="dialog"][aria-label^="Procesos registrados"]');
  // SE COGEN TODOS LOS MARCADOS, no el primero. Con `querySelector` a secas este guion
  // dio verde mientras la tarjeta marcaba CINCO procesos de los
  // cinco que hay en ABEJORRAL: el primero era el correcto y el resto no se miraba. Se
  // vio en una captura de pantalla, no en la comprobacion.
  const marcados = [...(tarjeta?.querySelectorAll('article[data-resaltado="si"]') || [])];
  const lienzo = document.querySelector('#mapa-workspace')?.getBoundingClientRect();

  const rotulos = [...document.querySelectorAll('.municipality-label')].map((n) => {
    const c = n.getBoundingClientRect();
    return { texto: (n.textContent || '').trim(), x: c.left + c.width / 2, y: c.top + c.height / 2 };
  });

  return {
    hayModalDeFicha: [...document.querySelectorAll('div')].some((d) => {
      const e = getComputedStyle(d);
      const c = d.getBoundingClientRect();
      return e.position === 'fixed'
        && c.width >= window.innerWidth * 0.9
        && /Detalle de /.test(d.textContent || '');
    }),
    hayTarjeta: Boolean(tarjeta),
    tituloDeLaTarjeta: tarjeta?.querySelector('h2')?.textContent?.trim() || '',
    procesosEnLaTarjeta: tarjeta?.querySelectorAll('article').length || 0,
    cuantosMarcados: marcados.length,
    nombresMarcados: marcados.map((a) => a.querySelector('h3')?.textContent?.trim() || ''),
    nombreResaltado: marcados[0]?.querySelector('h3')?.textContent?.trim() || '',
    rotulos: rotulos.map((r) => r.texto),
    lienzo: lienzo ? { x: lienzo.left, y: lienzo.top, w: lienzo.width, h: lienzo.height } : null,
    posicionDelRotulo: rotulos.length ? rotulos : [],
  };
});

/**
 * A cuántos píxeles del centro del lienzo quedó el rótulo del municipio.
 *
 * SE DA EL NÚMERO Y NO UN SÍ/NO. Un «no está centrado» no distingue entre quedarse a
 * treinta píxeles y no haberse movido del encuadre del departamento, que son un ajuste
 * y un defecto. El 29 de agosto de 2026, con el viaje bien hecho, la medida fue de 8 px
 * sobre un lienzo de 608x415.
 */
console.log('');
console.log(`  sitio:                                ${BASE}/mapa`);
console.log(`  registro pulsado:                     ${nombreDelRegistro}`);
console.log(`  su municipio según el API:            ${esperado ? `${esperado.municipio} (${esperado.codigo}, ${esperado.departamento})` : 'NO ESTÁ EN EL API'}`);
console.log(`  modal «Detalle de» a pantalla completa: ${medida.hayModalDeFicha ? 'SE ABRIÓ' : 'no se abrió'}`);
console.log(`  tarjeta del municipio:                ${medida.hayTarjeta ? `«${medida.tituloDeLaTarjeta}», ${medida.procesosEnLaTarjeta} proceso(s)` : 'NO SE ABRIÓ'}`);
console.log(`  registros marcados dentro de ella:    ${medida.cuantosMarcados} — ${medida.nombresMarcados.join(' | ') || '(ninguno)'}`);
console.log(`  rótulos municipales dibujados:        ${medida.rotulos.length}`);
console.log(`  rótulo del municipio, al centro:      ${distanciaAlCentro === null ? 'no se dibujó' : `${distanciaAlCentro} px (tolerancia ${toleranciaDeCentrado})`}`);
console.log(`  tardó en centrarse:                   ${enElCentro ? `${tardanzaEnCentrar} ms` : `no se centró en ${TOPE_DE_ESPERA_MS} ms`}`);
console.log(`  puntos del mapa visibles (de 240):    antes ${puntosAntes.vistos}  después ${puntosDespues.vistos}`);
console.log(`  de los tapados, cuántos son la tarjeta: ${puntosDespues.tapadosPorLaTarjeta} de ${puntosDespues.tapadosPorLaTarjeta + puntosDespues.tapadosPorOtraCosa}`);
console.log(`  el hilo principal responde tras el clic: ${sondas.map((x) => (x === null ? 'NO' : 'sí')).join(' ')}`);
console.log(`  peticiones a divipola/locations:      ${peticionesDivipola.join(' ') || '(ninguna)'}`);
console.log(`  errores de consola:                   ${errores.length}`);
errores.slice(0, 3).forEach((e) => console.log('      ' + e));
console.log('');

const fallos = [];

if (!nombreDelRegistro) {
  fallos.push('No hay ni una tarjeta en el directorio: no se pudo pulsar nada.');
}
if (!esperado) {
  fallos.push(
    `«${nombreDelRegistro}» no está en el API.\n` +
      '  Sin su municipio de referencia no hay contra qué comprobar el viaje.',
  );
}
if (medida.hayModalDeFicha) {
  fallos.push(
    'Volvió el modal a pantalla completa: el directorio tapa el mapa en vez de llevar a él.\n' +
      '  Es el defecto que este guion cierra.',
  );
}
if (!medida.hayTarjeta) {
  fallos.push('No se abrió la tarjeta del municipio, que es «la pestaña que se usa dentro del mapa para ubicar».');
} else if (esperado && medida.tituloDeLaTarjeta.toUpperCase() !== esperado.municipio.toUpperCase()) {
  fallos.push(
    `La tarjeta dice «${medida.tituloDeLaTarjeta}» y el API sitúa el registro en «${esperado.municipio}».`,
  );
}
if (medida.hayTarjeta && medida.cuantosMarcados === 0) {
  fallos.push(
    'La tarjeta no marca el registro desde el que se llegó.\n' +
      '  Con varios procesos en el mismo municipio, el atajo no dice a cuál llevó.',
  );
} else if (medida.cuantosMarcados > 1) {
  fallos.push(
    `La tarjeta marca ${medida.cuantosMarcados} procesos a la vez: ${medida.nombresMarcados.join(' | ')}.\n` +
      '  Una marca que señala a todos no señala a ninguno. Pasó porque el `id` del API se\n' +
      '  repite entre capas: «1» nombra un festival, una escuela, un mercado, una red y un lutier.',
  );
} else if (medida.cuantosMarcados === 1 && medida.nombreResaltado !== nombreDelRegistro) {
  fallos.push(`Se pulsó «${nombreDelRegistro}» y la tarjeta marca «${medida.nombreResaltado}».`);
}
if (esperado && !medida.rotulos.some((r) => r.toUpperCase() === esperado.municipio.toUpperCase())) {
  fallos.push(
    `El mapa no dibuja el rótulo de «${esperado.municipio}».\n` +
      '  Ese rótulo solo sale con el departamento abierto y por encima del zoom 7:\n' +
      '  sin él, la cámara no llegó al municipio.',
  );
} else if (!enElCentro) {
  fallos.push(
    `«${esperado?.municipio}» está dibujado, pero a ${distanciaAlCentro} px del centro del lienzo\n` +
      `  y la tolerancia es ${toleranciaDeCentrado} px. Estar en el departamento correcto no es\n` +
      `  estar en el municipio. Peticiones a divipola/locations: ${peticionesDivipola.join(' ') || 'NINGUNA'}.\n` +
      '  Si ahí no hay tres respuestas 200, la cámara se quedó donde debía: sin la tabla de\n' +
      '  puntos no se sabe dónde cae el municipio, y el atajo solo abre el departamento.',
  );
}
// La medida tiene que valer ANTES de comparar con ella. Con «antes» en cero, cualquier
// «después» pasa: es el falso verde que este guion ya tuvo una vez.
if (puntosAntes.vistos < 120) {
  fallos.push(
    `Antes del clic solo se veían ${puntosAntes.vistos} de 240 puntos del mapa.\n` +
      '  Con esa medida de partida, comparar el después no dice nada.',
  );
}
// LO QUE SE EXIGE NO ES UN PORCENTAJE, SINO QUIÉN TAPA. La tarjeta del municipio ocupa
// 232x286 px sobre un lienzo de 608x415: 26 % del mapa, medido.
// Ese porcentaje es el diseño, no un defecto. Lo que no puede pasar es que tape otra
// cosa —un velo, un modal—, y eso es lo que se cuenta aquí.
if (puntosDespues.tapadosPorOtraCosa > 0) {
  fallos.push(
    `${puntosDespues.tapadosPorOtraCosa} puntos del mapa están tapados por algo que no es la\n` +
      '  tarjeta del municipio. El atajo tiene que llevar al mapa, no taparlo.',
  );
}
if (errores.length) {
  fallos.push(`Hay ${errores.length} error(es) en consola: ${errores[0]}`);
}

if (fallos.length) {
  for (const f of fallos) console.error('✗ ' + f);
  console.error('');
  process.exitCode = 1;
} else {
  console.log('✓ El directorio lleva al mapa: encuadra el municipio, abre su tarjeta y marca el registro pulsado.');
  console.log('');
}

await navegador.close();
