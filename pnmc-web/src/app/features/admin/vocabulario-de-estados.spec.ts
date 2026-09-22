import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Subject, of } from 'rxjs';
import { AdminRecordsPanelComponent } from './admin-records-panel/admin-records-panel.component';
import { AdminService } from '../../core/services/admin.service';
import { SessionService } from '../../core/services/session.service';
import { ADMIN_MODULES, ADMIN_STATUS, ESTADOS_CON_TRABAJO_PENDIENTE, ESTADOS_QUE_ESPERAN_A_LA_CONSOLA, ETIQUETAS_DE_TRAMITE, etiquetaDeEstado } from './domain/admin-config';

/**
 * El vocabulario de estados de la consola, contra el de la base.
 *
 * <b>El defecto que estas pruebas fijan.</b> Hasta cuatro pantallas usaban
 * el código <code>en_evaluacion</code>. No existe: <code>dbo.EstadosContenido</code> tiene ocho
 * filas y ninguna es esa, y la palabra no aparece en <code>pnmc-api/src</code>. Comprobado contra la
 * API local el 26 de agosto:
 *
 * <pre>
 * POST /admin/data/records/musicSchools/1/status {"status":"en_evaluacion"}
 *   → 400 {"status":["El estado no existe en EstadosContenido."]}
 * POST /admin/data/records/musicSchools/1/status {"status":"en_revision"}
 *   → 200 {"id":1,"status":"en_revision"}
 * </pre>
 *
 * <b>Lo que rompía.</b> El desplegable de estado de cada fila ofrecía «En revisión» con ese valor:
 * un botón que parecía funcionar y devolvía 400. La cola de revisión filtraba por él, así que los
 * registros que de verdad estaban en revisión no salían en la cola. Y el recuento de pendientes del
 * armazón hacía lo mismo: decía que no había trabajo esperando cuando lo había.
 *
 * <b>Por qué el nombre no bastaba para verlo.</b> «en evaluación» y «en revisión» son sinónimos en
 * castellano, y las etiquetas de pantalla siempre dijeron «En revisión». El código estaba mal y el
 * texto estaba bien: leyendo la pantalla no se distinguía.
 */
describe('El vocabulario de estados de la consola', () => {
  /**
   * Las ocho filas de `dbo.EstadosContenido`, escritas a mano.
   *
   * SE ESCRIBEN LITERALES Y NO SE LEEN DE `ADMIN_STATUS`, que es lo que hace que esto sea una
   * prueba: comparar la constante consigo misma no puede fallar nunca. Consultado el 26 de agosto
   * de 2026 con `SELECT CodigoEstado FROM dbo.EstadosContenido ORDER BY CodigoEstado`.
   */
  const CODIGOS_DE_LA_BASE = [
    'ajustes_solicitados',
    'aprobado',
    'archivado',
    'borrador',
    'en_revision',
    'publicado',
    'rechazado',
    'registrada',
  ];

  it('el catálogo del navegador dice exactamente lo que dice la base', () => {
    expect(Object.keys(ADMIN_STATUS).sort()).toEqual(CODIGOS_DE_LA_BASE);
  });

  it('ningún código llega crudo a la pantalla, ni los que nadie declaró', () => {
    // EL CRITERIO:
    // «los estados aparecen con nombres como en_revision, no están optimizados para frontend».
    // Lo que fija esta prueba no es la tabla —esa la fija la de arriba— sino el RESPALDO: un código
    // que nadie añadió se humaniza igual, que es lo que impide que el defecto vuelva con el
    // siguiente estado que invente el servidor.
    expect(etiquetaDeEstado('en_revision')).toBe('En revisión');
    expect(etiquetaDeEstado('aclaracion_enviada')).toBe('Aclaración enviada');
    expect(etiquetaDeEstado('un_estado_que_nadie_declaro')).toBe('Un estado que nadie declaro');
    expect(etiquetaDeEstado('')).toBe('Sin estado');
    expect(etiquetaDeEstado(null)).toBe('Sin estado');

    // Y TAMBIEN EL CAMELLO. El respaldo solo partía por guiones, así que un código en PascalCase
    // salía entero: «AdministracionControl» se leía tal cual en el filtro de grupos de Auditoría,
    // medido.
    expect(etiquetaDeEstado('AdministracionControl')).toBe('Administracion control');
    expect(etiquetaDeEstado('catalogoEditorial')).toBe('Catalogo editorial');
    // LAS SIGLAS SOBREVIVEN ENTERAS: el corte va antes de una mayúscula precedida de minúscula o
    // dígito, y «PNMC» no tiene ninguna delante. Sin esta condición se leería «P N M C».
    expect(etiquetaDeEstado('PNMC')).toBe('PNMC');

    for (const codigo of [...Object.keys(ADMIN_STATUS), ...Object.keys(ETIQUETAS_DE_TRAMITE)]) {
      expect(etiquetaDeEstado(codigo)).not.toContain('_');
    }
  });

  it('los estados con trabajo pendiente son códigos que existen', () => {
    // Esta es la comprobación que habría cazado `en_evaluacion` el primer día.
    for (const codigo of ESTADOS_CON_TRABAJO_PENDIENTE) {
      expect(CODIGOS_DE_LA_BASE).toContain(codigo);
    }
    expect(ESTADOS_CON_TRABAJO_PENDIENTE).toContain('en_revision');
  });

  /*
    EL DISTINTIVO DE «REVISIÓN» DECIA 99. Contaba con `ESTADOS_CON_TRABAJO_PENDIENTE`, que incluye
    `borrador` y `ajustes_solicitados`. Medido en `PNMC_LOCAL`: 76
    borradores + 14 en revisión + 9 con ajustes = 99, el número exacto que salía en pantalla,
    cuando la consola solo tenía 14 decisiones que tomar. Un borrador no ha salido de la
    organización y uno con ajustes ya volvió a ella: en ninguno de los dos le toca a la consola.
  */
  it('lo que espera a la consola es solo «en revisión», y no el trabajo de la organización', () => {
    for (const codigo of ESTADOS_QUE_ESPERAN_A_LA_CONSOLA) {
      expect(CODIGOS_DE_LA_BASE).toContain(codigo);
    }
    expect(ESTADOS_QUE_ESPERAN_A_LA_CONSOLA).toContain('en_revision');
    expect(ESTADOS_QUE_ESPERAN_A_LA_CONSOLA)
      .withContext('un borrador no ha salido de la organización: la consola no puede ni verlo')
      .not.toContain('borrador');
    expect(ESTADOS_QUE_ESPERAN_A_LA_CONSOLA)
      .withContext('«ajustes solicitados» ya volvió a la organización: el turno no es de la consola')
      .not.toContain('ajustes_solicitados');
  });

  describe('el panel de registros', () => {
    let fixture: ComponentFixture<AdminRecordsPanelComponent>;
    let comp: AdminRecordsPanelComponent;
    let pedido: any = null;

    beforeEach(async () => {
      pedido = null;
      const adminStub = {
        cargarRegistrosDeLaConsola: (p: any) => {
          pedido = p;
          return new Subject<any>().asObservable();
        },
        cargarEsquemaDeLaBase: () => of({}),
        // El doble responde, pero ya nadie inspecciona lo que recibe: las pruebas que lo hacían
        // medían el desplegable y el formulario, retirados en una revisión anterior. Se conserva porque el
        // componente lo inyecta y sin él el TestBed falla al construirlo.
        cambiarEstadoDeRegistro: (p: any) => of({ id: p.id, status: p.status }),
      };

      await TestBed.configureTestingModule({
        imports: [AdminRecordsPanelComponent],
        providers: [
          { provide: AdminService, useValue: adminStub },
          { provide: SessionService, useValue: { session: () => null } },
        ],
      }).compileComponents();

      fixture = TestBed.createComponent(AdminRecordsPanelComponent);
      comp = fixture.componentInstance;
      comp.module = ADMIN_MODULES.find((m) => m.id === 'musicSchools') ?? ADMIN_MODULES[0];
      comp.roleId = 'webmaster';
      comp.divipola = {};
      fixture.detectChanges();
    });

    // AQUI HABIA TRES PRUEBAS SOBRE EL FORMULARIO Y EL DESPLEGABLE de cada fila: que el
    // desplegable «Cambiar estado» solo ofrecía códigos existentes, que al elegir «En revisión»
    // mandaba `en_revision` al API, y que guardar desde el formulario dejaba un estado válido.
    //
    // Los tres controles se retiraron en una revisión anterior con los módulos genéricos que los usaban.
    // Para un Festival nunca existieron, y con razón: su estado lo mueve el circuito de revisión
    // institucional —enviar, aprobar, pedir ajustes—, no un control que lo salta desde la tabla.
    //
    // LO QUE FIJABAN SIGUE FIJADO, y en el sitio correcto: las tres primeras pruebas de este
    // fichero comparan el catálogo del navegador contra el de la base, que es de donde salía el
    // código inexistente `en_evaluacion` que originó todo esto.

    it('la tabla se abre pidiendo el orden que pone delante lo pendiente', () => {
      // LA TABLA DE PRIORIDADES SE FUE AL SERVIDOR: aquí ordenaba la página
      // ya traída —cien filas de hasta quinientas—. Lo que se comprueba desde el navegador es que
      // ese orden se PIDE; que ponga `en_revision` primero lo comprueba
      // `El_Orden_Por_Prioridad_Pone_Delante_Lo_Que_Espera_Revision` en la suite de la API.
      // `loadRecords` lo dispara `ngOnChanges`, que no salta al asignar la entrada a mano.
      comp.loadRecords();

      expect(comp.ORDEN_POR_OMISION).toBe('prioridad');
      expect(pedido).not.toBeNull();
      expect(pedido.orden).toBe('prioridad');
      expect(pedido.direccion).toBe('asc');
    });

    it('la píldora de «en revisión» no es la de borrador', () => {
      expect(comp.statusPillClass('en_revision')).not.toBe(comp.statusPillClass('borrador'));
      expect(comp.statusPillClass('en_revision')).toContain('blue');
    });
  });

});
