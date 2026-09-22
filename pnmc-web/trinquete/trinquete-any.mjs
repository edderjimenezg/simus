#!/usr/bin/env node
/**
 * PNMC-058 — TRINQUETE DE DEUDA DE TIPADO (`any`)
 * ================================================
 *
 * EL PROBLEMA: la deuda de tipado podia subir sin que nada fallara. ESLint
 * tiene `@typescript-eslint/no-explicit-any` en "warn" (a proposito: ponerlo en
 * "error" dejaria el CI en rojo permanente, que es lo mismo que no tener CI),
 * y un aviso que nadie cuenta no frena nada.
 *
 * LA METRICA ES FIJA. No la toques. Viene de un desacuerdo que produjo cuatro
 * cifras distintas para el mismo hecho, y se cerro asi:
 *
 *     grep -roE ':\s*any\b|<any>|as any|any\[\]' --include='*.ts' src | wc -l
 *
 * Son OCURRENCIAS, no lineas ni ficheros. `any[]` cuenta. Este script
 * reimplementa ese grep en Node (para que corra igual en Windows sin Git Bash)
 * y `trinquete-any.prueba.mjs` comprueba en cada corrida que ambos coinciden:
 * si alguien altera el patron de aqui, la prueba lo delata.
 *
 * EL TRINQUETE GIRA EN UN SOLO SENTIDO:
 *   - cifra > techo  -> sale 1. El CI se rompe. Es la puerta.
 *   - cifra = techo  -> sale 0.
 *   - cifra < techo  -> sale 0 y avisa de que hay holgura para sellar.
 *   - `--sellar` con cifra < techo -> baja el techo y lo deja escrito.
 *   - `--sellar` con cifra > techo -> SE NIEGA y sale 1.
 *
 * O sea: la herramienta NUNCA sube el techo. Subirlo exige editar a mano
 * `techo-any.json`, y eso se ve en la revision del cambio, que es justo donde
 * se tiene que discutir.
 *
 * POR QUE EL TECHO INICIAL ES LA CIFRA DE HOY Y NO CERO: un trinquete
 * demasiado estricto se convierte en un impuesto que el equipo aprende a
 * rodear, y entonces no hay segunda oportunidad.
 *
 * FALLA CERRADO. Si falta el techo, si esta corrupto, o si el barrido no
 * encuentra NI UN fichero .ts, sale 1. Una puerta que pasa porque no midio
 * nada es el defecto que este repositorio ya se ha comido una vez.
 *
 * USO:
 *   node trinquete/trinquete-any.mjs              verificar (lo que corre el CI)
 *   node trinquete/trinquete-any.mjs --desglose   verificar + top de ficheros
 *   node trinquete/trinquete-any.mjs --sellar     bajar el techo tras pagar deuda
 *   node trinquete/trinquete-any.mjs --json       salida para maquinas
 *   node trinquete/trinquete-any.mjs --raiz DIR   apuntar a otra carpeta (pruebas)
 */

import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const AQUI = path.dirname(fileURLToPath(import.meta.url));
const FICHERO_TECHO = path.join(AQUI, "techo-any.json");
const RAIZ_POR_DEFECTO = path.resolve(AQUI, "..", "src");

/**
 * Equivalente exacto de `grep -oE ':\s*any\b|<any>|as any|any\[\]'`.
 *
 * Se usa [ \t\v\f\r] y no \s aposta: el \s de JavaScript incluye espacios
 * Unicode (NBSP y compania) que el [[:space:]] de grep en locale C no matchea.
 * Con la clase explicita las dos implementaciones cuentan lo mismo.
 *
 * Se devuelve un RegExp nuevo en cada llamada para no compartir `lastIndex`.
 */
export function patron() {
  return /:[ \t\v\f\r]*any\b|<any>|as any|any\[\]/g;
}

/** grep trabaja linea a linea: partimos igual para que \s nunca cruce un salto. */
export function contarEnTexto(texto) {
  let n = 0;
  for (const linea of texto.split("\n")) {
    const encontrados = linea.match(patron());
    if (encontrados) n += encontrados.length;
  }
  return n;
}

/**
 * Cuantas ocurrencias aporta cada alternativa del patron, medido sobre el mismo arbol que el
 * total. Se guarda en el techo al sellar; ver la nota del bloque de sellado sobre por que no
 * puede heredarse del sello anterior.
 */
export function repartoPorAlternativa(raiz) {
  const salida = { ": any": 0, "<any>": 0, "as any": 0, "any[]": 0 };
  for (const fichero of listarTs(raiz)) {
    let texto;
    try {
      texto = fs.readFileSync(fichero, "utf8");
    } catch {
      continue;
    }
    // Se reutiliza el patron completo y se clasifica cada coincidencia, en vez de contar con
    // cuatro patrones sueltos: asi la suma cuadra con `contarEnTexto` POR CONSTRUCCION y no
    // por coincidencia. Cuatro patrones independientes se solaparian —`any[]` tambien casa
    // `: any` segun el contexto— y el desglose dejaria de sumar el total.
    for (const linea of texto.split("\n")) {
      const encontrados = linea.match(patron());
      if (!encontrados) continue;
      for (const trozo of encontrados) {
        if (trozo === "<any>") salida["<any>"] += 1;
        else if (trozo === "as any") salida["as any"] += 1;
        else if (trozo === "any[]") salida["any[]"] += 1;
        else salida[": any"] += 1;
      }
    }
  }
  return salida;
}

/** Como `grep -r --include='*.ts'`: recursivo, sin seguir enlaces simbolicos. */
export function listarTs(raiz) {
  const salida = [];
  const pila = [raiz];
  while (pila.length > 0) {
    const dir = pila.pop();
    let entradas;
    try {
      entradas = fs.readdirSync(dir, { withFileTypes: true });
    } catch {
      continue;
    }
    for (const e of entradas) {
      const p = path.join(dir, e.name);
      if (e.isSymbolicLink()) continue;
      if (e.isDirectory()) pila.push(p);
      else if (e.isFile() && e.name.endsWith(".ts")) salida.push(p);
    }
  }
  salida.sort();
  return salida;
}

export function medir(raiz) {
  const ficheros = listarTs(raiz);
  const porFichero = [];
  let total = 0;
  for (const f of ficheros) {
    const n = contarEnTexto(fs.readFileSync(f, "utf8"));
    total += n;
    if (n > 0) {
      porFichero.push({
        fichero: path.relative(raiz, f).split(path.sep).join("/"),
        ocurrencias: n,
      });
    }
  }
  porFichero.sort(
    (a, b) => b.ocurrencias - a.ocurrencias || a.fichero.localeCompare(b.fichero),
  );
  return { total, ficherosEscaneados: ficheros.length, porFichero };
}

function leerTecho() {
  let crudo;
  try {
    crudo = fs.readFileSync(FICHERO_TECHO, "utf8");
  } catch {
    return { error: `No existe ${FICHERO_TECHO}. Sin techo no hay trinquete: se falla cerrado.` };
  }
  let datos;
  try {
    datos = JSON.parse(crudo);
  } catch (e) {
    return { error: `${FICHERO_TECHO} no es JSON valido (${e.message}). Se falla cerrado.` };
  }
  if (!Number.isInteger(datos.techo) || datos.techo < 0) {
    return { error: `El campo "techo" de ${FICHERO_TECHO} no es un entero >= 0. Se falla cerrado.` };
  }
  return { datos };
}

function main() {
  const args = process.argv.slice(2);
  const sellar = args.includes("--sellar");
  const json = args.includes("--json");
  const desglose = args.includes("--desglose");
  const iRaiz = args.indexOf("--raiz");
  const raiz = iRaiz >= 0 && args[iRaiz + 1] ? path.resolve(args[iRaiz + 1]) : RAIZ_POR_DEFECTO;

  const salir = (codigo, cuerpo) => {
    if (json) console.log(JSON.stringify({ ...cuerpo, codigo }, null, 2));
    process.exit(codigo);
  };
  const di = (linea) => {
    if (!json) console.log(linea);
  };

  const { datos: techoDatos, error } = leerTecho();
  if (error) {
    di(`TRINQUETE any: FALLO — ${error}`);
    return salir(1, { estado: "TECHO_INVALIDO", error });
  }

  if (!fs.existsSync(raiz)) {
    const m = `La raiz ${raiz} no existe. Se falla cerrado: no se puede afirmar nada sobre lo que no se midio.`;
    di(`TRINQUETE any: FALLO — ${m}`);
    return salir(1, { estado: "RAIZ_INEXISTENTE", error: m });
  }

  const { total, ficherosEscaneados, porFichero } = medir(raiz);

  // Guarda anti-adorno: si no se escaneo nada, la cifra 0 no significa
  // "deuda saldada", significa "el barrido esta roto". Falla.
  if (ficherosEscaneados === 0) {
    const m = `No se encontro ningun .ts bajo ${raiz}. Una puerta que pasa porque no midio nada es un adorno.`;
    di(`TRINQUETE any: FALLO — ${m}`);
    return salir(1, { estado: "BARRIDO_VACIO", error: m });
  }

  const techo = techoDatos.techo;
  const base = { estado: "", cifra: total, techo, ficherosEscaneados, raiz };

  if (desglose && !json) {
    console.log("Ficheros con mas deuda:");
    for (const f of porFichero.slice(0, 15)) {
      console.log(`  ${String(f.ocurrencias).padStart(4)}  ${f.fichero}`);
    }
    console.log("");
  }

  // --- SUBIO: la puerta muerde. ---
  if (total > techo) {
    const exceso = total - techo;
    di(`TRINQUETE any: FALLO — la deuda de tipado SUBIO.`);
    di(`  techo sellado : ${techo}   (${techoDatos.medido_en ?? "sin fecha"})`);
    di(`  cifra de ahora: ${total}   (+${exceso})`);
    di(`  ${ficherosEscaneados} ficheros .ts barridos bajo ${raiz}`);
    di("");
    di(`  Quita ${exceso} ocurrencia(s) de 'any' de lo que acabas de tocar.`);
    di(`  Para ver donde: node trinquete/trinquete-any.mjs --desglose`);
    di("");
    di(`  El techo NO se sube con esta herramienta. Si de verdad hay que subirlo,`);
    di(`  se edita techo-any.json a mano y se discute en la revision del cambio.`);
    return salir(1, { ...base, estado: "SUBIO", exceso });
  }

  const holgura = techo - total;

  // --- BAJO: sellar solo si te lo piden explicitamente. ---
  if (sellar) {
    if (holgura === 0) {
      di(`TRINQUETE any: nada que sellar. La cifra ya es igual al techo (${techo}).`);
      return salir(0, { ...base, estado: "SIN_CAMBIO" });
    }
    const ahora = new Date().toISOString();

    // EL DESGLOSE SE REMIDE, NO SE HEREDA.
    //
    // Aqui habia un `...techoDatos` que arrastraba `reparto_por_alternativa_al_sellar` y
    // `mayores_deudores_al_sellar` del sello ANTERIOR mientras `techo` se movia. El fichero
    // quedaba contradiciendose a si mismo: registraba `techo: 518` y a
    // la vez un reparto que sumaba 533 y un `admin.service.ts: 150` que era la cifra de HEAD,
    // no la del arbol sellado (135).
    //
    // No es cosmetico: dos dias despues, otra sesion diagnosticando una subida del trinquete
    // resto la cifra de hoy contra ese desglose caduco y dedujo un «+18» que no existia. Un
    // desglose que describe un estado distinto del que anuncia su total no es informacion
    // incompleta, es informacion falsa — y aparece justo donde alguien la va a usar para
    // decidir quien rompio el trinquete.
    //
    // Los sufijos `_al_sellar` prometen «medido en el momento de sellar». Ahora lo cumplen.
    const reparto = repartoPorAlternativa(raiz);
    const nuevo = {
      ...techoDatos,
      techo: total,
      medido_en: ahora,
      ficheros_escaneados: ficherosEscaneados,
      reparto_por_alternativa_al_sellar: reparto,
      mayores_deudores_al_sellar: [...porFichero]
        .sort((a, b) => b.ocurrencias - a.ocurrencias)
        .slice(0, 5)
        .map(({ fichero, ocurrencias }) => ({ fichero, ocurrencias })),
      historial: [
        ...(Array.isArray(techoDatos.historial) ? techoDatos.historial : []),
        { fecha: ahora, techo: total, delta: -holgura, via: "--sellar" },
      ],
    };

    // POSTCONDICION, no adorno: el reparto tiene que sumar el total que se esta sellando. Si
    // no suma, el fichero saldria contradiciendose otra vez y es mejor no escribirlo.
    const sumaReparto = Object.values(reparto).reduce((a, b) => a + b, 0);
    if (sumaReparto !== total) {
      const m = `El reparto por alternativa suma ${sumaReparto} y el total es ${total}. `
        + "No se sella: un techo con desglose incoherente engaña a quien diagnostique despues.";
      di(`TRINQUETE any: FALLO — ${m}`);
      return salir(1, { estado: "DESGLOSE_INCOHERENTE", error: m });
    }
    fs.writeFileSync(FICHERO_TECHO, JSON.stringify(nuevo, null, 2) + "\n", "utf8");
    di(`TRINQUETE any: techo BAJADO de ${techo} a ${total} (-${holgura}).`);
    di(`  Escrito en ${FICHERO_TECHO}. Incluye ese fichero en tu cambio.`);
    return salir(0, { ...base, estado: "SELLADO", techo: total, bajada: holgura });
  }

  if (holgura > 0) {
    // La antiguedad del sello importa tanto como la cifra: «holgura de 15
    // respecto a un techo sellado hace 6 horas» dice que alguien limpio y no
    // cerro. Sin la fecha, se lee como ruido y se ignora.
    let antiguedad = "";
    const sello = techoDatos?.medido_en ? Date.parse(techoDatos.medido_en) : NaN;
    if (Number.isFinite(sello)) {
      const horas = (Date.now() - sello) / 36e5;
      if (horas >= 0) {
        antiguedad =
          horas < 1
            ? `hace ${Math.round(horas * 60)} min`
            : horas < 48
              ? `hace ${Math.round(horas)} h`
              : `hace ${Math.round(horas / 24)} dias`;
      }
    }
    di(`TRINQUETE any: OK — y ademas has BAJADO la deuda.`);
    di(`  techo sellado : ${techo}${antiguedad ? `   (${antiguedad})` : ""}`);
    di(`  cifra de ahora: ${total}   (-${holgura})`);
    di("");
    di(`  Sella la mejora para que no se pueda deshacer sin que nadie se entere:`);
    di(`     node trinquete/trinquete-any.mjs --sellar`);

    // En CI, ademas, como anotacion: el trinquete solo ROMPE cuando la cifra
    // supera el techo, asi que una mejora sin sellar no falla nada y se queda
    // como margen gratis para el siguiente cambio. Enterrada en el log, nadie la
    // ve. Paso el 22 ago 2026: se pagaron 15 ocurrencias y, de no haberse
    // sellado a mano ese mismo dia, habrian quedado 15 de holgura invisible.
    if (process.env.GITHUB_ACTIONS) {
      di(
        `::notice title=Trinquete de tipado con holgura::La deuda de 'any' bajo a ${total}, ` +
          `${holgura} por debajo del techo de ${techo}${antiguedad ? ` sellado ${antiguedad}` : ""}. ` +
          `Ejecuta 'node trinquete/trinquete-any.mjs --sellar' y confirma el techo, o ese margen se lo gasta el siguiente cambio.`,
      );
    }
    return salir(0, { ...base, estado: "BAJO", holgura });
  }

  di(`TRINQUETE any: OK — ${total} ocurrencias, igual al techo (${techo}).`);
  di(`  ${ficherosEscaneados} ficheros .ts barridos.`);
  return salir(0, { ...base, estado: "IGUAL" });
}

// macOS expone el mismo temporal como `/var/folders/...` y `/private/var/folders/...`.
// Comparar las cadenas hacía que las copias aisladas usadas por la propia prueba no ejecutaran
// `main`: devolvían 0 sin medir, sin sellar y hasta con el JSON del techo borrado. Se comparan las
// rutas reales para que la puerta falle cerrada también cuando el sistema atraviesa un enlace.
const esEntrada = Boolean(process.argv[1])
  && fs.realpathSync(path.resolve(process.argv[1])) === fs.realpathSync(fileURLToPath(import.meta.url));
if (esEntrada) {
  main();
}
