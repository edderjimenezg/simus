import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { AdminNoticiasPanelComponent } from './admin-noticias-panel.component';
import { Noticia } from '../../../core/services/noticias.service';

/**
 * Noticias en la consola.
 *
 * <b>LO QUE ESTAS PRUEBAS IMPIDEN.</b> Que «publicada» y «visible» vuelvan a confundirse. Una
 * noticia fechada el lunes queda publicada hoy y NO aparece en el portal hasta el lunes; sin el
 * aviso de la lista, el «la publiqué y no sale» es seguro, y acaba en alguien publicándola otra
 * vez con la fecha de hoy.
 */
describe('el panel de Noticias distingue publicada de visible', () => {
  function crear() {
    TestBed.configureTestingModule({
      imports: [AdminNoticiasPanelComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    const fixture = TestBed.createComponent(AdminNoticiasPanelComponent);
    return fixture.componentInstance;
  }

  function noticia(parcial: Partial<Noticia>): Noticia {
    const base = {
      id: 1, slug: 'una-noticia', titulo: 'Una noticia', resumen: 'Resumen', cuerpo: 'Cuerpo',
      fechaPublicacion: null as string | null, imagenRuta: null, imagenAlternativa: null, autoriaNombre: null,
      imagenArchivoId: null, imagenUrl: null, imagenAlt: null,
      estado: 'borrador', version: 1, etiquetas: [], categoriaId: null, categoria: null,
      practicasMusicales: [], territoriosSonoros: [], procedencia: null, proyectosTransversales: [], fechaActualizacion: '2026-09-11T00:00:00Z',
      ...parcial,
    };
    // EL ESTADO EFECTIVO LO CALCULA EL SERVIDOR; aquí se reproduce su regla para que el fijador
    // no pueda inventar una combinación que el API nunca devuelve —«publicado» con fecha futura y
    // efectivo «publicado»—, que es exactamente el defecto que esta pantalla vino a cerrar.
    const programada = base.estado === 'publicado'
      && base.fechaPublicacion !== null
      && base.fechaPublicacion > new Date().toISOString().slice(0, 10);
    return { ...base, estadoEfectivo: parcial.estadoEfectivo ?? (programada ? 'programada' : base.estado) } as Noticia;
  }

  const hoy = () => new Date().toISOString().slice(0, 10);
  const enDias = (dias: number) => new Date(Date.now() + dias * 864e5).toISOString().slice(0, 10);

  it('avisa de que una noticia publicada con fecha futura aún no se ve', () => {
    const componente = crear();

    expect(componente.esperandoSuFecha(noticia({ estado: 'publicado', fechaPublicacion: enDias(7) }))).toBe(true);
  });

  it('no avisa cuando la fecha ya llegó', () => {
    const componente = crear();

    expect(componente.esperandoSuFecha(noticia({ estado: 'publicado', fechaPublicacion: hoy() }))).toBe(false);
    expect(componente.esperandoSuFecha(noticia({ estado: 'publicado', fechaPublicacion: enDias(-3) }))).toBe(false);
  });

  it('no avisa de un borrador, por futura que sea su fecha', () => {
    const componente = crear();

    // UN BORRADOR NO ESTA ESPERANDO SU FECHA: está esperando una decisión, que es otra cosa.
    expect(componente.esperandoSuFecha(noticia({ estado: 'borrador', fechaPublicacion: enDias(7) }))).toBe(false);
  });

  it('una noticia programada ofrece previsualizar y NO «Ver en el portal»', () => {
    const componente = crear();
    const programada = noticia({ estado: 'publicado', fechaPublicacion: enDias(7) });

    const acciones = componente.accionesDe(programada).map(accion => accion.id);

    // POR QUE EXISTE: la noticia está guardada como publicada, así que la consola ofrecía
    // «Ver en el portal» y ese enlace devolvía un 404 —la lectura pública no la enseña hasta su
    // fecha—. Una acción que lleva a una vista vacía es justo lo que el proyecto no admite.
    expect(acciones).not.toContain('ver');
    expect(acciones).toContain('previsualizar');
  });

  it('una noticia que ya se ve ofrece abrirla, y no previsualizarla', () => {
    const componente = crear();
    const publicada = noticia({ estado: 'publicado', fechaPublicacion: hoy() });

    const acciones = componente.accionesDe(publicada).map(accion => accion.id);

    // OFRECER LAS DOS obligaría a elegir entre dos acciones que hacen casi lo mismo, y la buena
    // —abrir la noticia de verdad— quedaría escondida detrás de la otra.
    expect(acciones).toContain('ver');
    expect(acciones).not.toContain('previsualizar');
  });

  it('un borrador se puede previsualizar antes de existir para nadie', () => {
    const componente = crear();

    expect(componente.accionesDe(noticia({ estado: 'borrador' })).map(a => a.id)).toContain('previsualizar');
  });
});
