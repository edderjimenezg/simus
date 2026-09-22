import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { EMPTY, of, throwError } from 'rxjs';
import { AdminService, AlertaDeCalidad, CandidatoDuplicado, SolicitudDeVinculacion } from '../../../core/services/admin.service';
import { AdminSolicitudesPanelComponent } from './admin-solicitudes-panel.component';
import { BandejaDeTrabajoService } from './bandeja-de-trabajo.service';

/**
 * LA BANDEJA UNIFICADA DE SOLICITUDES: el único centro institucional para procesar trámites.
 *
 * <b>Dónde vive.</b> Hasta la bandeja era parte de
 * `AdminShellPageComponent`, y estas pruebas tenían que abrir el armazón entero sin sesión para
 * que su `effect()` de recarga no disparara media docena de peticiones. Ahora prueban el panel
 * (`AdminSolicitudesPanelComponent`) y escriben directamente sobre las colas de
 * `BandejaDeTrabajoService` (`revisiones`, `propuestas`, `solicitudes`, `reclamaciones`…), que es
 * quien las reúne en `colaCompleta()`.
 *
 * <b>Qué unificó este archivo.</b> Hasta, «Solicitudes», la «Bandeja de
 * revisión» de Festivales y las «Propuestas de cambio en revisión» eran tres paneles que decidían
 * lo mismo por caminos distintos -y el peor síntoma era que las propuestas de cambio ni siquiera
 * aparecían en la bandeja unificada, solo en su panel aparte-. Ahora `colaVisible` las junta a
 * las cuatro -revisión, propuesta, solicitud/retiro, reclamación-, cada fila se expande en el
 * mismo lugar en vez de abrir un panel aparte, y «Visualizar» es la única acción para ver el
 * detalle completo: para un Festival en revisión monta `<app-ficha-en-revision>` -la misma ficha
 * que llenó la organización-, para una propuesta muestra la comparación campo a campo, y para todo
 * lo demás abre el panel superpuesto genérico con lo que ya trae `raw`.
 */
describe('AdminSolicitudesPanelComponent · la bandeja', () => {
  let fixture: ComponentFixture<AdminSolicitudesPanelComponent>;
  let componente: AdminSolicitudesPanelComponent;
  let bandeja: BandejaDeTrabajoService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [AdminSolicitudesPanelComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });

    fixture = TestBed.createComponent(AdminSolicitudesPanelComponent);
    componente = fixture.componentInstance;
    bandeja = TestBed.inject(BandejaDeTrabajoService);
    http = TestBed.inject(HttpTestingController);
    // EL PANEL NO PIDE NADA AL MONTARSE: quien carga la cola es el armazón, que sabe cuándo hay
    // sesión. Aquí no la hay, y las pruebas escriben directamente sobre las colas del servicio.
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  it('la reclamación de administración entra a la bandeja con el nombre real del Festival y de la organización', () => {
    bandeja.reclamaciones.set([{
      id: '9', estado: 'enviada', registroCanonicoId: '105', registroNombre: 'Festival Prueba',
      organizacionSolicitanteId: '116', organizacionSolicitanteNombre: 'Fundación Prueba',
      fechaEnvio: '2026-09-01T10:00:00Z',
    }]);

    const fila = componente.colaVisible().find(item => item.kind === 'reclamacion');

    // MUTANTE QUE MATA: leer `registroCanonicoId`/`organizacionSolicitanteId` en vez de los
    // nombres. La fila seguiría existiendo -por eso el `find` de arriba no basta solo- pero
    // volvería a decir «Registro #105» y «Organización #116», el defecto que este cambio cierra.
    expect(fila?.record).toBe('Festival Prueba');
    expect(fila?.organization).toBe('Fundación Prueba');
    expect(fila?.type).toBe('Reclamación de administración');
  });

  it('una reclamación ya decidida no aparece en la bandeja', () => {
    bandeja.reclamaciones.set([
      { id: '1', estado: 'enviada', registroNombre: 'Activa' },
      { id: '2', estado: 'aprobada', registroNombre: 'Ya aprobada' },
      { id: '3', estado: 'rechazada', registroNombre: 'Ya rechazada' },
    ]);

    const reclamaciones = componente.colaVisible().filter(item => item.kind === 'reclamacion');
    expect(reclamaciones.map(item => item.record)).toEqual(['Activa']);
  });

  it('una solicitud de vinculación o de retiro ya decidida no aparece en la bandeja', () => {
    bandeja.solicitudes.set([
      { id: '1', status: 'pendiente', recordName: 'Pendiente' },
      { id: '2', status: 'aprobada', recordName: 'Ya aprobada' },
      { id: '3', status: 'rechazada', recordName: 'Ya rechazada' },
      { id: '4', status: 'cancelada', recordName: 'Ya cancelada' },
      { id: '5', status: 'en_revision', recordName: 'En revisión' },
    ]);

    // MUTANTE QUE MATA: filtrar por 'approved'/'rejected'/'cancelled' -en inglés, que es lo que
    // decía este filtro antes de esta prueba-. Los seis valores reales son españoles
    // (`RecordGovernanceEndpoints.LinkStatuses`), así que ese filtro no descartaba nunca nada y
    // una solicitud ya decidida se quedaba en la bandeja para siempre.
    const registros = componente.colaVisible().filter(item => item.kind === 'solicitud' || item.kind === 'retiro').map(item => item.record);
    expect(registros).toEqual(['Pendiente', 'En revisión']);
  });

  it('el id de una revisión lleva el módulo: dos registros de módulos distintos con el mismo id numérico no se confunden', () => {
    // `reviewRecords()` junta las filas de TODOS los módulos -Festivales, Escuelas, Lutería...-,
    // cada uno con su propio autoincremental. Se vio en vivo: al
    // decidir sobre una Escuela con id 101, la bandeja recargada mostraba «seleccionado» un
    // registro de Lutería con el mismo id 101 que nadie había tocado.
    bandeja.revisiones.set([
      { id: '101', moduleId: 'musicSchools', title: 'Escuela de Vientos del Norte', status: 'en_revision' },
      { id: '101', moduleId: 'luteria', title: 'La Sonata Lutieres', status: 'en_revision' },
    ]);

    const ids = componente.colaVisible().map(item => item.id);

    // MUTANTE QUE MATA: construir el id solo con `review-${item.id}`. Las dos filas de arriba
    // compartirían `review-101` -mismo id para dos asuntos distintos- y `tramiteElegido()`
    // encontraría la primera coincidencia sin importar cuál se hubiera seleccionado.
    expect(new Set(ids).size).toBe(2);
  });

  it('una propuesta de cambio sobre un Festival publicado entra a la bandeja como su propio tipo', () => {
    // EL DEFECTO QUE ESTO CIERRA. Hasta esta lista solo vivía dentro
    // de `AdminFestivalReviewPanelComponent`: la bandeja unificada no la conocía, y el usuario lo
    // vio en vivo -«me dice que hay una solicitud... pero abajo sí aparece una que dice propuesta
    // cambio de festival»-, dos bandejas que deberían concordar y no se hablaban entre sí.
    bandeja.propuestas.set([
      { id: 3, nombreFestival: 'Festival Prueba', organizacionNombre: 'Fundación Prueba', fechaEnvioRevision: '2026-09-01T10:00:00Z' },
    ]);

    const fila = componente.colaVisible().find(item => item.kind === 'propuesta');

    expect(fila?.record).toBe('Festival Prueba');
    expect(fila?.organization).toBe('Fundación Prueba');
    expect(fila?.module).toBe('festivals');
    expect(fila?.rawId).toBe(3);
  });

  it('el filtro «reclamacion» deja solo las reclamaciones, sin tocar revisiones, propuestas ni solicitudes', () => {
    bandeja.revisiones.set([{ id: '1', title: 'Un Festival en revisión', status: 'en_revision' }]);
    bandeja.propuestas.set([{ id: 2, nombreFestival: 'Una propuesta' }]);
    bandeja.solicitudes.set([{ id: '3', status: 'pendiente', recordName: 'Una solicitud' }]);
    bandeja.reclamaciones.set([{ id: '4', estado: 'enviada', registroNombre: 'Una reclamación' }]);

    bandeja.filtro.set('reclamacion');

    const cola = componente.colaVisible();
    expect(cola.length).toBe(1);
    expect(cola[0].kind).toBe('reclamacion');
  });

  describe('expandir una fila', () => {
    let adminService: AdminService;

    beforeEach(() => {
      adminService = TestBed.inject(AdminService);
    });

    it('sobre un Festival en revisión, pide su ficha institucional completa por el id numérico', () => {
      const ficha = { id: '77', nombre: 'Festival de prueba agosto', descripcion: 'Un Festival' } as any;
      const espia = spyOn(adminService, 'cargarFichaInstitucionalDeFestival').and.returnValue(of(ficha));

      componente.alternarTramite({ id: 'review-festivals-77', kind: 'revision', rawId: '77', module: 'festivals' });

      // MUTANTE QUE MATA: pasar el `rawId` como string en vez de convertirlo con `Number(...)`.
      // El endpoint institucional espera un id numérico en la URL.
      expect(espia).toHaveBeenCalledWith(77);
      expect(componente.detalleFestival()).toBe(ficha);
      expect(componente.detalleCargando()).toBe(false);
    });

    it('sobre una propuesta de cambio, pide el detalle con la comparación campo a campo', () => {
      const detalle = {
        propuesta: { id: 3, nombre: 'Festival Prueba', descripcion: null, nivelCobertura: 'Municipal', codigoDepartamento: null, codigoMunicipio: null, periodicidad: null, correoContacto: null, practicasMusicales: [], territoriosSonoros: [] },
        versionVigente: { numeroVersion: 2, nombre: 'Festival Prueba', descripcion: null, nivelCobertura: 'Departamental', codigoDepartamento: null, codigoMunicipio: null, periodicidad: null, correoContacto: null, practicasMusicales: [], territoriosSonoros: [] },
        historial: [],
      };
      const espia = spyOn(adminService, 'cargarFichaDePropuesta').and.returnValue(of(detalle));

      componente.alternarTramite({ id: 'proposal-3', kind: 'propuesta', rawId: 3, module: 'festivals' });

      expect(espia).toHaveBeenCalledWith(3);
      expect(componente.detallePropuesta()).toBe(detalle as any);
      // MUTANTE QUE MATA: comparar solo nombre y descripción, como hacía la plantilla vieja del
      // panel aparte. «Alcance territorial» cambió de Departamental a Municipal y tiene que
      // contar como un campo que cambia.
      expect(componente.propuestaCamposQueCambian()).toBe(1);
    });

    it('sobre una solicitud, un retiro o una reclamación, no pide nada al servidor: usa lo que ya trae `raw`', () => {
      const espia = spyOn(adminService, 'cargarFichaInstitucionalDeFestival').and.returnValue(of({} as any));

      componente.alternarTramite({ id: 'request-9', kind: 'solicitud', rawId: '9', module: 'festivals' });

      expect(espia).not.toHaveBeenCalled();
      expect(componente.detalleCargando()).toBe(false);
    });

    it('volver a expandir la misma fila la contrae: es un interruptor, no solo un abrir', () => {
      componente.alternarTramite({ id: 'request-9', kind: 'solicitud', rawId: '9', module: 'festivals' });
      expect(componente.tramiteElegidoId()).toBe('request-9');

      componente.alternarTramite({ id: 'request-9', kind: 'solicitud', rawId: '9', module: 'festivals' });

      expect(componente.tramiteElegidoId()).toBeNull();
    });

    it('si el servidor rechaza la ficha del Festival, se muestra el error y no queda «cargando»', () => {
      spyOn(adminService, 'cargarFichaInstitucionalDeFestival').and.returnValue(throwError(() => ({ message: 'No fue posible abrir la ficha del Festival' })));

      componente.alternarTramite({ id: 'review-festivals-77', kind: 'revision', rawId: '77', module: 'festivals' });

      expect(componente.detalleError()).toBe('No fue posible abrir la ficha del Festival');
      expect(componente.detalleCargando()).toBe(false);
      expect(componente.detalleFestival()).toBeNull();
    });

    it('«Cerrar» limpia el detalle cargado, para que la próxima fila no arrastre datos de la anterior', () => {
      spyOn(adminService, 'cargarFichaInstitucionalDeFestival').and.returnValue(of({ id: '77' } as any));
      componente.alternarTramite({ id: 'review-festivals-77', kind: 'revision', rawId: '77', module: 'festivals' });

      componente.cerrarTramite();

      expect(componente.tramiteElegidoId()).toBeNull();
      expect(componente.detalleFestival()).toBeNull();
      expect(componente.detalleError()).toBe('');
    });
  });

  describe('«Visualizar»: la única acción para ver el detalle completo', () => {
    it('sobre un Festival en revisión, monta la ficha completa en vez del panel superpuesto genérico', () => {
      // EL DEFECTO QUE ESTO CIERRA. Se define eliminar la
      // duplicación entre «Previsualizar» y «Abrir ficha» y dejar una sola acción. Para un
      // Festival en revisión, esa acción no es el panel genérico de solo lectura: es
      // `<app-ficha-en-revision>`, la misma ficha que llenó la organización, con «Pedir cambio»
      // campo a campo -repetirlo con datos de solo lectura habría sido el panel paralelo que la
      // unificación quiere cerrar-.
      componente.visualizar({ id: 'review-festivals-77', kind: 'revision', rawId: '77', module: 'festivals', record: 'Festival de prueba agosto', organization: 'Fundación Prueba' });

      expect(componente.fichaEnRevisionAbierta()).toEqual({ festivalId: '77', nombreFestival: 'Festival de prueba agosto', organizacion: 'Fundación Prueba' });
    });

    it('una propuesta no monta ninguna ficha aparte: su comparación ya vive en el panel', () => {
      // El panel superpuesto genérico se retiró al pasar la revisión a
      // la columna del trámite. Una propuesta no necesita montar nada: `detallePropuesta()` ya
      // tiene la comparación campo a campo y el historial.
      componente.visualizar({ id: 'proposal-3', kind: 'propuesta', rawId: 3, module: 'festivals' });

      expect(componente.fichaEnRevisionAbierta()).toBeNull();
      expect(componente.edicionEnRevisionAbierta()).toBeNull();
    });

    it('cambiar a la comparación completa y volver deja el panel donde estaba', () => {
      componente.vistaDelTramite.set('ficha');

      componente.cambiarVistaDelTramite('resumen');

      expect(componente.vistaDelTramite()).toBe('resumen');
    });

    it('«enviada» desde la ficha en revisión cierra la ficha, contrae la fila y avisa que se pidieron ajustes', () => {
      spyOn(bandeja, 'recargar');
      componente.tramiteElegidoId.set('review-festivals-77');
      componente.visualizar({ id: 'review-festivals-77', kind: 'revision', rawId: '77', module: 'festivals', record: 'Festival de prueba agosto', organization: 'Fundación Prueba' });

      componente.alEnviarDesdeFichaEnRevision();

      expect(componente.fichaEnRevisionAbierta()).toBeNull();
      expect(componente.tramiteElegidoId()).toBeNull();
      expect(componente.accionRapidaMensaje()).toBe('');
      expect(componente.avisoTemporal()).toContain('sugerencias de ajuste');
      expect(bandeja.recargar).toHaveBeenCalled();
    });
  });

  describe('los hallazgos del sistema entran a la misma bandeja', () => {
    /*
      HASTA EL 15 DE SEPTIEMBRE DE 2026 VIVIAN EN OTRA PANTALLA. «Calidad y coincidencias» era una
      sección aparte con cinco pestañas, tres de las cuales repetían esta bandeja. Se retiró, y sus
      dos pestañas propias -Duplicados y Alertas- pasaron a ser dos posiciones del filtro.

      La diferencia con un trámite es real y por eso se prueba: nadie los envió, así que no tienen
      solicitante. Lo que NO puede pasar es que se inventen uno.
    */
    const duplicado = {
      id: '21', moduleId: 'festivals', sourceRecordId: '23', candidateRecordId: '24',
      similarityLevel: 'alta', similarityScore: 0.95, status: 'pendiente',
      createdAt: '2026-09-10T10:00:00Z',
    };
    const alerta: AlertaDeCalidad = {
      id: '31', moduleId: 'festivals', recordId: '23', flagType: 'territorio_incompleto',
      severity: 'alta', status: 'abierta', detail: 'Falta municipio.',
      createdAt: '2026-09-10T10:00:00Z', updatedAt: '2026-09-10T10:00:00Z',
    };

    it('un posible duplicado es una fila más, con los dos registros que se parecen', () => {
      bandeja.duplicados.set([duplicado]);

      const fila = componente.colaVisible().find(item => item.kind === 'duplicado');

      expect(fila?.type).toBe('Posible duplicado');
      expect(fila?.record).toBe('Registros 23 y 24');
      // MUTANTE QUE MATA: escribir «Organización sin identificar», que diría que se buscó y no
      // había. El contrato de duplicados no trae organización: la fila dice de dónde sale.
      expect(fila?.organization).toBe('Detectado por el sistema');
    });

    it('una alerta de calidad es una fila más, y nombra el registro sobre el que se abrió', () => {
      bandeja.alertas.set([alerta]);

      const fila = componente.colaVisible().find(item => item.kind === 'alerta');

      expect(fila?.type).toBe('Alerta de calidad');
      expect(fila?.record).toBe('festivals #23');
    });

    it('lo ya decidido no vuelve a la cola: solo entra lo que sigue esperando', () => {
      bandeja.duplicados.set([duplicado, { ...duplicado, id: '22', status: 'resuelto' }]);
      bandeja.alertas.set([alerta, { ...alerta, id: '32', status: 'resuelta' }]);

      const cola = componente.colaVisible();

      expect(cola.filter(item => item.kind === 'duplicado').length).toBe(1);
      expect(cola.filter(item => item.kind === 'alerta').length).toBe(1);
    });

    it('el filtro gana dos posiciones con su recuento, y las demás siguen ahí', () => {
      bandeja.duplicados.set([duplicado]);
      bandeja.alertas.set([alerta]);

      const posiciones = componente.filtrosDeLaBandeja();

      expect(posiciones.find(p => p.id === 'duplicado')?.conteo).toBe(1);
      expect(posiciones.find(p => p.id === 'alerta')?.conteo).toBe(1);
      expect(posiciones.find(p => p.id === 'todos')?.conteo).toBe(2);
    });

    it('los dos se deciden en la bandeja, sin abrir otra pantalla', () => {
      expect(componente.seDecideAqui({ kind: 'duplicado' })).toBeTrue();
      expect(componente.seDecideAqui({ kind: 'alerta' })).toBeTrue();
    });

  });

  describe('acciones rápidas desde el resumen expandido', () => {
    let adminService: AdminService;
    /**
     * El recorrido completo de una decisión, tal como lo hace una persona desde el 17 de septiembre
     * de 2026: se pulsa el verbo, se abre la confirmación EN EL PANEL, se escribe el motivo si el
     * verbo lo pide, y se confirma. Antes esto eran un `window.prompt` y dos `window.confirm`.
     */
    function decidir(item: Record<string, unknown>, accion: string, motivo = ''): void {
      componente.accionRapida(item as never, accion as never);
      componente.motivoDeLaDecision = motivo;
      componente.confirmarLaAccion();
    }
    // Los dobles devuelven el CONTRATO y no un objeto vacío sin tipar: si un campo desaparece
    // del DTO, esto deja de compilar, que es justo el aviso que se quiere.
    const duplicadoDecidido: CandidatoDuplicado = {
      id: '21', moduleId: 'festivals', sourceRecordId: '23', candidateRecordId: '24',
      similarityLevel: 'alta', similarityScore: 0.95, evidenceJson: '{}', status: 'resuelto',
      decision: 'fusionar', decisionComment: '', createdAt: '2026-09-10T10:00:00Z',
      updatedAt: '2026-09-10T10:00:00Z',
    };
    const alertaResuelta: AlertaDeCalidad = {
      id: '31', moduleId: 'festivals', recordId: '23', flagType: 'territorio_incompleto',
      severity: 'alta', status: 'resuelta', detail: 'Falta municipio.',
      createdAt: '2026-09-10T10:00:00Z', updatedAt: '2026-09-10T10:00:00Z',
    };

    beforeEach(() => {
      adminService = TestBed.inject(AdminService);
      // `accionRapida()` recarga la bandeja al terminar -es lo correcto contra el servidor real-,
      // pero eso dispara la cascada de peticiones del servicio. Aquí importa solo qué endpoint
      // decide, no que la bandeja se repinte, así que se apaga.
      spyOn(bandeja, 'recargar');
      // YA NO HACE FALTA APAGAR NINGUN DIALOGO DEL NAVEGADOR: la confirmación vive en el panel,
      // así que una prueba que dispare un aviso sin preverlo ya no cuelga la corrida.
    });

    it('«Aprobar» sobre una revisión de un módulo genérico llama al endpoint de estado, sin pedir motivo', () => {
      const espia = spyOn(adminService, 'cambiarEstadoDeRegistro').and.returnValue(of({}));

      decidir({ kind: 'revision', rawId: '77', module: 'musicSchools' }, 'aprobar');

      // MUTANTE QUE MATA: pedir motivo también para aprobar. Aprobar es el camino sin nada que
      // discutir -por eso es un atajo-; si pidiera texto igual que rechazar, dejaría de ahorrar
      // el paso que se define ahorrar.
      expect(componente.pideMotivo()).toBeFalse();
      expect(espia).toHaveBeenCalledWith({ moduleId: 'musicSchools', id: '77', status: 'aprobado', comment: '' });
      expect(componente.accionRapidaMensaje()).toContain('aprobado');
      // Y SE DICE EN PANTALLA: el aviso flotante lo anuncia mientras la fila sale de la cola.
      expect(componente.avisoTemporal()).toContain('aprobado');
    });

    it('«Rechazar» sin escribir un motivo no llama a ningún endpoint', () => {
      const espia = spyOn(adminService, 'cambiarEstadoDeRegistro').and.returnValue(of({}));

      decidir({ kind: 'revision', rawId: '77', module: 'musicSchools' }, 'rechazar', '   ');

      expect(espia).not.toHaveBeenCalled();
    });

    it('«Rechazar» con motivo manda el estado correcto y el texto escrito', () => {
      const espia = spyOn(adminService, 'cambiarEstadoDeRegistro').and.returnValue(of({}));

      decidir({ kind: 'revision', rawId: '77', module: 'musicSchools' }, 'rechazar', 'Falta el soporte de la entidad organizadora.');

      expect(espia).toHaveBeenCalledWith({ moduleId: 'musicSchools', id: '77', status: 'rechazado', comment: 'Falta el soporte de la entidad organizadora.' });
    });

    it('una revisión de Festival NO llama al endpoint genérico: el servidor lo rechaza con 409', () => {
      // Contra el servidor real, `POST /admin/data/records/festivals/{id}/status` responde 409
      // siempre -"El estado de un Festival no se modifica desde la administración genérica"-.
      // Se vio en vivo al probar «Aprobar» sobre un Festival desde
      // esta misma bandeja.
      const generico = spyOn(adminService, 'cambiarEstadoDeRegistro').and.returnValue(of({}));
      const institucional = spyOn(adminService, 'decidirRevisionDeFestival').and.returnValue(of({}));

      decidir({ kind: 'revision', rawId: '77', module: 'festivals', record: 'Festival de prueba agosto' }, 'aprobar');

      expect(generico).not.toHaveBeenCalled();
      expect(institucional).toHaveBeenCalledWith(77, { accion: 'Publicar', observacion: null, motivoRechazo: null });
    });

    it('«Aprobar» sobre un Festival avisa de que publica en el sitio público, y si se cancela no llama a nada', () => {
      const institucional = spyOn(adminService, 'decidirRevisionDeFestival').and.returnValue(of({}));

      componente.accionRapida({ kind: 'revision', rawId: '77', module: 'festivals', record: 'Festival de prueba agosto' } as never, 'aprobar');

      // EL AVISO SE VE EN EL PANEL, no en una ventana del navegador, y dice qué va a pasar.
      expect(componente.avisoDeLaConfirmacion()).toContain('visible en el sitio público');
      expect(componente.tituloDeLaConfirmacion()).toContain('Festival de prueba agosto');
      expect(institucional).not.toHaveBeenCalled();

      componente.cancelarLaConfirmacion();

      expect(componente.confirmacion()).toBeNull();
      expect(institucional).not.toHaveBeenCalled();
    });

    it('«Rechazar» un Festival manda accion=Rechazar y el motivo en motivoRechazo, no en observacion', () => {
      const institucional = spyOn(adminService, 'decidirRevisionDeFestival').and.returnValue(of({}));

      decidir({ kind: 'revision', rawId: '77', module: 'festivals' }, 'rechazar', 'Falta información del territorio.');

      expect(institucional).toHaveBeenCalledWith(77, { accion: 'Rechazar', observacion: null, motivoRechazo: 'Falta información del territorio.' });
    });

    it('«Pedir ajustes» sobre un Festival manda accion=SolicitarAjustes y el motivo en observacion', () => {
      const institucional = spyOn(adminService, 'decidirRevisionDeFestival').and.returnValue(of({}));

      decidir({ kind: 'revision', rawId: '77', module: 'festivals' }, 'ajustes', 'Falta la fecha de la próxima edición.');

      expect(institucional).toHaveBeenCalledWith(77, { accion: 'SolicitarAjustes', observacion: 'Falta la fecha de la próxima edición.', motivoRechazo: null });
    });

    it('una propuesta de cambio decide por `decidirRevisionDePropuesta`, por el id de la propuesta y no el del Festival', () => {
      // LAS PROPUESTAS TIENEN SU PROPIO CIRCUITO: mismos verbos que un Festival directo, pero por
      // `propuestaId`. Confundir los dos ids decidiría sobre la propuesta equivocada, o sobre un
      // Festival que ni siquiera es el que se está mirando.
      const institucional = spyOn(adminService, 'decidirRevisionDePropuesta').and.returnValue(of({}));
      const festival = spyOn(adminService, 'decidirRevisionDeFestival').and.returnValue(of({}));

      decidir({ kind: 'propuesta', rawId: 3, module: 'festivals', record: 'Festival Prueba' }, 'aprobar');

      expect(institucional).toHaveBeenCalledWith(3, { accion: 'Publicar', observacion: null, motivoRechazo: null });
      expect(festival).not.toHaveBeenCalled();
      expect(componente.accionRapidaMensaje()).toContain('publicada');
    });

    it('«Aprobar» una propuesta avisa de que publica en el sitio público antes de hacerlo', () => {
      const institucional = spyOn(adminService, 'decidirRevisionDePropuesta').and.returnValue(of({}));

      componente.accionRapida({ kind: 'propuesta', rawId: 3, module: 'festivals', record: 'Festival Prueba' } as never, 'aprobar');

      expect(componente.avisoDeLaConfirmacion()).toContain('visible en el sitio público');
      expect(institucional).not.toHaveBeenCalled();
    });

    it('una solicitud de retiro aprobada dice que el registro quedó archivado', () => {
      const retiro: SolicitudDeVinculacion = {
        id: '9', moduleId: 'festivales_retiro', recordId: '77', requestingUserId: '12',
        entidadId: null, requestedScope: 'responsable', reason: 'Cierre definitivo',
        evidenceText: '', status: 'aprobada', reviewComment: '',
        createdAt: '2026-09-10T10:00:00Z', updatedAt: '2026-09-10T11:00:00Z',
        recordName: 'Festival de prueba', entidadNombre: null,
      };
      spyOn(adminService, 'decidirSolicitudDeVinculacion').and.returnValue(of(retiro));

      decidir({ kind: 'retiro', rawId: '9', module: 'festivales_retiro' }, 'aprobar');

      // MUTANTE QUE MATA: usar el mismo mensaje genérico de «Solicitud aprobada» para un retiro.
      // Un retiro aprobado archiva el Festival del lado del servidor -efecto real, no solo un
      // cambio de estado-, y quien decide necesita saber que eso fue lo que pasó.
      expect(componente.accionRapidaMensaje()).toContain('archivado');
    });

    it('una reclamación aprobada pasa por decidirReclamacionDeAdministracion, no por el endpoint de solicitudes', () => {
      const reclamacion = spyOn(adminService, 'decidirReclamacionDeAdministracion').and.returnValue(EMPTY);
      const link = spyOn(adminService, 'decidirSolicitudDeVinculacion').and.returnValue(EMPTY);

      decidir({ kind: 'reclamacion', rawId: '5', module: 'Festival' }, 'aprobar');

      expect(reclamacion).toHaveBeenCalledWith('5', 'aprobar', '');
      expect(link).not.toHaveBeenCalled();
    });

    it('«Aprobar» una reclamación avisa de que abre el camino a transferir la administración', () => {
      const espia = spyOn(adminService, 'decidirReclamacionDeAdministracion').and.returnValue(EMPTY);

      componente.accionRapida({ kind: 'reclamacion', rawId: '5', module: 'Festival' } as never, 'aprobar');

      expect(componente.avisoDeLaConfirmacion()).toContain('transferir');
      expect(espia).not.toHaveBeenCalled();
    });

    it('«Pedir aclaración» sobre una reclamación llama a pedirAclaracionDeReclamacion y no a decidirReclamacionDeAdministracion', () => {
      const aclaracion = spyOn(adminService, 'pedirAclaracionDeReclamacion').and.returnValue(EMPTY);
      const reclamacion = spyOn(adminService, 'decidirReclamacionDeAdministracion').and.returnValue(EMPTY);

      decidir({ kind: 'reclamacion', rawId: '5', module: 'Festival' }, 'aclaracion', '¿Cuál es tu vínculo con el Festival?');

      expect(aclaracion).toHaveBeenCalledWith('5', '¿Cuál es tu vínculo con el Festival?');
      expect(reclamacion).not.toHaveBeenCalled();
    });

    it('si el servidor rechaza la decisión, se muestra el error y no un mensaje de éxito', () => {
      spyOn(adminService, 'cambiarEstadoDeRegistro').and.returnValue(throwError(() => ({ message: 'El registro ya no está en revisión.' })));

      decidir({ kind: 'revision', rawId: '77', module: 'musicSchools' }, 'aprobar');

      expect(componente.accionRapidaError()).toBe('El registro ya no está en revisión.');
      expect(componente.accionRapidaMensaje()).toBe('');
      // Y NO SE ANUNCIA COMO SI HUBIERA IDO BIEN: el aviso flotante es solo para lo que terminó.
      // Lo que se pinta es `accionRapidaError()`, junto a los botones de la decisión —la cola de
      // esta prueba está vacía, así que ese panel no existe aquí; se comprobó en navegador—.
      expect(componente.avisoTemporal()).toBe('');
    });

    it('una decisión que termina bien devuelve el panel al resumen, para no dejarlo sobre la ficha de un asunto ya decidido', () => {
      componente.vistaDelTramite.set('ficha');
      spyOn(adminService, 'cambiarEstadoDeRegistro').and.returnValue(of({}));

      decidir({ kind: 'revision', rawId: '77', module: 'musicSchools' }, 'aprobar');

      expect(componente.vistaDelTramite()).toBe('resumen');
    });

    /*
      UN EVENTO SE DECIDE COMO TODO LO DEMAS. Hasta tenía su propio
      camino dentro de la misma fila de acciones: publicaba sin preguntar y pedía «Qué hay que
      corregir» en un campo siempre visible, antes de que nadie hubiera decidido devolverlo.
    */
    it('publicar un evento pasa por la confirmación y dice que quedará visible en la Agenda', () => {
      const espia = spyOn(adminService, 'decidirSobreEvento').and.returnValue(of({}) as never);

      componente.accionRapida({ kind: 'evento', rawId: '31', module: 'agenda', record: 'Concierto de clausura' } as never, 'aprobar' as never);

      // MUTANTE QUE MATA: ejecutar al pulsar, que es lo que hacía. Publicar mete el evento en la
      // Agenda pública y desde aquí no hay deshacer.
      expect(espia).not.toHaveBeenCalled();
      expect(componente.tituloDeLaConfirmacion()).toContain('Publicar «Concierto de clausura»');
      expect(componente.avisoDeLaConfirmacion()).toContain('Agenda pública');
      expect(componente.pideMotivo()).toBeFalse();

      componente.confirmarLaAccion();

      expect(espia).toHaveBeenCalledWith('31', 'publicar', undefined);
      expect(componente.accionRapidaMensaje()).toContain('publicado');
    });

    it('devolver un evento exige decir qué hay que corregir, y eso es lo que viaja', () => {
      const espia = spyOn(adminService, 'decidirSobreEvento').and.returnValue(of({}) as never);

      componente.accionRapida({ kind: 'evento', rawId: '31', module: 'agenda', record: 'Concierto de clausura' } as never, 'rechazar' as never);
      expect(componente.pideMotivo()).toBeTrue();

      // Sin texto no se ejecuta: el evento volvería igual y nadie sabría qué arreglar.
      componente.confirmarLaAccion();
      expect(espia).not.toHaveBeenCalled();

      componente.motivoDeLaDecision = 'Falta el lugar del encuentro.';
      componente.confirmarLaAccion();

      expect(espia).toHaveBeenCalledWith('31', 'devolver', 'Falta el lugar del encuentro.');
    });

    /*
      LOS DOS HALLAZGOS QUE NADIE PIDIO, decididos desde la misma bandeja.

      Estas cuatro pruebas vienen de `gobernanza-con-contratos.spec.ts`, que vigilaba el panel de
      «Calidad y coincidencias». Ese panel se retiró -tres de sus cinco
      pestañas listaban los mismos trámites que esta bandeja, leídos de los mismos endpoints- y sus
      dos pestañas propias entraron aquí. Lo que vigilaban NO se retira con él: los valores
      canónicos que acepta el servidor -`fusionar`, `no_duplicado`, `resuelta`- son los mismos, y
      escribir otro deja la fila cambiada en pantalla y sin cambiar en la base.
    */
    it('«Marcar para fusión» envía la decisión canónica del contrato de duplicados', () => {
      const espia = spyOn(adminService, 'decidirPosibleDuplicado').and.returnValue(of(duplicadoDecidido));

      decidir({ kind: 'duplicado', rawId: '21', module: 'festivals', record: 'Registros 23 y 24' }, 'fusionar');

      expect(espia).toHaveBeenCalledWith({ id: '21', decision: 'fusionar' });
    });

    it('«Son registros distintos» envía `no_duplicado` y no pide ningún texto', () => {
      const espia = spyOn(adminService, 'decidirPosibleDuplicado').and.returnValue(of(duplicadoDecidido));

      decidir({ kind: 'duplicado', rawId: '21', module: 'festivals' }, 'ignorar');

      expect(espia).toHaveBeenCalledWith({ id: '21', decision: 'no_duplicado' });
    });

    it('marcar para fusión avisa antes de que la decisión NO combina los registros, y sin confirmar no llama al servidor', () => {
      // La frontera del contrato: registra el juicio, no fusiona. Si eso deja de decirse, la
      // pantalla promete una fusión que el servidor no hace.
      const espia = spyOn(adminService, 'decidirPosibleDuplicado').and.returnValue(of(duplicadoDecidido));

      componente.accionRapida({ kind: 'duplicado', rawId: '21', module: 'festivals' } as never, 'fusionar');

      expect(componente.avisoDeLaConfirmacion()).toContain('no se combinan');
      expect(espia).not.toHaveBeenCalled();
      // Y el bloqueo no se levanta por abrir la confirmación: nada se ha enviado todavía.
      expect(componente.accionRapidaOcupada()).toBeFalse();
    });

    it('resolver una alerta usa el estado femenino que admite el contrato de calidad', () => {
      const espia = spyOn(adminService, 'decidirAlertaDeCalidad').and.returnValue(of(alertaResuelta));

      decidir({ kind: 'alerta', rawId: '31', module: 'festivals' }, 'resolver');

      expect(espia).toHaveBeenCalledWith({ id: '31', status: 'resuelta' });
    });

    it('una segunda acción mientras la primera está en curso no dispara un segundo endpoint', () => {
      // `of(...)` resuelve sincrónicamente, así que para observar el estado "ocupado" a mitad de
      // camino hay que devolver un observable que no resuelva solo.
      const espia = spyOn(adminService, 'cambiarEstadoDeRegistro').and.returnValue({ subscribe: () => undefined } as any);

      decidir({ kind: 'revision', rawId: '77', module: 'musicSchools' }, 'aprobar');
      decidir({ kind: 'revision', rawId: '77', module: 'musicSchools' }, 'aprobar');

      expect(espia).toHaveBeenCalledTimes(1);
    });
  });
});

/**
 * La bandeja se ordena ENTERA por el criterio elegido, y viene con lo más antiguo primero.
 *
 * <b>EL DEFECTO QUE TRAJO ESTA PRUEBA.</b> La cola concatenaba ocho listas en el orden en que
 * llegaban del servidor y no había ningún criterio: el criterio pide el 15 de
 * septiembre de 2026 «criterios de organización: por fecha, primera fecha de llegada, última fecha
 * de llegada, alfabéticamente». Y sobre los nombres: «no sé qué propuestas, qué es… no sé qué es
 * solicitudes versus reclamaciones».
 */
describe('AdminSolicitudesPanelComponent · orden y nombres de la bandeja', () => {
  let fixture: ComponentFixture<AdminSolicitudesPanelComponent>;
  let componente: AdminSolicitudesPanelComponent;
  let bandeja: BandejaDeTrabajoService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [AdminSolicitudesPanelComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    fixture = TestBed.createComponent(AdminSolicitudesPanelComponent);
    componente = fixture.componentInstance;
    bandeja = TestBed.inject(BandejaDeTrabajoService);
    http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();

    // Tres reclamaciones con antigüedades y nombres deliberadamente cruzados: el orden de llegada,
    // el alfabético del registro y el alfabético de la organización no coinciden entre sí.
    bandeja.reclamaciones.set([
      { id: 'b', estado: 'enviada', registroNombre: 'Bandola', organizacionSolicitanteNombre: 'Zeta', fechaEnvio: '2026-09-14T10:00:00Z' },
      { id: 'a', estado: 'enviada', registroNombre: 'Arpa', organizacionSolicitanteNombre: 'Marimba', fechaEnvio: '2026-09-10T10:00:00Z' },
      { id: 'c', estado: 'enviada', registroNombre: 'Cuatro', organizacionSolicitanteNombre: 'Alfa', fechaEnvio: '2026-09-15T10:00:00Z' },
    ]);
  });

  afterEach(() => http.verify());

  const registros = () => componente.colaVisible().map(item => item.record);

  it('viene con lo más antiguo primero: es el orden de trabajo de una bandeja', () => {
    expect(componente.ordenDeLaBandeja()).toBe('antiguos');
    expect(registros()).toEqual(['Arpa', 'Bandola', 'Cuatro']);
  });

  it('cambiar el criterio reordena la cola entera', () => {
    componente.ordenDeLaBandeja.set('recientes');
    expect(registros()).toEqual(['Cuatro', 'Bandola', 'Arpa']);
    componente.ordenDeLaBandeja.set('registro');
    expect(registros()).toEqual(['Arpa', 'Bandola', 'Cuatro']);
    componente.ordenDeLaBandeja.set('organizacion');
    expect(componente.colaVisible().map(item => item.organization)).toEqual(['Alfa', 'Marimba', 'Zeta']);
  });

  it('las posiciones dicen qué contienen y la elegida se explica en una línea', () => {
    const etiquetas = componente.filtrosDeLaBandeja().map(f => f.etiqueta);
    expect(etiquetas).toContain('Registros nuevos');
    expect(etiquetas).toContain('Cambios a lo publicado');
    expect(etiquetas).toContain('Vinculaciones');
    expect(etiquetas).toContain('Reclamos de administración');
    // Los identificadores no cambian: los usan los enlaces profundos y las acciones de la cabecera.
    expect(componente.filtrosDeLaBandeja().map(f => f.id)).toContain('propuesta');

    // Cada posición se explica en una línea, salvo «Todo»: la cabecera de la sección ya lo dice y
    // repetirlo era decir lo mismo dos veces («ese texto no va», 15 de septiembre de 2026).
    for (const filtro of componente.filtrosDeLaBandeja()) {
      componente.cambiarFiltroDeBandeja(filtro.id);
      if (filtro.id === 'todos') {
        expect(componente.descripcionDelFiltro()).toBe('');
      } else {
        expect(componente.descripcionDelFiltro().length).withContext(filtro.id).toBeGreaterThan(20);
      }
    }
  });
});
