import * as path from 'path';
import * as os from 'os';

/**
 * Dónde vive la cookie de la sesión compartida entre barridos.
 *
 * Vive en un módulo aparte —y no en `sesion.setup.ts`— porque Playwright
 * prohíbe que un fichero de prueba importe otro fichero de prueba, y el setup
 * lo es. Fuera del repositorio, además: una cookie de sesión no es código.
 */
export const RUTA_SESION =
  process.env.PNMC_AUDIT_SESION ?? path.join(os.tmpdir(), 'pnmc-auditoria-sesion.json');
