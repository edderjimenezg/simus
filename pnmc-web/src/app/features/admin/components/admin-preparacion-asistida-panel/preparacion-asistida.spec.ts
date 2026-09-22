import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AdminPreparacionAsistidaPanelComponent } from './admin-preparacion-asistida-panel.component';
import { ImportacionesService } from './importaciones.service';
import { of } from 'rxjs';

describe('Preparación asistida de archivos', () => {
  let fixture: ComponentFixture<AdminPreparacionAsistidaPanelComponent>;
  let servicio: jasmine.SpyObj<ImportacionesService>;

  beforeEach(() => {
    servicio = jasmine.createSpyObj<ImportacionesService>(
      'ImportacionesService',
      ['listar', 'consultar', 'previsualizar', 'confirmar', 'dominiosImportables'],
    );
    // LA PANTALLA PREGUNTA AL SERVIDOR QUE SE PUEDE IMPORTAR desde, en
    // vez de llevar la lista escrita. El fijador responde lo mismo que el contrato del API.
    servicio.dominiosImportables.and.returnValue(of({
      dominios: [{
        dominio: 'festivales',
        etiqueta: 'Festivales',
        estadoAlImportar: 'borrador',
        porQueEseEstado: 'Publicarlo sigue exigiendo revisión.',
        campos: [
          { nombre: 'nombre', etiqueta: 'Nombre del Festival', obligatorio: true },
          { nombre: 'departamento', etiqueta: 'Departamento', obligatorio: false },
        ],
      }],
    }));
    servicio.listar.and.returnValue(of({
      items: [{
        id: 17,
        nombreArchivo: 'festivales-revisados.xlsx',
        formato: 'xlsx',
        estado: 'previsualizado',
        fechaPrevisualizacion: '2026-09-11T10:00:00Z',
        fechaAplicacion: null,
        fechaExpiracion: '2026-10-11T10:00:00Z',
        fechaDepuracion: null,
        estadoRetencion: 'detalle_temporal',
        esPropio: true,
        puedeConfirmar: true,
        totalFilas: 12,
        filasImportables: 10,
        filasRechazadas: 2,
        filasAplicadas: 0,
        filasExcluidas: 0,
      }],
      pagina: 1,
      tamano: 10,
      total: 1,
      totalPaginas: 1,
      diasRetencionPrevisualizacion: 30,
      politicaRetencion: 'Las previsualizaciones conservan detalle durante 30 días.',
    }));
    TestBed.configureTestingModule({
      imports: [AdminPreparacionAsistidaPanelComponent],
      providers: [{
        provide: ImportacionesService,
        useValue: servicio,
      }],
    });
    fixture = TestBed.createComponent(AdminPreparacionAsistidaPanelComponent);
    fixture.componentRef.setInput('idModulo', 'festivals');
    fixture.detectChanges();
  });

  it('explica la separación entre previsualizar, confirmar y publicar', () => {
    const texto = fixture.nativeElement.textContent as string;

    // EL TITULO Y LAS INSTRUCCIONES NOMBRAN EL DESTINO ELEGIDO desde.
    // Antes decían «Festivales» siempre, en una pantalla que sirve a cinco destinos: quien
    // preparaba organizaciones leía que «solo una confirmación crea Festivales».
    expect(texto).toContain('Importación asistida de Festivales');
    expect(texto).toContain('La previsualización en la API no crea registros');
    expect(texto).toContain('crea registros de Festivales en estado «borrador»');
    expect(texto).toContain('nunca publica ni modifica existentes');
    expect(texto).not.toContain('Inteligencia Artificial');
    expect(texto).not.toContain('Importación IA');
  });

  it('ofrece exactamente los dominios que el servidor dice saber recibir', () => {
    // ANTES LA LISTA ESTABA ESCRITA EN EL NAVEGADOR, y por eso esta pantalla llegó a anunciar ocho
    // módulos de los que siete no tenían circuito. Ahora el servidor manda: si mañana declara uno
    // nuevo, aparece sin tocar el navegador; y si deja de declararlo, desaparece.
    expect(fixture.componentInstance.modulosAdministrativos().map(modulo => modulo.dominio)).toEqual(['festivales']);
  });

  it('dice antes de subir nada que lo importado no se publica solo', () => {
    const aviso = (fixture.nativeElement as HTMLElement).querySelector('[data-promesa-del-dominio]');

    // ENTERARSE AL VER LA LISTA DE RESULTADOS LLEGA TARDE para quien creía que estaba publicando un
    // directorio entero. El estado lo declara el servidor; la pantalla solo lo repite.
    expect(aviso?.textContent).toContain('borrador');
    expect(aviso?.textContent).toContain('nada se publica');
  });

  it('si el servidor no contesta, no inventa destinos', () => {
    servicio.dominiosImportables.and.returnValue(of({ dominios: [] }));
    const otra = TestBed.createComponent(AdminPreparacionAsistidaPanelComponent);
    otra.componentRef.setInput('idModulo', 'festivals');
    otra.detectChanges();

    // OFRECER UN DESTINO QUE NO EXISTE es peor que no ofrecer ninguno: el trabajo de preparar el
    // archivo se pierde entero al final.
    expect(otra.componentInstance.modulosAdministrativos()).toEqual([]);
  });

  it('presenta el historial con estado y política de conservación', async () => {
    await fixture.componentInstance.alternarHistorial();
    fixture.detectChanges();
    const texto = fixture.nativeElement.textContent as string;

    // EL DOMINIO VIAJA EN CADA LLAMADA desde. Antes la ruta llevaba
    // «festivales» escrito dentro: la consola ofrecía varios destinos y todas las llamadas iban al
    // mismo, así que elegir «Organizaciones» habría previsualizado contra las reglas de Festival.
    expect(servicio.listar).toHaveBeenCalledWith(
      jasmine.any(String), { pagina: 1, tamano: 10, estado: '', archivo: '' });
    expect(texto).toContain('Historial de importaciones');
    expect(texto).toContain('festivales-revisados.xlsx');
    expect(texto).toContain('Pendiente de confirmación');
    expect(texto).toContain('Detalle temporal');
    expect(texto).toContain('30 días');
  });

  it('cada llamada lleva el destino elegido, no uno escrito en la ruta', async () => {
    // EL DEFECTO QUE ESTO CIERRA. El Bloque 5b convirtió la importación en genérica —núcleo,
    // contrato, pantalla y registro de dominios— y renombró el servicio, pero dejó la ruta con
    // «festivales» escrito dentro. La consola ofrecía varios destinos y TODAS las llamadas iban al
    // mismo: elegir «Organizaciones» habría previsualizado organizaciones contra las reglas de
    // Festival. Lo señalaba la auditoría de rutas, enterrado entre sus propios falsos positivos.
    const componente = fixture.componentInstance;
    componente.idModuloSeleccionado.set('organizaciones');

    await componente.alternarHistorial();

    // MUTANTE QUE MATA: volver a una constante fija. El primer argumento dejaría de ser el destino
    // elegido y esta comprobación caería.
    expect(servicio.listar).toHaveBeenCalledWith('organizaciones', jasmine.anything());
  });
});
