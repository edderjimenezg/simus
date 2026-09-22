#!/usr/bin/env node
/**
 * PRUEBAS DEL TRINQUETE DE INTERFAZ
 * =================================
 *
 * Una puerta que no puede fallar no es una puerta. Este fichero existe para
 * demostrar, en cada corrida y no una sola vez en la consola de alguien, que el
 * trinquete de interfaz RECHAZA de verdad.
 *
 * Lo que se comprueba, y por que cada cosa:
 *
 *   1  SEMANTICA DEL CONTADOR, caso a caso, sobre plantillas de juguete. Es la
 *      parte que mas importa: dos de las siete metricas ya cambiaron de
 *      definicion una vez porque la primera version producia falsos positivos.
 *      Cada caso de aqui es una de esas discusiones, congelada.
 *   2  LA PUERTA MUERDE: con el techo una unidad por debajo, sale 1. Se prueba
 *      en los DOS sentidos, porque un minimo mal implementado pasa siempre.
 *   3  Con el techo exacto sale 0. El trinquete no es un impuesto: solo castiga
 *      empeorar.
 *   4  GIRA EN UN SOLO SENTIDO: --sellar con una metrica en rojo se niega y sale
 *      1, sin tocar el fichero de techo.
 *   5  --sellar con holgura SI aprieta (sobre una copia, jamas sobre el techo
 *      real).
 *   6  FALLA CERRADO: sin fichero de techo, con techo corrupto, con una metrica
 *      sin declarar, o con un barrido que no encuentra ni un .html, sale 1. El
 *      modo "paso porque no midio nada" es el defecto que este repositorio ya se
 *      comio una vez.
 *   7  El src REAL esta en verde, y un solo defecto inyectado lo pone en rojo.
 *   8  El techo real no se ha movido durante las pruebas.
 *
 * USO:
 *   node trinquete/trinquete-interfaz.prueba.mjs
 */

import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const AQUI = path.dirname(fileURLToPath(import.meta.url));
const HERRAMIENTA = path.join(AQUI, "trinquete-interfaz.mjs");
const TECHO_REAL = path.join(AQUI, "techo-interfaz.json");
const SRC_REAL = path.resolve(AQUI, "..", "src");

const huellaTechoAntes = fs.readFileSync(TECHO_REAL, "utf8");

let pasadas = 0;
let fallos = 0;

function comprobar(titulo, condicion, detalle = "") {
  if (condicion) {
    pasadas++;
    console.log(`   ok   ${titulo}`);
  } else {
    fallos++;
    console.log(`   FALLA ${titulo}`);
    if (detalle) console.log(`        ${String(detalle).split("\n").slice(0, 6).join("\n        ")}`);
  }
}

/** Ejecuta la herramienta y devuelve { codigo, salida }. */
function correr(args) {
  const r = spawnSync(process.execPath, [HERRAMIENTA, ...args], { encoding: "utf8" });
  return { codigo: r.status, salida: (r.stdout || "") + (r.stderr || "") };
}

/** Monta un directorio temporal con las plantillas dadas y un techo a medida. */
function banco(plantillas, metricas) {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), "trinquete-interfaz-"));
  const src = path.join(dir, "src");
  fs.mkdirSync(src, { recursive: true });
  for (const [nombre, contenido] of Object.entries(plantillas)) {
    const destino = path.join(src, nombre);
    fs.mkdirSync(path.dirname(destino), { recursive: true });
    fs.writeFileSync(destino, contenido, "utf8");
  }
  const techo = path.join(dir, "techo.json");
  fs.writeFileSync(techo, JSON.stringify({ metricas }, null, 2), "utf8");
  return { dir, src, techo };
}

/** Mide una sola metrica sobre unas plantillas, via --json. */
function medirClave(plantillas, clave) {
  const todas = {
    botones_de_accion_sin_tipo: 999999,
    imagenes_sin_dimension: 999999,
    colores_literales: 999999,
    pildoras_de_estado: 999999,
    formatos_de_fecha_sueltos: 999999,
    enlaces_de_router: 0,
    dialogos_con_rol: 0,
    anuncios_accesibles: 0,
    tamanos_fuera_de_escala: 999999,
    ventanas_sin_directiva: 999999,
    campos_sin_rotulo: 999999,
    rotulos_de_campo_a_mano: 999999,
  };
  const b = banco(plantillas, todas);
  try {
    const r = correr(["--raiz", b.src, "--techo", b.techo, "--json"]);
    const j = JSON.parse(r.salida);
    return j.filas.find((f) => f.clave === clave).valor;
  } finally {
    fs.rmSync(b.dir, { recursive: true, force: true });
  }
}

// ---------------------------------------------------------------- 1) semantica
console.log("\n1) Semantica del contador");

comprobar(
  "una ventana modal sin la directiva cuenta",
  medirClave(
    { "a.html": '<div role="dialog" aria-modal="true" aria-label="Algo">…</div>' },
    "ventanas_sin_directiva",
  ) === 1,
);

comprobar(
  "la misma con appDialogo NO cuenta",
  medirClave(
    { "a.html": '<div role="dialog" aria-modal="true" appDialogo (cerrar)="x()">…</div>' },
    "ventanas_sin_directiva",
  ) === 0,
);

comprobar(
  "un dialogo NO MODAL no cuenta: la directiva le sobra, y encerraria el foco en un globo",
  medirClave(
    { "a.html": '<div role="dialog" aria-label="Ayuda sobre la practica">…</div>' },
    "ventanas_sin_directiva",
  ) === 0,
);

comprobar(
  "un rotulo con la tipografia escrita a mano cuenta",
  medirClave(
    { "a.html": '<label for="q" class="text-xs font-bold uppercase tracking-wider text-slate-500">Nombre</label>' },
    "rotulos_de_campo_a_mano",
  ) === 1,
);

comprobar(
  "el mismo rotulo con la forma comun NO cuenta",
  medirClave(
    { "a.html": '<label for="q" class="block campo__rotulo">Nombre</label>' },
    "rotulos_de_campo_a_mano",
  ) === 0,
);

comprobar(
  "un rotulo en minusculas no cuenta: es otra familia, no una copia de esta",
  medirClave(
    { "a.html": '<label for="q" class="block text-sm font-bold text-slate-800">Nombre</label>' },
    "rotulos_de_campo_a_mano",
  ) === 0,
);

comprobar(
  "un <input> cuyo unico rotulo es el placeholder cuenta como campo sin rotulo",
  medirClave({ "a.html": '<input type="text" placeholder="Buscar..." />' }, "campos_sin_rotulo") === 1,
);

comprobar(
  "un <input> con <label for> que apunta a su id NO cuenta",
  medirClave(
    { "a.html": '<label for="q">Buscar</label><input id="q" type="text" />' },
    "campos_sin_rotulo",
  ) === 0,
);

comprobar(
  "las formas ligadas [attr.for] y [attr.id] tambien rotulan: es como lo escriben las piezas compartidas",
  medirClave(
    { "a.html": '<label [attr.for]="ident()">Buscar</label><input [attr.id]="ident()" type="search" />' },
    "campos_sin_rotulo",
  ) === 0,
);

comprobar(
  'un `>` DENTRO de un atributo no corta la etiqueta (el fallo que acusaba al buscador compartido)',
  medirClave(
    {
      "a.html":
        '<label [attr.for]="ident()">Buscar</label>' +
        '<input [class.lleno]="valor().length > 0" [attr.id]="ident()" type="search" />',
    },
    "campos_sin_rotulo",
  ) === 0,
);

comprobar(
  "un campo envuelto por su propio <label> NO cuenta",
  medirClave({ "a.html": '<label>Activo <input type="checkbox" /></label>' }, "campos_sin_rotulo") === 0,
);

comprobar(
  "data-rotulo-externo declara que el rotulo lo pone quien embebe la pieza, y no cuenta",
  medirClave({ "a.html": '<select [id]="idCampo" data-rotulo-externo></select>' }, "campos_sin_rotulo") === 0,
);

comprobar(
  "los campos ocultos y los botones no son campos que rotular",
  medirClave(
    { "a.html": '<input type="hidden" value="1" /><input type="submit" value="Enviar" />' },
    "campos_sin_rotulo",
  ) === 0,
);

comprobar(
  "una etiqueta <button> PARTIDA EN VARIAS LINEAS se cuenta (el motivo de no usar grep)",
  medirClave({ "a.html": '<button\n  (click)="x()"\n  class="y"\n>Hola</button>' }, "botones_de_accion_sin_tipo") === 1,
);

comprobar(
  'un <button type="button" (click)> NO cuenta',
  medirClave({ "a.html": '<button type="button" (click)="x()">Hola</button>' }, "botones_de_accion_sin_tipo") === 0,
);

comprobar(
  "un boton de ENVIO (sin type y sin (click)) NO cuenta: es el submit legitimo del formulario",
  medirClave({ "a.html": "<form><button [disabled]=\"c()\">Guardar</button></form>" }, "botones_de_accion_sin_tipo") === 0,
);

comprobar(
  "<img> partida en varias lineas y sin width SI cuenta",
  medirClave({ "a.html": '<img\n  [src]="f"\n  alt="foto" />' }, "imagenes_sin_dimension") === 1,
);

comprobar(
  "<img> con width NO cuenta",
  medirClave({ "a.html": '<img src="f.jpg" width="320" height="200" alt="foto" />' }, "imagenes_sin_dimension") === 0,
);

comprobar(
  "'#00da5e80' (color con canal alfa) cuenta UNA vez, no dos ni cero",
  medirClave({ "a.html": '<div class="shadow-[0_0_8px_#00da5e80]"></div>' }, "colores_literales") === 1,
);

comprobar(
  "un color seguido de '_' (degradado arbitrario de Tailwind) SI cuenta",
  medirClave({ "a.html": '<div class="bg-[linear-gradient(135deg,#1d0d30_0%,#291242_55%)]"></div>' }, "colores_literales") === 2,
);

comprobar(
  "un hexadecimal de 7 digitos NO se cuenta como color de 6",
  medirClave({ "a.html": "<p>#1234567</p>" }, "colores_literales") === 0,
);

comprobar(
  'aria-live y role="alert" se suman en la misma metrica',
  medirClave({ "a.html": '<p aria-live="polite"></p><p role="alert"></p>' }, "anuncios_accesibles") === 2,
);

comprobar(
  "la palabra 'aria-live' suelta en un comentario NO cuenta (hace falta el '=')",
  medirClave({ "a.html": "<!-- falta un aria-live aqui -->" }, "anuncios_accesibles") === 0,
);

// --- formatos_de_fecha_sueltos: la unica metrica con AMBITO ---
//
// SE PRUEBA EL AMBITO Y NO SOLO EL PATRON. Al escribirla se puso `^src/app/features/admin/`
// y la ruta que llega es relativa a `src`, asi que empieza en `app/`: la metrica no casaba
// NUNCA y daba 0 en todas partes, que es exactamente como se ve una metrica saldada. Una
// puerta que pasa porque no mira nada es peor que no tener puerta.
const ADMIN = "app/features/admin/panel.component.html";
const PORTAL = "app/features/agenda/pages/agenda.component.html";

comprobar(
  "un formato de fecha fuera de los tres acordados cuenta si esta en el panel administrativo",
  medirClave({ [ADMIN]: "<p>{{ f | date: 'dd/MM/yyyy' }}</p>" }, "formatos_de_fecha_sueltos") === 1,
);

comprobar(
  "los tres formatos acordados NO cuentan",
  medirClave({
    [ADMIN]: "<p>{{ a | date: 'd MMM y' }}{{ b | date: 'd MMM y, HH:mm' }}{{ c | date: 'HH:mm' }}</p>",
  }, "formatos_de_fecha_sueltos") === 0,
);

comprobar(
  "el mismo formato suelto en el portal publico NO cuenta: es otra decision y no es de este panel",
  medirClave({ [PORTAL]: "<p>{{ f | date: 'EEEE d MMMM' }}</p>" }, "formatos_de_fecha_sueltos") === 0,
);

comprobar(
  "el formato con la localizacion detras tambien se reconoce, que es como se escribe en el panel",
  medirClave({ [ADMIN]: "<p>{{ f | date: 'd MMM y, HH:mm':'':'es-CO' }}</p>" }, "formatos_de_fecha_sueltos") === 0,
);

// ------------------------------------------------------------ 2,3) la puerta
console.log("\n2) La puerta muerde, en los dos sentidos");

{
  const plantillas = {
    "a.html": '<button (click)="x()">A</button>\n<a routerLink="/x">ir</a>',
  };
  // 1 boton de accion sin tipo, 1 routerLink.
  const base = {
    botones_de_accion_sin_tipo: 1,
    imagenes_sin_dimension: 0,
    colores_literales: 0,
    pildoras_de_estado: 0, formatos_de_fecha_sueltos: 0,
    enlaces_de_router: 1,
    dialogos_con_rol: 0,
    anuncios_accesibles: 0,
    tamanos_fuera_de_escala: 999999,
    ventanas_sin_directiva: 999999,
    campos_sin_rotulo: 999999,
    rotulos_de_campo_a_mano: 999999,
  };

  const b1 = banco(plantillas, base);
  comprobar("con el techo EXACTO sale 0", correr(["--raiz", b1.src, "--techo", b1.techo]).codigo === 0);
  fs.rmSync(b1.dir, { recursive: true, force: true });

  const b2 = banco(plantillas, { ...base, botones_de_accion_sin_tipo: 0 });
  const r2 = correr(["--raiz", b2.src, "--techo", b2.techo]);
  comprobar("MAXIMO superado (1 > 0) -> sale 1", r2.codigo === 1, r2.salida);
  comprobar("y dice cual metrica y por que", /botones_de_accion_sin_tipo/.test(r2.salida) && /type/.test(r2.salida), r2.salida);
  fs.rmSync(b2.dir, { recursive: true, force: true });

  const b3 = banco(plantillas, { ...base, enlaces_de_router: 2 });
  const r3 = correr(["--raiz", b3.src, "--techo", b3.techo]);
  comprobar("MINIMO incumplido (1 < 2) -> sale 1", r3.codigo === 1, r3.salida);
  fs.rmSync(b3.dir, { recursive: true, force: true });
}

// ------------------------------------------------------------------ 4,5) sellar
console.log("\n3) --sellar aprieta, nunca afloja");

{
  const plantillas = { "a.html": '<button (click)="x()">A</button>' };

  // En rojo: --sellar debe negarse y NO tocar el fichero.
  const rojo = banco(plantillas, {
    botones_de_accion_sin_tipo: 0,
    imagenes_sin_dimension: 0,
    colores_literales: 0,
    pildoras_de_estado: 0, formatos_de_fecha_sueltos: 0,
    enlaces_de_router: 0,
    dialogos_con_rol: 0,
    anuncios_accesibles: 0,
    tamanos_fuera_de_escala: 999999,
    ventanas_sin_directiva: 999999,
    campos_sin_rotulo: 999999,
    rotulos_de_campo_a_mano: 999999,
  });
  const antes = fs.readFileSync(rojo.techo, "utf8");
  const rs = correr(["--raiz", rojo.src, "--techo", rojo.techo, "--sellar"]);
  comprobar("--sellar con una metrica en rojo sale 1", rs.codigo === 1, rs.salida);
  comprobar("y NO reescribe el fichero de techo", fs.readFileSync(rojo.techo, "utf8") === antes);
  fs.rmSync(rojo.dir, { recursive: true, force: true });

  // Con holgura: --sellar debe apretar.
  const holgado = banco(plantillas, {
    botones_de_accion_sin_tipo: 5,
    imagenes_sin_dimension: 3,
    colores_literales: 9,
    pildoras_de_estado: 0, formatos_de_fecha_sueltos: 0,
    enlaces_de_router: 0,
    dialogos_con_rol: 0,
    anuncios_accesibles: 0,
    tamanos_fuera_de_escala: 999999,
    ventanas_sin_directiva: 999999,
    campos_sin_rotulo: 999999,
    rotulos_de_campo_a_mano: 999999,
  });
  const hs = correr(["--raiz", holgado.src, "--techo", holgado.techo, "--sellar"]);
  const tras = JSON.parse(fs.readFileSync(holgado.techo, "utf8"));
  comprobar("--sellar con holgura sale 0", hs.codigo === 0, hs.salida);
  comprobar("y aprieta el techo a la cifra real (5 -> 1)", tras.metricas.botones_de_accion_sin_tipo === 1, JSON.stringify(tras.metricas));
  comprobar("y deja constancia en el historial", Array.isArray(tras.historial) && tras.historial.length === 1);
  fs.rmSync(holgado.dir, { recursive: true, force: true });
}

// ------------------------------------------------------------- 6) falla cerrado
console.log("\n4) Falla cerrado");

{
  const b = banco({ "a.html": "<p>hola</p>" }, { botones_de_accion_sin_tipo: 0, imagenes_sin_dimension: 0, colores_literales: 0, pildoras_de_estado: 0, formatos_de_fecha_sueltos: 0, enlaces_de_router: 0, dialogos_con_rol: 0, anuncios_accesibles: 0, tamanos_fuera_de_escala: 999999, ventanas_sin_directiva: 999999 });

  const sinTecho = correr(["--raiz", b.src, "--techo", path.join(b.dir, "no-existe.json")]);
  comprobar("sin fichero de techo -> sale 1", sinTecho.codigo === 1, sinTecho.salida);

  const corrupto = path.join(b.dir, "corrupto.json");
  fs.writeFileSync(corrupto, "{ esto no es json", "utf8");
  comprobar("con el techo corrupto -> sale 1", correr(["--raiz", b.src, "--techo", corrupto]).codigo === 1);

  const incompleto = path.join(b.dir, "incompleto.json");
  fs.writeFileSync(incompleto, JSON.stringify({ metricas: { colores_literales: 0 } }), "utf8");
  const ri = correr(["--raiz", b.src, "--techo", incompleto]);
  comprobar("con una metrica sin declarar en el techo -> sale 1", ri.codigo === 1, ri.salida);

  const vacio = fs.mkdtempSync(path.join(os.tmpdir(), "trinquete-vacio-"));
  const rv = correr(["--raiz", vacio, "--techo", b.techo]);
  comprobar("sin NI UNA plantilla .html -> sale 1 (no 'verde porque no midio nada')", rv.codigo === 1, rv.salida);
  fs.rmSync(vacio, { recursive: true, force: true });

  fs.rmSync(b.dir, { recursive: true, force: true });
}

// --------------------------------------------------------- 7) sobre el src real
console.log("\n5) Sobre el src REAL");

{
  const antes = correr([]);
  comprobar("el src real esta en verde antes de tocar nada", antes.codigo === 0, antes.salida.slice(0, 400));

  const intruso = path.join(SRC_REAL, "app", "__intruso-trinquete.html");
  try {
    fs.writeFileSync(intruso, '<button (click)="x()">colado</button>\n', "utf8");
    const durante = correr([]);
    comprobar("un unico boton de accion sin type pone el trinquete en rojo", durante.codigo === 1, durante.salida.slice(0, 400));
  } finally {
    if (fs.existsSync(intruso)) fs.rmSync(intruso);
  }

  const despues = correr([]);
  comprobar("al quitarlo, vuelve a verde", despues.codigo === 0, despues.salida.slice(0, 400));
}

// -------------------------------------- 7bis) el sello registra contra que se midio
console.log("\n5bis) --sellar deja escrito el estado del arbol");

{
  // Se sella sobre una COPIA del techo real, aflojada a proposito, y desde
  // dentro del repositorio de verdad: es la unica forma de ejercitar la rama de
  // git de `estadoDelArbol`. Los bancos de prueba son directorios temporales sin
  // git, asi que alli solo se recorre la rama de "no hay repositorio".
  const copia = path.join(fs.mkdtempSync(path.join(os.tmpdir(), "trinquete-sello-")), "techo.json");
  const real = JSON.parse(fs.readFileSync(TECHO_REAL, "utf8"));
  real.metricas.colores_literales += 50; // holgura artificial
  fs.writeFileSync(copia, JSON.stringify(real, null, 2), "utf8");

  const r = correr(["--techo", copia, "--sellar"]);
  comprobar("--sellar sobre el src real sale 0", r.codigo === 0, r.salida.slice(0, 300));

  const tras = JSON.parse(fs.readFileSync(copia, "utf8"));
  comprobar("registra que git estaba disponible", tras.git_disponible === true, JSON.stringify(tras.git_disponible));
  comprobar("registra el commit base", typeof tras.commit_base === "string" && tras.commit_base.length >= 7, tras.commit_base);
  comprobar("registra la rama", typeof tras.rama === "string" && tras.rama.length > 0, tras.rama);
  comprobar("registra si el arbol estaba limpio", typeof tras.arbol_limpio === "boolean", String(tras.arbol_limpio));
  comprobar(
    "registra la lista de plantillas sin confirmar",
    Array.isArray(tras.plantillas_sin_confirmar_al_sellar),
    JSON.stringify(tras.plantillas_sin_confirmar_al_sellar?.slice(0, 2)),
  );
  comprobar(
    "conserva completa la primera ruta devuelta por git porcelain",
    !tras.plantillas_sin_confirmar_al_sellar.some((ruta) => ruta.startsWith("nmc-web/")),
    JSON.stringify(tras.plantillas_sin_confirmar_al_sellar?.slice(0, 2)),
  );
  comprobar("y aprieta la metrica aflojada", tras.metricas.colores_literales < real.metricas.colores_literales);

  fs.rmSync(path.dirname(copia), { recursive: true, force: true });
}

// ------------------------------------------------------------------- 8) cierre
console.log("\n6) Las pruebas no han movido el techo real");
comprobar(
  "techo-interfaz.json intacto",
  fs.readFileSync(TECHO_REAL, "utf8") === huellaTechoAntes,
  "las pruebas han reescrito el techo de verdad; eso las invalida",
);

console.log(`\n${pasadas} pasadas, ${fallos} fallidas\n`);
process.exit(fallos === 0 ? 0 : 1);
