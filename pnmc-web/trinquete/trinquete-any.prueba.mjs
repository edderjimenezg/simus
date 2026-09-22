#!/usr/bin/env node
/**
 * PNMC-058 — PRUEBAS DEL TRINQUETE
 * =================================
 *
 * Una puerta que no puede fallar no es una puerta. Este fichero existe para
 * demostrar, en cada corrida y no una sola vez en la consola de alguien, que
 * el trinquete RECHAZA de verdad.
 *
 * Lo que se comprueba, y por que cada cosa:
 *
 *   1  El contador de Node da EXACTAMENTE lo mismo que el grep oficial sobre
 *      el src real. Es la unica garantia de que no hemos sustituido la metrica
 *      acordada por otra parecida. Si no hay grep (Windows pelado), se salta y
 *      se dice en voz alta, no se finge que paso.
 *   2  Semantica del patron, caso a caso. `: any[]` es UNA ocurrencia, no dos.
 *      `Map<string, any>` es CERO (el patron oficial no lo pilla; es una
 *      limitacion conocida y sellada, no un fallo que arreglar aqui).
 *   3  LA PUERTA MUERDE: con techo+1 el proceso sale 1.
 *   4  Con techo exacto sale 0, y con techo-1 sale 0. El trinquete no es un
 *      impuesto: solo castiga subir.
 *   5  EL TRINQUETE GIRA EN UN SOLO SENTIDO: `--sellar` con la cifra POR
 *      ENCIMA del techo se niega y sale 1, sin tocar el fichero de techo.
 *   6  `--sellar` con la cifra por debajo SI baja el techo (sobre una copia,
 *      jamas sobre el techo real).
 *   7  FALLA CERRADO: sin fichero de techo, con techo corrupto, o con un
 *      barrido que no encuentra ni un .ts, sale 1. El modo "paso porque no
 *      midio nada" es exactamente el defecto que ya nos comimos una vez.
 *   8  El techo real no se ha movido durante las pruebas.
 *
 * USO:
 *   node trinquete/trinquete-any.prueba.mjs
 *   node trinquete/trinquete-any.prueba.mjs --con-inyeccion-real
 *
 * `--con-inyeccion-real` mete un .ts de mas en el src DE VERDAD, comprueba que
 * el trinquete se pone rojo, y lo borra. Es la prueba mas contundente, pero
 * escribe en el arbol de trabajo: por eso no corre por defecto, para no chocar
 * con otra sesion trabajando a la vez ni con el propio CI.
 */

import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";

import { contarEnTexto, medir } from "./trinquete-any.mjs";

const AQUI = path.dirname(fileURLToPath(import.meta.url));
const GUION = path.join(AQUI, "trinquete-any.mjs");
const FICHERO_TECHO = path.join(AQUI, "techo-any.json");
const SRC_REAL = path.resolve(AQUI, "..", "src");

let pasadas = 0;
let fallos = 0;

function comprobar(nombre, condicion, detalle = "") {
  if (condicion) {
    pasadas++;
    console.log(`  ok   ${nombre}`);
  } else {
    fallos++;
    console.log(`  FALLA ${nombre}${detalle ? `\n         ${detalle}` : ""}`);
  }
}

function saltar(nombre, razon) {
  console.log(`  --   ${nombre}  (saltada: ${razon})`);
}

/** Ejecuta el trinquete y devuelve { codigo, salida }. */
function correr(args, opciones = {}) {
  const guion = opciones.guion ?? GUION;
  const r = spawnSync(process.execPath, [guion, ...args], { encoding: "utf8" });
  return { codigo: r.status, salida: `${r.stdout ?? ""}${r.stderr ?? ""}` };
}

/** Crea un directorio con ficheros .ts que suman exactamente `n` ocurrencias. */
function fixture(n) {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), "trinquete-any-"));
  const lineas = [];
  for (let i = 0; i < n; i++) lineas.push(`export const v${i}: any = ${i};`);
  fs.writeFileSync(path.join(dir, "deuda.ts"), lineas.join("\n") + "\n", "utf8");
  // Comprobacion de la propia fixture: si el generador miente, todo lo demas
  // que se apoye en el es humo.
  const real = medir(dir).total;
  if (real !== n) throw new Error(`fixture rota: pedidas ${n} ocurrencias, generadas ${real}`);
  return dir;
}

/** Copia el trinquete a un temporal con su propio techo, para probar el sellado. */
function trinqueteAislado(techo) {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), "trinquete-copia-"));
  fs.copyFileSync(GUION, path.join(dir, "trinquete-any.mjs"));
  fs.writeFileSync(
    path.join(dir, "techo-any.json"),
    JSON.stringify({ techo, medido_en: "prueba", historial: [] }, null, 2),
    "utf8",
  );
  return { guion: path.join(dir, "trinquete-any.mjs"), techoJson: path.join(dir, "techo-any.json") };
}

const huellaTechoAntes = fs.readFileSync(FICHERO_TECHO, "utf8");
const TECHO = JSON.parse(huellaTechoAntes).techo;

console.log(`\nTRINQUETE any — pruebas.  Techo sellado: ${TECHO}\n`);

// ---------------------------------------------------------------- 1
console.log("1) El contador de Node coincide con el grep oficial");
{
  const nodeTotal = medir(SRC_REAL).total;
  const g = spawnSync(
    "grep",
    ["-roE", ":\\s*any\\b|<any>|as any|any\\[\\]", "--include=*.ts", "src"],
    { encoding: "utf8", cwd: path.resolve(AQUI, "..") },
  );
  if (g.error || typeof g.stdout !== "string" || (g.status !== 0 && g.status !== 1)) {
    saltar("node == grep sobre el src real", "no hay grep utilizable en este equipo");
    console.log(`       (el contador de Node dice ${nodeTotal}; verificalo a mano en un shell con grep)`);
  } else {
    const grepTotal = g.stdout.split("\n").filter((l) => l.length > 0).length;
    comprobar(
      `node == grep sobre el src real (${nodeTotal})`,
      nodeTotal === grepTotal,
      `node=${nodeTotal}  grep=${grepTotal}. La reimplementacion se ha desviado de la metrica oficial.`,
    );
  }
}

// ---------------------------------------------------------------- 2
console.log("\n2) Semantica del patron oficial");
{
  const casos = [
    ["const a: any = 1;", 1, "': any' cuenta"],
    ["const a:any = 1;", 1, "sin espacio tambien"],
    ["const a:   any = 1;", 1, "con varios espacios tambien"],
    ["let xs: any[] = [];", 1, "': any[]' es UNA, no dos"],
    ["let xs: Array<any> = [];", 1, "'<any>' cuenta"],
    ["const b = x as any;", 1, "'as any' cuenta"],
    ["function f(): any {}", 1, "tipo de retorno cuenta"],
    ["type T = any[];", 1, "'any[]' suelto cuenta"],
    // PUNTO CIEGO CONOCIDO Y SELLADO, no un fallo que arreglar aqui: el patron
    // oficial solo ve '<any>' pegado, asi que 'Map<string, any>' o
    // 'Record<string, any>' valen CERO. Se deja fijado en una prueba para que
    // sea una limitacion documentada y no una sorpresa: quien quiera cerrar
    // ese hueco tiene que cambiar la metrica Y el techo en la misma decision.
    ["const m: Map<string, any> = new Map();", 0, "'any' como 2o argumento generico NO lo pilla el patron (punto ciego sellado)"],
    ["const r: Record<string, any> = {};", 0, "idem con Record: punto ciego sellado"],
    ["const s: anything = 1;", 0, "'anything' NO cuenta: \\b protege"],
    ["const anyway = 1;", 0, "'anyway' NO cuenta"],
    ["// comentario sobre any", 0, "'any' suelto en prosa NO cuenta"],
    ["const c: string = 'any';", 0, "'any' dentro de una cadena sin ':' delante NO cuenta"],
  ];
  for (const [texto, esperado, porque] of casos) {
    const real = contarEnTexto(texto);
    comprobar(`${porque}  ->  ${esperado}`, real === esperado, `"${texto}" dio ${real}, se esperaba ${esperado}`);
  }
  comprobar(
    "el patron no cruza saltos de linea (grep va linea a linea)",
    contarEnTexto("const a:\n  any = 1;") === 0,
    "un ':' al final de linea y 'any' en la siguiente no debe contar",
  );
}

// ---------------------------------------------------------------- 3  LA PUERTA MUERDE
console.log("\n3) LA PUERTA MUERDE: una ocurrencia de mas rompe el CI");
{
  const dir = fixture(TECHO + 1);
  const { codigo, salida } = correr(["--raiz", dir]);
  comprobar(`con ${TECHO + 1} ocurrencias (techo+1) sale 1`, codigo === 1, `salio ${codigo}`);
  comprobar("y lo dice: 'SUBIO'", /SUBIO/.test(salida), salida.slice(0, 400));
  comprobar("y dice cuanto sobra", /\+1/.test(salida), salida.slice(0, 400));
  fs.rmSync(dir, { recursive: true, force: true });
}

// ---------------------------------------------------------------- 4
console.log("\n4) No es un impuesto: igual pasa, y menos pasa");
{
  const igual = fixture(TECHO);
  const r1 = correr(["--raiz", igual]);
  comprobar(`con ${TECHO} exactas sale 0`, r1.codigo === 0, `salio ${r1.codigo}: ${r1.salida.slice(0, 300)}`);
  fs.rmSync(igual, { recursive: true, force: true });

  const menos = fixture(TECHO - 1);
  const r2 = correr(["--raiz", menos]);
  comprobar(`con ${TECHO - 1} sale 0`, r2.codigo === 0, `salio ${r2.codigo}`);
  comprobar("y ofrece sellar la mejora", /--sellar/.test(r2.salida), r2.salida.slice(0, 300));
  fs.rmSync(menos, { recursive: true, force: true });
}

// ---------------------------------------------------------------- 5  UN SOLO SENTIDO
console.log("\n5) El trinquete gira en UN SOLO SENTIDO");
{
  const { guion, techoJson } = trinqueteAislado(100);
  const antes = fs.readFileSync(techoJson, "utf8");
  const dir = fixture(150);
  const { codigo, salida } = correr(["--raiz", dir, "--sellar"], { guion });
  comprobar("'--sellar' con la cifra POR ENCIMA del techo se niega y sale 1", codigo === 1, `salio ${codigo}`);
  comprobar(
    "y NO reescribe el techo para dejarlo pasar",
    fs.readFileSync(techoJson, "utf8") === antes,
    "el techo se movio hacia arriba: el trinquete seria un adorno",
  );
  comprobar("y explica que subirlo se hace a mano y se revisa", /a mano/.test(salida), salida.slice(0, 400));
  fs.rmSync(dir, { recursive: true, force: true });
}

// ---------------------------------------------------------------- 6
console.log("\n6) '--sellar' SI baja el techo cuando se paga deuda de verdad");
{
  const { guion, techoJson } = trinqueteAislado(100);
  const dir = fixture(80);
  const { codigo } = correr(["--raiz", dir, "--sellar"], { guion });
  const despues = JSON.parse(fs.readFileSync(techoJson, "utf8"));
  comprobar("sale 0", codigo === 0, `salio ${codigo}`);
  comprobar("el techo baja de 100 a 80", despues.techo === 80, `quedo en ${despues.techo}`);
  comprobar("y queda registrado en el historial", despues.historial.at(-1)?.delta === -20);

  // Y una vez sellado a 80, volver a 100 tiene que romper.
  const dir2 = fixture(100);
  const r = correr(["--raiz", dir2, "--sellar"], { guion });
  comprobar(
    "tras bajar el techo, volver atras ya rompe (el trinquete no se puede deshacer)",
    r.codigo === 1,
    `salio ${r.codigo}`,
  );
  fs.rmSync(dir, { recursive: true, force: true });
  fs.rmSync(dir2, { recursive: true, force: true });
}

// ---------------------------------------------------------------- 7  FALLA CERRADO
console.log("\n7) Falla cerrado (una puerta que pasa por no medir es un adorno)");
{
  const vacio = fs.mkdtempSync(path.join(os.tmpdir(), "trinquete-vacio-"));
  const r1 = correr(["--raiz", vacio]);
  comprobar("barrido sin ningun .ts -> sale 1, no 'cero deuda, enhorabuena'", r1.codigo === 1, `salio ${r1.codigo}`);
  comprobar("y lo llama por su nombre", /adorno|ningun \.ts/.test(r1.salida), r1.salida.slice(0, 300));
  fs.rmSync(vacio, { recursive: true, force: true });

  const r2 = correr(["--raiz", path.join(os.tmpdir(), "no-existe-" + Date.now())]);
  comprobar("raiz inexistente -> sale 1", r2.codigo === 1, `salio ${r2.codigo}`);

  const { guion, techoJson } = trinqueteAislado(100);
  fs.writeFileSync(techoJson, "{ esto no es json", "utf8");
  const dir = fixture(10);
  const r3 = correr(["--raiz", dir], { guion });
  comprobar("techo corrupto -> sale 1", r3.codigo === 1, `salio ${r3.codigo}`);
  fs.rmSync(techoJson);
  const r4 = correr(["--raiz", dir], { guion });
  comprobar("techo borrado -> sale 1 (no 'sin techo, todo vale')", r4.codigo === 1, `salio ${r4.codigo}`);
  fs.rmSync(dir, { recursive: true, force: true });
}

// ---------------------------------------------------------------- 8 (opcional)
if (process.argv.includes("--con-inyeccion-real")) {
  console.log("\n8) Inyeccion en el src REAL");
  const intruso = path.join(SRC_REAL, "__trinquete_inyeccion_temporal.ts");
  try {
    const antes = correr([]);
    comprobar("el src real esta en verde antes de tocar nada", antes.codigo === 0, antes.salida.slice(0, 300));
    fs.writeFileSync(intruso, "export const colado: any = 1;\n", "utf8");
    const durante = correr([]);
    comprobar("un unico 'any' de mas en el src real pone el trinquete en rojo", durante.codigo === 1, durante.salida.slice(0, 400));
  } finally {
    if (fs.existsSync(intruso)) fs.rmSync(intruso);
  }
  const despues = correr([]);
  comprobar("al quitarlo, vuelve a verde", despues.codigo === 0, despues.salida.slice(0, 300));
} else {
  console.log("\n8) Inyeccion en el src REAL  (saltada: pasa --con-inyeccion-real para incluirla)");
}

// ---------------------------------------------------------------- cierre
console.log("\n9) Las pruebas no han movido el techo real");
comprobar(
  "techo-any.json intacto",
  fs.readFileSync(FICHERO_TECHO, "utf8") === huellaTechoAntes,
  "las pruebas han reescrito el techo de verdad; eso las invalida",
);

console.log(`\n${pasadas} pasadas, ${fallos} fallidas\n`);
process.exit(fallos === 0 ? 0 : 1);
