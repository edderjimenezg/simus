/**
 * Un nombre propio escrito como se escribe, no como lo publica el DANE.
 *
 * <b>POR QUE HACE FALTA.</b> La fuente territorial del proyecto —`dbo.Divipola`, cargada del
 * archivo oficial— guarda los nombres en mayúsculas sostenidas: `HUILA`, `LA PLATA`,
 * `CIÉNAGA DE ORO`. Es como el DANE los publica, y **la fuente no se toca**: normalizar la tabla
 * sería reescribir un dato oficial para resolver un asunto de presentación. Lo que se corrige es
 * cómo se enseña.
 *
 * <b>QUE HACE.</b> Pone cada palabra en su forma normal y deja en minúscula las que en español no
 * se escriben con mayúscula dentro de un nombre —«de», «del», «la», «y»…—, salvo cuando abren el
 * nombre. Deja intactas las siglas con punto, que es lo que hace que `BOGOTÁ, D.C.` no acabe como
 * «Bogotá, D.c.».
 *
 * Casos comprobados contra la tabla real, que son los tres que se salen de lo corriente:
 * `BOGOTÁ, D.C.`, `MIRITÍ - PARANÁ` y
 * `ARCHIPIÉLAGO DE SAN ANDRÉS, PROVIDENCIA Y SANTA CATALINA`.
 *
 * <b>SOLO PARA NOMBRES PROPIOS.</b> No sirve para texto corrido ni para rótulos: no sabe de
 * oraciones, solo de nombres.
 */

/** Palabras que dentro de un nombre propio van en minúscula, salvo si lo abren. */
const ENLACES = new Set([
  'de', 'del', 'la', 'las', 'el', 'los', 'y', 'e',
]);

function palabra(bruta: string, esLaPrimera: boolean): string {
  // Una sigla con punto —«D.C.»— ya está escrita como debe: tocarla la estropea.
  if (bruta.includes('.')) { return bruta; }

  const limpia = bruta.toLocaleLowerCase('es-CO');
  // La comparación se hace sin los signos pegados: «SANTA,» sigue siendo «santa».
  const nucleo = limpia.replace(/[^\p{L}]/gu, '');
  if (!esLaPrimera && ENLACES.has(nucleo)) { return limpia; }

  // Se pone en mayúscula la primera LETRA, que no siempre es el primer carácter: «(la)».
  return limpia.replace(/\p{L}/u, letra => letra.toLocaleUpperCase('es-CO'));
}

/**
 * Devuelve el nombre escrito en su forma normal. Un valor vacío vuelve tal cual.
 */
export function nombrePropio(valor: string | null | undefined): string {
  const texto = (valor ?? '').trim();
  if (!texto) { return ''; }

  // SOLO SI VIENE EN MAYUSCULAS SOSTENIDAS. Un nombre ya escrito a mano —«Fundación de Prueba»—
  // no se vuelve a componer: hacerlo destruiría una mayúscula deliberada dentro de la palabra.
  if (texto !== texto.toLocaleUpperCase('es-CO')) { return texto; }

  let primeraPendiente = true;
  return texto.split(/(\s+)/).map(trozo => {
    if (/^\s+$/.test(trozo)) { return trozo; }
    // Un guion suelto entre dos nombres no cuenta como palabra y no consume «la primera».
    if (!/\p{L}/u.test(trozo)) { return trozo; }
    const resultado = palabra(trozo, primeraPendiente);
    primeraPendiente = false;
    return resultado;
  }).join('');
}
