import { AdminShellPageComponent } from './admin-shell-page.component';

/**
 * Una sola puerta para cada público.
 *
 * <b>Qué se retiró y por qué.</b> El armazón institucional montaba
 * `ExternalUserDashboardComponent` —581 líneas de TypeScript y 1052 de plantilla— cuando la
 * sesión no tenía un rol interno. Era la última de las «dos puertas al mismo sitio» que este
 * proyecto viene cerrando: la misma persona veía un panel dentro de `/administracion` y otro
 * distinto en `/gestion`. La anterior, `/colaboradores`, se retiró.
 *
 * <b>Por qué se podía retirar sin perder nada.</b> El API firma la cookie institucional en tres
 * sitios —entrar, renovar y actualizar el perfil— y los tres exigen `Permisos.RolesInternos`
 * antes de firmarla. `PuertaInstitucionalTests` lo fija del lado del servidor. Es decir: si hay
 * sesión institucional, es interna por construcción, y la rama de «colaborador» solo se alcanza
 * cuando la lectura de la sesión devuelve una lista de roles vacía — un fallo, no un perfil.
 *
 * <b>Lo que estas pruebas impiden.</b> Que alguien vuelva a colgar del armazón institucional una
 * segunda consola para gente de fuera. La bifurcación de seguridad se conserva; lo que no puede
 * volver es su antiguo destino.
 */
describe('el armazón administrativo tiene una sola puerta', () => {
  const plantilla = (AdminShellPageComponent as unknown as { ɵcmp?: { template?: unknown } });

  it('no declara ningún componente de panel externo entre sus imports', () => {
    const definicion = (AdminShellPageComponent as unknown as {
      ɵcmp?: { dependencies?: unknown };
    }).ɵcmp;

    const dependencias = definicion?.dependencies;
    const lista = typeof dependencias === 'function'
      ? (dependencias as () => unknown[])()
      : (dependencias as unknown[] | undefined) ?? [];

    const nombres = lista
      .map(d => (d as { name?: string })?.name ?? '')
      .filter(Boolean);

    // PRIMERO, QUE LA REFLEXION VEA ALGO. Sin esta comprobacion, el dia que Angular cambie la
    // forma de `ɵcmp` esta prueba pasaria con una lista vacia y dejaria de vigilar nada: seria
    // una puerta que no puede fallar, que es lo mismo que no tener puerta.
    expect(nombres.length)
      .withContext('la reflexión sobre ɵcmp no devolvió dependencias; la prueba no está midiendo nada')
      .toBeGreaterThan(5);
    expect(nombres).toContain('AdminGestionSitioPanelComponent');

    const externos = nombres.filter(n => /ExternalUserDashboard|PanelOrganizacion/i.test(n));
    expect(externos)
      .withContext(`el armazón institucional no debe montar paneles externos: ${externos.join(', ')}`)
      .toEqual([]);
  });

  it('sigue teniendo la bifurcación que evita abrir la consola sin rol interno', () => {
    // `isCollaboratorRole` es la única bifurcación del armazón entre «consola» y «no consola».
    // Retirar el panel externo NO puede haberse llevado por delante la guarda: sin ella, una
    // lectura fallida de la sesión abriría la consola entera.
    expect(typeof Object.getOwnPropertyDescriptor(
      AdminShellPageComponent.prototype, 'isCollaboratorRole',
    )?.get).toBe('function');

    expect(plantilla).toBeDefined();
  });
});
