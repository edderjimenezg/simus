import {
  ROLES_INTERNOS,
  algunoEsRolInterno,
  canRoles,
  esRolInterno,
  getModulesForRole,
  getModulesForRoles,
  rolesDeSesion,
} from './admin-config';

/**
 * En la consola: una sesión puede traer varios roles, y lo que abre es la UNIÓN.
 *
 * POR QUÉ EXISTE. El SPA decidía por `session().role`, un solo valor. Desde que el API envía
 * `roles`, decidir por el primero haría que la consola abriera o cerrara pantallas según el orden
 * en que llegaran — el mismo defecto que en el API señalaba el plan de construcción como «el sitio
 * donde N:M rompe de verdad», aquí trasladado a la pantalla.
 *
 * LO QUE MIDE, Y EN QUÉ ORDEN:
 *  1. Que `rolesDeSesion` entienda las dos formas de respuesta, incluida la anterior al cambio.
 *  2. Que la unión sea unión: ni la intersección, ni «todo».
 *  3. Que una sesión ilegible caiga del lado seguro, que es el que no abre la consola interna.
 */
describe('roles en conjunto', () => {
  describe('rolesDeSesion', () => {
    it('lee el campo nuevo cuando viene', () => {
      expect(rolesDeSesion({ role: 'gestor_interno', roles: ['gestor_interno', 'webmaster'] }))
        .toEqual(['gestor_interno', 'webmaster']);
    });

    it('cae al campo antiguo cuando el nuevo no viene, para no romper una respuesta anterior al cambio', () => {
      expect(rolesDeSesion({ role: 'webmaster' })).toEqual(['webmaster']);
    });

    it('normaliza y quita repetidos, porque el conjunto se compara y se recorre', () => {
      expect(rolesDeSesion({ roles: ['  WEBMASTER ', 'webmaster', ''] })).toEqual(['webmaster']);
    });

    it('devuelve la lista VACÍA sin sesión, que es el lado seguro del error', () => {
      // No devuelve un rol por omisión a propósito: con la lista vacía `algunoEsRolInterno` dice
      // que no y el SPA abre el panel de colaborador en vez de la consola interna. Es la misma
      // decisión que documenta `isCollaboratorRole`.
      expect(rolesDeSesion(null)).toEqual([]);
      expect(rolesDeSesion({ role: null, roles: [] })).toEqual([]);
      expect(algunoEsRolInterno(rolesDeSesion(null))).toBeFalse();
    });
  });

  describe('la unión de módulos', () => {
    it('con dos roles abre exactamente lo de uno más lo del otro, sin repetir', () => {
      const soloGestor = getModulesForRole('gestor_interno');
      const soloWebmaster = getModulesForRole('webmaster');
      const losDos = getModulesForRoles(['gestor_interno', 'webmaster']);

      const esperados = new Set([...soloGestor, ...soloWebmaster].map((modulo) => modulo.id));

      expect(losDos.map((modulo) => modulo.id).sort()).toEqual([...esperados].sort());
      expect(new Set(losDos.map((modulo) => modulo.id)).size).toBe(losDos.length);
    });

    it('no abre nada que no abriera ninguno de los dos por separado', () => {
      // Es el aserto que separa «da la unión» de «da todo». Sin él, una implementación que
      // devolviera ADMIN_MODULES entero pasaría la prueba de arriba en cuanto los dos roles
      // sumaran el catálogo completo.
      const permitidos = new Set([
        ...getModulesForRole('gestor_interno'),
        ...getModulesForRole('webmaster'),
      ].map((modulo) => modulo.id));

      for (const modulo of getModulesForRoles(['gestor_interno', 'webmaster'])) {
        expect(permitidos.has(modulo.id)).toBeTrue();
      }
    });

    it('un conjunto vacío no abre ningún módulo', () => {
      expect(getModulesForRoles([])).toEqual([]);
    });
  });

  describe('capacidades', () => {
    it('basta que UNO de los roles la traiga', () => {
      // `manage_global_users` es solo del webmaster. El gestor no la tiene; los dos juntos, sí.
      expect(canRoles(['gestor_interno'], 'manage_global_users')).toBeFalse();
      expect(canRoles(['gestor_interno', 'webmaster'], 'manage_global_users')).toBeTrue();
    });

    it('un rol que nadie declaró no aporta capacidades', () => {
      expect(canRoles(['rol_que_nadie_declaro'], 'read')).toBeFalse();
    });
  });

  describe('«lo tiene» no es lo mismo que «es su rol principal»', () => {
    // POR QUE ESTA PRUEBA EXISTE, aunque hoy las dos formas den lo mismo.
    //
    // La consola decidia que paneles abrir con `roleId === 'webmaster'`, donde `roleId` es el rol
    // de MAYOR precedencia. Funciona, pero solo porque el catalogo de hoy es una jerarquia de
    // tres. El dia que aparezca un rol que no encaje en ese orden —la subdivision fina de
    // funcionarios que el modelo deja para mas adelante— esa comparacion empezaria a ocultar
    // paneles a quien si puede abrirlos, y en silencio.
    //
    // Se paso a `roles.includes(...)`. Esto fija la diferencia entre las dos reglas con un rol
    // que la precedencia no conoce, que es exactamente el caso futuro.
    const principalDe = (roles: readonly string[]): string => {
      if (roles.includes('webmaster')) { return 'webmaster'; }
      if (roles.includes('gestor_interno')) { return 'gestor_interno'; }
      return roles[0] || '';
    };

    it('con la jerarquia de hoy, las dos reglas coinciden', () => {
      const roles = ['gestor_interno', 'webmaster'];
      expect(principalDe(roles) === 'webmaster').toBe(roles.includes('webmaster'));
    });

    it('con un rol que la precedencia no conoce, DEJAN de coincidir', () => {
      const roles = ['webmaster', 'revisor_territorial'];

      // «Lo tiene»: si. «Es su principal»: no, porque webmaster gana la precedencia.
      expect(roles.includes('revisor_territorial')).toBeTrue();
      expect(principalDe(roles) === 'revisor_territorial').toBeFalse();
    });
  });

  describe('la lista blanca de roles internos', () => {
    it('sigue siendo lista blanca: lo que no está nombrado, no es interno', () => {
      expect(algunoEsRolInterno(['externo'])).toBeFalse();
      expect(algunoEsRolInterno(['rol_que_nadie_declaro'])).toBeFalse();
      expect(algunoEsRolInterno(['externo', 'gestor_interno'])).toBeTrue();
      expect(ROLES_INTERNOS.every((rol) => esRolInterno(rol))).toBeTrue();
    });
  });
});
