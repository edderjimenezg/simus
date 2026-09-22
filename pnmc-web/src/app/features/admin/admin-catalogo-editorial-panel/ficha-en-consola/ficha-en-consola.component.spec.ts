import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FichaEnConsolaComponent } from './ficha-en-consola.component';
import { AnotacionEditorial, CatalogoEditorialService, PublicacionEditorial } from '../../../../core/services/catalogo-editorial.service';

/**
 * La ficha del Catálogo Editorial en la consola.
 *
 * <b>QUE FIJAN ESTAS PRUEBAS.</b> Que la versión de trabajo enseña lo que la pública NO —el estado,
 * la confianza, quién verificó los derechos, la procedencia y el hilo de revisiones—, y que dice
 * qué le falta a una ficha para verse en el portal ANTES de que alguien pulse publicar y se lo
 * rechacen. Y que las decisiones y las anotaciones se leen en un mismo hilo.
 */
class ApiDoble {
  historialDevuelto: AnotacionEditorial[] = [];
  anotado: { id: number; comentario: string }[] = [];
  historial = (id: number) => { void id; return Promise.resolve({ ok: true, data: this.historialDevuelto }); };
  anotar = (id: number, comentario: string) => {
    this.anotado.push({ id, comentario });
    return Promise.resolve({ ok: true, data: undefined });
  };
}

function publicacion(cambios: Partial<PublicacionEditorial> = {}): PublicacionEditorial {
  return {
    id: 4, codigo: 'PNMC-ED-004', titulo: 'Música Coral Colombiana', subtitulo: null,
    designacionVolumen: null, serieOColeccion: null, resumen: null, fechaEdtf: null,
    anioInicio: 2001, anioFin: null, notaFecha: null, idioma: 'es', tipoPublicacion: 'Libro',
    categoriaId: null, categoria: null, ambito: 'Nacional', ambitoTexto: null, formato: 'Físico',
    categoriaSecundaria: null, palabrasClave: [], miniaturaRuta: null,
    seccionPrincipal: 'Repertorio', rutaSeccion: null, practicaMusical: 'Coro',
    subcategoria: null, tamanoFormato: null, paginas: null, duracion: null,
    camposAdicionales: null, textoPortada: null, tipologia: [], confianza: 'Alta',
    revisarClasificacion: false, revisarCreditos: true, notasCatalogacion: null,
    diapositivaOrigen: '9', estadoCatalogacion: 'validada', estadoPublicacion: 'publicado',
    version: 1,
    derechos: { estado: 'verificado', permitePublicarFicha: true, permitePublicarArchivo: false,
      licenciaONota: null, fuenteId: 1, fechaVerificacion: null, verificadoPor: 'Webmaster PNMC' },
    fuentes: [{ id: 1, nombre: 'Catálogo del Proyecto Editorial del PNMC', referencia: null,
      url: null, fechaFuente: null, fechaConsulta: '2026-09-13T00:00:00Z', verificadaPor: 'Webmaster PNMC' }],
    creditos: [], identificadores: [], accesos: [], programas: [],
    practicasMusicales: [], territoriosSonoros: [], procedencia: null,
    fechaActualizacion: '2026-09-13T00:00:00Z',
    ...cambios,
  } as PublicacionEditorial;
}

describe('FichaEnConsolaComponent', () => {
  let fixture: ComponentFixture<FichaEnConsolaComponent>;
  let api: ApiDoble;

  const montar = (ficha: PublicacionEditorial) => {
    fixture = TestBed.createComponent(FichaEnConsolaComponent);
    fixture.componentRef.setInput('publicacion', ficha);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  };

  beforeEach(async () => {
    api = new ApiDoble();
    await TestBed.configureTestingModule({
      imports: [FichaEnConsolaComponent],
      providers: [{ provide: CatalogoEditorialService, useValue: api }],
    }).compileComponents();
  });

  it('enseña el trabajo de catalogación, que el portal no recibe', () => {
    const raiz = montar(publicacion());
    const texto = raiz.textContent!.replace(/\s+/g, ' ');

    expect(texto).toContain('Confianza de la ficha');
    expect(texto).toContain('Alta');
    expect(texto).toContain('Revisar créditos');
    expect(texto).toContain('Lámina del catálogo original');
    // Y QUIEN VERIFICO LOS DERECHOS, que es lo que el criterio de biblioteca deja fuera del portal.
    expect(texto).toContain('Webmaster PNMC');
  });

  it('dice qué le falta a la ficha para verse, antes de que nadie pulse publicar', () => {
    const raiz = montar(publicacion({
      estadoCatalogacion: 'pendiente_revision',
      estadoPublicacion: 'borrador',
      fuentes: [],
      derechos: { estado: 'pendiente', permitePublicarFicha: false, permitePublicarArchivo: false,
        licenciaONota: null, fuenteId: null, fechaVerificacion: null, verificadoPor: null },
    }));
    const texto = raiz.textContent!.replace(/\s+/g, ' ');

    // LAS TRES QUE FALTAN, ENUMERADAS: es lo que convierte «no se puede publicar» en una lista de
    // tareas en vez de en un rechazo del servidor.
    expect(texto).toContain('Todavía no se puede ver en el portal');
    expect(texto).toContain('La ficha todavía no está validada.');
    expect(texto).toContain('No cita ninguna fuente.');
    expect(texto).toContain('Los derechos no permiten publicar la ficha.');
  });

  it('cuando cumple las cuatro condiciones lo dice, y no inventa un aviso', () => {
    const raiz = montar(publicacion());
    expect(raiz.textContent).toContain('Esta ficha se está viendo en el portal.');
  });

  it('lee las decisiones y las anotaciones en un mismo hilo, con su verbo en letra', async () => {
    api.historialDevuelto = [
      { id: 2, accion: 'Anotacion', estadoAnterior: 'validada', estadoNuevo: 'validada',
        comentario: 'Falta el arreglista.', usuarioId: 1, usuarioNombre: 'Webmaster PNMC', fecha: '2026-09-13T10:00:00Z' },
      { id: 1, accion: 'CatalogacionCambiada', estadoAnterior: 'pendiente_revision', estadoNuevo: 'validada',
        comentario: 'Contrastada con el ejemplar.', usuarioId: 1, usuarioNombre: 'Webmaster PNMC', fecha: '2026-09-13T09:00:00Z' },
    ];
    const raiz = montar(publicacion());
    // EL HILO SE PIDE AL ABRIR Y LLEGA DESPUES: sin esperar, la pantalla sigue en «Consultando…».
    await fixture.whenStable();
    fixture.detectChanges();
    const texto = raiz.textContent!.replace(/\s+/g, ' ');

    expect(texto).toContain('Anotación');
    // EL VERBO EN LETRA Y NO EN CLAVE: «CatalogacionCambiada» no se lo dice a nadie.
    expect(texto).toContain('Catalogación · Validada');
    expect(texto).toContain('Falta el arreglista.');
    expect(texto).toContain('Contrastada con el ejemplar.');
  });

  it('no guarda una anotación en blanco', async () => {
    montar(publicacion());
    fixture.componentInstance.anotacion.set('   ');

    await fixture.componentInstance.guardarAnotacion();

    // UNA ENTRADA VACIA NO LE DICE NADA A QUIEN LA LEA DESPUES.
    expect(api.anotado.length).toBe(0);
  });

  it('guarda la anotación y limpia el campo, para que no se envíe dos veces', async () => {
    montar(publicacion());
    fixture.componentInstance.anotacion.set('  Revisar los créditos.  ');

    await fixture.componentInstance.guardarAnotacion();

    expect(api.anotado).toEqual([{ id: 4, comentario: 'Revisar los créditos.' }]);
    expect(fixture.componentInstance.anotacion()).toBe('');
  });
});
