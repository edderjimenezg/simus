import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';

import { appConfig } from '../../../app.config';
import { AdminFichaMercadoComponent, FichaMercadoAdministrativa } from './admin-ficha-mercado.component';

/**
 * La ficha de un mercado en la consola.
 *
 * <b>LO QUE ESTAS PRUEBAS IMPIDEN.</b> Que un mercado vuelva a ser el proceso del Ecosistema que
 * solo se puede abrir para editarlo. Un festival se abre, se lee entero, se publica, se retira y se
 * archiva desde su ficha; un mercado no tenía ficha. Se fija lo que hace que las dos se recorran
 * igual: que las acciones dependan del estado y del permiso, que el historial y la procedencia
 * estén, y que los dos ejes de estado de una edición no se confundan.
 */
describe('la ficha administrativa de un mercado', () => {
  let fixture: ComponentFixture<AdminFichaMercadoComponent>;
  let http: HttpTestingController;

  const FICHA: FichaMercadoAdministrativa = {
    id: '4',
    nombre: 'Mercado Musical del Amazonas',
    descripcion: 'Rueda de negocios del sur.',
    estado: 'borrador',
    nivelCobertura: 'departamental',
    departamento: 'Amazonas',
    municipio: null,
    lugarEspecifico: null,
    periodicidad: 'anual',
    alcance: 'Nacional',
    modalidad: 'Presencial',
    correoContacto: 'contacto@mercado.test',
    telefono: null,
    sitioWeb: null,
    organizacionResponsableId: 3104,
    organizacionResponsableNombre: 'Corporación Musical de Amazonas',
    seRealizaEnElMarcoDeUnFestival: true,
    festivalId: 12,
    festivalNombre: 'Festival de Trova',
    procedencia: null,
    practicasMusicales: [],
    territoriosSonoros: [],
    ediciones: [
      {
        id: '7', anio: 2026, numeroEdicion: 2, nombre: null,
        fechaInicio: '2026-05-10', fechaFin: '2026-05-14',
        estado: 'programada', estadoVisibilidad: 'borrador', procedencia: null,
      },
    ],
    fechaCreacion: '2026-09-01T10:00:00Z',
    fechaActualizacion: null,
  };

  // `formatDate` necesita los datos de es-CO, y los registra `appConfig`.
  beforeAll(() => expect(appConfig.providers.length).toBeGreaterThan(0));

  async function montar(ficha: FichaMercadoAdministrativa = FICHA, puedePublicar = true): Promise<void> {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [AdminFichaMercadoComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    fixture = TestBed.createComponent(AdminFichaMercadoComponent);
    http = TestBed.inject(HttpTestingController);
    fixture.componentRef.setInput('mercadoId', ficha.id);
    fixture.componentRef.setInput('puedePublicar', puedePublicar);
    fixture.detectChanges();
    http.expectOne(`/api/v1/admin/mercados/${ficha.id}`).flush(ficha);
    // LA CARGA ES `async`: sin esperar aquí la plantilla se comprueba todavía en «cargando».
    await fixture.whenStable();
    fixture.detectChanges();
  }

  afterEach(() => http?.verify());

  it('sin publicar ofrece publicar y previsualizar, no despublicar', async () => {
    // MUTANTE QUE MATA: ofrecer las tres siempre. «Despublicar» sobre un borrador es una acción que
    // no aplica al estado, y este proyecto no pinta acciones que no aplican.
    await montar();
    const texto = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(texto).toContain('Publicar');
    expect(texto).toContain('Previsualizar');
    expect(texto).not.toContain('Despublicar');
    expect(texto).not.toContain('Ver en el portal');
  });

  it('publicado ofrece retirarlo, archivarlo y verlo en el portal', async () => {
    await montar({ ...FICHA, estado: 'publicado' });
    const raiz = fixture.nativeElement as HTMLElement;

    expect(raiz.textContent).toContain('Despublicar');
    expect(raiz.textContent).toContain('Archivar');
    const enlace = Array.from(raiz.querySelectorAll('a')).find(a => a.textContent?.includes('Ver en el portal'));
    expect(enlace?.getAttribute('href')).toBe('/ecosistema/mercados-musicales/4');
  });

  it('quien no puede publicar no ve ninguna de las tres, pero sí la lectura', async () => {
    // El servidor lo comprueba igual; esto evita ofrecer un botón que va a devolver un 403.
    await montar(FICHA, false);
    const texto = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(texto).not.toContain('Publicar');
    expect(texto).toContain('Ver historial');
    expect(texto).toContain('Procedencia');
  });

  it('publicar manda el estado y vuelve a leer la ficha', async () => {
    await montar();

    const publicando = fixture.componentInstance.cambiarPublicacion('publicado');
    const peticion = http.expectOne('/api/v1/admin/mercados/4/publicacion');
    expect(peticion.request.body).toEqual({ estado: 'publicado' });
    peticion.flush({ id: '4', estado: 'publicado' });
    // LA RELECTURA OCURRE EN UNA MICROTAREA POSTERIOR al `flush`: sin esperar aquí, la petición
    // todavía no se ha emitido y `expectOne` no encuentra ninguna.
    await fixture.whenStable();
    http.expectOne('/api/v1/admin/mercados/4').flush({ ...FICHA, estado: 'publicado' });
    await publicando;

    expect(fixture.componentInstance.ficha()?.estado).toBe('publicado');
    expect(fixture.componentInstance.avisoDePublicacion()).toContain('publicado');
  });

  it('los dos ejes de estado de una edición no se mezclan', async () => {
    // La visibilidad decide si el portal la enseña —«Borrador»—; la realización describe el
    // acontecimiento —«Programada»—. Son dos preguntas distintas sobre la misma edición.
    await montar();
    const componente = fixture.componentInstance;

    expect(componente.rotuloDeVisibilidad('borrador')).toBe('Borrador');
    expect(componente.rotuloDeRealizacion('programada')).toBe('Programada');
    expect(componente.nombreDeLaEdicion(FICHA.ediciones[0])).toBe('Edición 2');
  });

  it('si la ficha no se puede abrir lo dice, y no se inventa una vacía', async () => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [AdminFichaMercadoComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    fixture = TestBed.createComponent(AdminFichaMercadoComponent);
    http = TestBed.inject(HttpTestingController);
    fixture.componentRef.setInput('mercadoId', '4');
    fixture.detectChanges();
    http.expectOne('/api/v1/admin/mercados/4').flush('', { status: 500, statusText: 'Error' });
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.componentInstance.ficha()).toBeNull();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('No fue posible abrir la ficha');
  });
});
