import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { EdicionesDeMercadoComponent } from './ediciones-de-mercado.component';
import {
  EdicionDeMercadoDeLaOrganizacion,
  GuardarEdicionDeMercadoSolicitud,
  PanelOrganizacionApi,
} from './panel-organizacion.api';

/**
 * Las ediciones de un mercado, desde el panel de su organización.
 *
 * <b>LA RUTA EXISTIA DESDE EL PRIMER CORTE DEL MODULO y solo la usaba la consola:</b> quien
 * registraba un mercado no podía anunciar ninguna de sus realizaciones sin que el Programa se la
 * creara. Lo que se pinza aquí es lo que distingue esta pantalla de una lista cualquiera: que la
 * regla del servidor —solo sobre un mercado publicado— se diga ANTES de pulsar, que los dos ejes de
 * estado no se confundan, y que la negativa del servidor llegue con su motivo.
 */
describe('EdicionesDeMercadoComponent', () => {
  let fixture: ComponentFixture<EdicionesDeMercadoComponent>;
  let componente: EdicionesDeMercadoComponent;
  let creado: { mercadoId: string; solicitud: GuardarEdicionDeMercadoSolicitud } | null;
  let guardado: { mercadoId: string; edicionId: number; solicitud: GuardarEdicionDeMercadoSolicitud } | null;
  let falloAlCrear: unknown = null;
  let visibilidadCambiada: { mercadoId: string; edicionId: number; accion: string } | null;
  let eliminada: { mercadoId: string; edicionId: number } | null;

  const edicion: EdicionDeMercadoDeLaOrganizacion = {
    id: 7, mercadoId: 4, anio: 2026, numeroEdicion: 2, nombre: null, descripcion: 'La segunda rueda.',
    fechaInicio: '2026-05-10', fechaFin: '2026-05-14', codigoDepartamento: '91', nombreDepartamento: 'Amazonas',
    codigoMunicipio: null, nombreMunicipio: null, lugarEspecifico: 'Malecón',
    estado: 'programada', estadoVisibilidad: 'borrador',
    fechaCreacion: '2026-01-01T00:00:00Z', fechaActualizacion: null, fechaPublicacion: null,
  };

  function montar(publicado: boolean, ediciones: EdicionDeMercadoDeLaOrganizacion[] = [edicion]): void {
    creado = null; guardado = null; visibilidadCambiada = null; eliminada = null;
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [EdicionesDeMercadoComponent],
      providers: [{ provide: PanelOrganizacionApi, useValue: {
        obtenerEdicionesDeMercado: () => of(ediciones),
        crearEdicionDeMercado: (mercadoId: string, solicitud: GuardarEdicionDeMercadoSolicitud) => {
          creado = { mercadoId, solicitud };
          return falloAlCrear ? throwError(() => falloAlCrear) : of({ ...edicion, ...solicitud, id: 8 });
        },
        guardarEdicionDeMercado: (mercadoId: string, edicionId: number, solicitud: GuardarEdicionDeMercadoSolicitud) => {
          guardado = { mercadoId, edicionId, solicitud };
          return of({ ...edicion, ...solicitud });
        },
        cambiarVisibilidadDeEdicionDeMercado: (mercadoId: string, edicionId: number, accion: string) => {
          visibilidadCambiada = { mercadoId, edicionId, accion };
          return of({ ...edicion, estadoVisibilidad: accion === 'publicar' ? 'publicado' : 'borrador' });
        },
        eliminarEdicionDeMercado: (mercadoId: string, edicionId: number) => {
          eliminada = { mercadoId, edicionId };
          return of(undefined);
        },
      } }],
    });
    fixture = TestBed.createComponent(EdicionesDeMercadoComponent);
    componente = fixture.componentInstance;
    fixture.componentRef.setInput('mercadoId', '4');
    fixture.componentRef.setInput('mercadoPublicado', publicado);
    componente.ngOnChanges();
    fixture.detectChanges();
  }

  it('sobre un mercado sin publicar dice la regla y no ofrece registrar', () => {
    // MUTANTE QUE MATA: ofrecer el botón igual. El servidor responde 409 y quien escribió la
    // edición entera pierde el trabajo por una regla que se podía decir antes.
    montar(false);

    expect(fixture.nativeElement.querySelector('[data-aun-sin-publicar]').textContent).toContain('ya está publicado');
    expect(fixture.nativeElement.querySelector('[data-nueva-edicion]')).toBeNull();
  });

  it('sobre un mercado publicado lista sus ediciones con los dos ejes de estado separados', () => {
    montar(true);

    expect(componente.ediciones().length).toBe(1);
    expect(fixture.nativeElement.querySelector('[data-nueva-edicion]')).not.toBeNull();
    // La visibilidad decide si el portal la enseña; la realización describe el acontecimiento.
    expect(componente.etiquetaDeVisibilidad('borrador')).toBe('Borrador');
    expect(componente.etiquetaDeRealizacion('programada')).toBe('Programada');
    // Sin nombre propio, la edición se identifica por su número y, si no, por su año.
    expect(componente.nombreDe(edicion)).toBe('Edición 2');
    expect(componente.nombreDe({ ...edicion, numeroEdicion: null })).toBe('Edición 2026');
  });

  it('el ciclo de visibilidad se ofrece según dónde esté la edición', () => {
    // ARCHIVADA NO OFRECE NADA: reabrir lo archivado sin dejar constancia es peor que no poder
    // reabrirlo. Y ELIMINAR SOLO SOBRE UN BORRADOR: lo que se publicó alguna vez se archiva.
    montar(true);

    expect(componente.accionesDe(edicion).map(a => a.id)).toEqual(['editar', 'publicar', 'archivar', 'eliminar']);
    expect(componente.accionesDe({ ...edicion, estadoVisibilidad: 'publicado' }).map(a => a.id))
      .toEqual(['editar', 'despublicar', 'archivar']);
    expect(componente.accionesDe({ ...edicion, estadoVisibilidad: 'archivado' })).toEqual([]);
  });

  it('publicar una edición usa su propia puerta, no un guardado con otro estado', () => {
    // MUTANTE QUE MATA: volver a reenviar la edición entera con otro `estadoVisibilidad`. Funciona,
    // pero se salta las reglas del servidor —publicar la edición de un mercado que no está
    // publicado, reabrir una archivada— y queda en la bitácora como «guardar», así que el historial
    // no puede decir qué pasó.
    montar(true);

    componente.ejecutarAccion(edicion, 'publicar');

    expect(visibilidadCambiada).toEqual({ mercadoId: '4', edicionId: 7, accion: 'publicar' });
    expect(guardado).toBeNull();
    expect(componente.aviso()).toContain('publicada en el portal');
  });

  it('eliminar pregunta antes, y solo borra cuando se confirma', () => {
    // ES LA UNICA ACCION DE ESTA PANTALLA QUE NO SE DESHACE.
    montar(true);

    componente.ejecutarAccion(edicion, 'eliminar');
    expect(eliminada).toBeNull();
    expect(componente.eliminacionPedida()).not.toBeNull();

    componente.eliminarLaEdicion();
    expect(eliminada).toEqual({ mercadoId: '4', edicionId: 7 });
    expect(componente.eliminacionPedida()).toBeNull();
  });

  it('una edición nueva nace en borrador y con el año en curso', () => {
    montar(true);

    componente.nueva();
    componente.guardar();

    expect(creado!.mercadoId).toBe('4');
    expect(creado!.solicitud.anio).toBe(new Date().getFullYear());
    expect(creado!.solicitud.estadoVisibilidad).toBe('borrador');
    expect(creado!.solicitud.estado).toBe('en_preparacion');
  });

  it('si el servidor se niega, se dice con su motivo y el formulario no se pierde', () => {
    // «Este mercado ya tiene una edición de 2026» dice qué hacer; «no fue posible» manda a adivinar.
    falloAlCrear = { message: 'genérico', payload: { message: 'Este mercado ya tiene una edición de 2026. Abre esa y corrígela en vez de crear otra.' } };
    montar(true);

    componente.nueva();
    componente.guardar();

    expect(componente.error()).toContain('ya tiene una edición de 2026');
    expect(componente.formulario()).not.toBeNull();
    falloAlCrear = null;
  });
});
