/**
 * Sustituto de produccion de `agentation.ts`.
 *
 * El archivo real importa el paquete `agentation` junto con React y ReactDOM.
 * Aunque `main.ts` solo lo carga fuera de produccion, el import dinamico bastaba
 * para que el bundler emitiera y desplegara ese chunk. Reemplazar el modulo
 * corta la dependencia de raiz: React deja de compilarse.
 */
export function mountAgentation(): void {
  // La herramienta de desarrollo no existe en produccion.
}
