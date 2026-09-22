/**
 * Sustituto de produccion de `dev-accounts.ts`. Sin cuentas: el panel de
 * cuentas de prueba no se dibuja y los formularios arrancan vacios.
 */
export interface DevAccount {
  email: string;
  password: string;
}

export const DEV_ADMIN_ACCOUNTS: Record<string, DevAccount> = {};
