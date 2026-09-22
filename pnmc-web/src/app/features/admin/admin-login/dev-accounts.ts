/**
 * Cuentas sembradas del entorno local, para no tener que teclearlas en cada
 * prueba de rol.
 *
 * En el build de produccion este archivo se sustituye por
 * `dev-accounts.production.ts` mediante `fileReplacements` en angular.json, de
 * modo que ni los correos ni las contrasenas llegan al bundle publicado. La
 * sustitucion es la garantia: no depende de que el optimizador elimine una rama
 * muerta.
 */
export interface DevAccount {
  email: string;
  password: string;
}

/**
 * Cuentas de la consola interna, por identificador de rol.
 *
 * SOLO LAS INTERNAS. `externo@pnmc.local` estaba aqui y no le corresponde: en la
 * base tiene `CanalAcceso = 'externo'`, igual que `participante.dos` y
 * `participante.tres`. Su puerta es `/ecosistema/ingresar`. Tenerlo en el acceso
 * rapido de `/admin` ofrecia entrar por donde su canal no entra.
 *
 * El rol `externo` NO se retira de `ADMIN_ROLES`: el panel de usuarios lo usa
 * para etiquetar a esas personas, que existen aunque no entren por aqui.
 */
export const DEV_ADMIN_ACCOUNTS: Record<string, DevAccount> = {
  webmaster: { email: 'admin@pnmc.local', password: 'admin' },
  gestor_interno: { email: 'gestor@pnmc.local', password: 'admin' },
};
