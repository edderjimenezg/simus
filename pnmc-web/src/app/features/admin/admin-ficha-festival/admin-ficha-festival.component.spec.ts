import { TestBed, ComponentFixture } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AdminFichaFestivalComponent, FichaFestivalAdministrativa } from './admin-ficha-festival.component';

/**
 * El historial del Festival en la consola.
 *
 * <b>LO QUE ESTA PRUEBA IMPIDE.</b> Que la entidad principal del Ecosistema vuelva a ser la única
 * cuyo historial no se puede abrir. `TABLA_DE_AUDITORIA` declaraba `festivales: 'Festivales'` y la
 * bitácora venía registrando cada actuación desde hacía cortes, pero ninguna pantalla llamaba a
 * `/admin/auditoria` con esa tabla: el dato existía y era inalcanzable. Noticias, Agenda y Catálogo
 * ya lo abrían con la misma pieza; aquí no había nada.
 */
describe('la ficha administrativa de un Festival', () => {
  let fixture: ComponentFixture<AdminFichaFestivalComponent>;
  let http: HttpTestingController;

  const FICHA: FichaFestivalAdministrativa = {
    id: '105',
    nombre: 'Festival de Prueba',
    descripcion: null,
    estado: 'publicado',
    nivelCobertura: 'municipal',
    departamento: 'Antioquia',
    municipio: 'Medellín',
    periodicidad: null,
    correoContacto: null,
    telefono: null,
    sitioWeb: null,
    organizacionResponsableId: 117,
    organizacionResponsableNombre: 'Fundación X',
    procedencia: null,
    practicasMusicales: [],
    territoriosSonoros: [],
    ediciones: [],
    fechaCreacion: '2026-09-01T10:00:00Z',
    fechaActualizacion: null,
  };

  async function montar(): Promise<void> {
    TestBed.configureTestingModule({
      imports: [AdminFichaFestivalComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    fixture = TestBed.createComponent(AdminFichaFestivalComponent);
    http = TestBed.inject(HttpTestingController);
    fixture.componentRef.setInput('festivalId', '105');
    fixture.detectChanges();
    http.expectOne('/api/v1/admin/festivales/105').flush(FICHA);
    // LA CARGA ES `async`: el `set` de la señal ocurre en una microtarea posterior al `flush`, así
    // que sin esperar aquí la plantilla se comprueba todavía en «cargando» y no hay nada que mirar.
    await fixture.whenStable();
    fixture.detectChanges();
  }

  afterEach(() => http?.verify());

  it('abre el historial del Festival con la misma pieza que las demás pantallas', async () => {
    await montar();

    // `data-testid` Y NO `data-abrir-historial`: «Ver historial» es ahora un `app-boton`, que pone el
    // identificador de prueba en el botón real (su anfitrión es `display: contents`). Lo que se
    // vigila —que la ficha ofrezca el historial y que no dependa del permiso de publicar— no cambia.
    const boton = fixture.nativeElement.querySelector('[data-testid="abrir-historial"]') as HTMLButtonElement;
    expect(boton).withContext('la ficha del Festival no ofrecía historial').toBeTruthy();
    boton.click();
    fixture.detectChanges();

    const peticion = http.expectOne(p => p.url.includes('/admin/auditoria'));
    // LA TABLA ES LA QUE EL CODIGO ESCRIBE DE VERDAD: `TableName = "Festivales"`. Pedirla con el
    // nombre del módulo devolvería una lista vacía y parecería que el Festival no tiene historia.
    expect(peticion.request.urlWithParams).toContain('tabla=Festivales');
    expect(peticion.request.urlWithParams).toContain('registroId=105');
    peticion.flush({ items: [] });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Historial y procedencia');
  });

  it('el historial no depende del permiso de publicar', async () => {
    // ES UNA LECTURA. Los botones de publicar, despublicar y archivar sí están tras `puedePublicar`
    // porque cambian el registro; saber quién lo tocó no cambia nada y lo necesita cualquiera que
    // pueda abrir la ficha para entender en qué estado está y por qué.
    await montar();
    expect(fixture.componentInstance.puedePublicar).toBeFalse();
    expect(fixture.nativeElement.querySelector('[data-testid="abrir-historial"]')).toBeTruthy();
  });
});
