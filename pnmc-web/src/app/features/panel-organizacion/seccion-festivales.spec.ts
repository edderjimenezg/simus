import { ComponentFixture, TestBed } from '@angular/core/testing';
import { SeccionFestivalesComponent } from './seccion-festivales.component';
import { FestivalDeLaOrganizacion } from './panel-organizacion.api';

describe('SeccionFestivalesComponent · centro de gestión', () => {
  let fixture: ComponentFixture<SeccionFestivalesComponent>;
  let componente: SeccionFestivalesComponent;

  const festivalCon = (extra: Partial<FestivalDeLaOrganizacion> = {}): FestivalDeLaOrganizacion => ({
    id: '1', nombre: 'Festival de prueba', estado: 'Borrador', ...extra,
  });
  const pintar = (festivales: FestivalDeLaOrganizacion[], ocupado = false): void => {
    fixture.componentRef.setInput('festivales', festivales);
    fixture.componentRef.setInput('ocupado', ocupado);
    fixture.detectChanges();
  };
  const raiz = (): HTMLElement => fixture.nativeElement as HTMLElement;
  const boton = (texto: string): HTMLButtonElement => {
    const encontrado = Array.from(raiz().querySelectorAll<HTMLButtonElement>('button'))
      .find(item => item.textContent?.trim() === texto);
    if (!encontrado) throw new Error(`No se encontró «${texto}».`);
    return encontrado;
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [SeccionFestivalesComponent] }).compileComponents();
    fixture = TestBed.createComponent(SeccionFestivalesComponent);
    componente = fixture.componentInstance;
  });

  it('presenta cada Festival como una fila con organización, territorio, estados y resumen', () => {
    pintar([festivalCon({
      organizacionPrincipalNombre: 'Corporación Cultural de Neiva',
      nombreMunicipio: 'Neiva', nombreDepartamento: 'Huila', cuantasEdiciones: 4,
      fechaActualizacion: '2026-09-01T00:00:00Z', estadoPropuesta: 'borrador', cambiosPedidos: 2,
    })]);
    const texto = raiz().textContent ?? '';
    expect(texto).toContain('Corporación Cultural de Neiva · Neiva (Huila)');
    expect(texto).toContain('4 ediciones registradas');
    expect(texto).toContain('Propuesta Borrador');
    expect(texto).toContain('2 ajustes pendientes');
    expect(raiz().querySelectorAll('li').length).toBe(1);
  });

  it('mantiene solo Ver ficha visible y concentra el resto dentro de Acciones', () => {
    pintar([festivalCon()]);
    expect(Array.from(raiz().querySelectorAll('button')).map(item => item.textContent?.trim()))
      .toEqual(['Ver ficha', 'Continuar registro', 'Enviar a revisión', 'Registrar nueva edición', 'Eliminar borrador']);
    expect(raiz().querySelector('summary')?.textContent).toContain('Acciones');
  });

  it('ofrece solo las operaciones válidas del borrador y emite el registro correcto', () => {
    const festival = festivalCon({ id: '10' });
    pintar([festival]);
    const emitidos: string[] = [];
    componente.editar.subscribe(item => emitidos.push(`editar:${item.id}`));
    componente.enviarARevision.subscribe(item => emitidos.push(`revisar:${item.id}`));
    componente.retirar.subscribe(item => emitidos.push(`retirar:${item.id}`));
    boton('Continuar registro').click();
    boton('Enviar a revisión').click();
    boton('Eliminar borrador').click();
    expect(emitidos).toEqual(['editar:10', 'revisar:10', 'retirar:10']);
  });

  it('bloquea el reenvío mientras queden ajustes por atender', () => {
    pintar([festivalCon({ estado: 'AjustesSolicitados', cambiosPedidos: 1 })]);
    expect(boton('Volver a enviar a revisión').disabled).toBeTrue();
    expect((raiz().textContent ?? '')).toContain('1 ajuste pendiente');
  });

  it('en revisión permite retirar, pero no editar ni enviar otra vez', () => {
    pintar([festivalCon({ estado: 'EnRevision' })]);
    expect(boton('Retirar de revisión')).toBeTruthy();
    expect(raiz().textContent).not.toContain('Continuar registro');
    expect(raiz().textContent).not.toContain('Enviar a revisión');
  });

  it('en un Festival publicado usa propuesta de cambios y conserva la gestión de ediciones', () => {
    pintar([festivalCon({ id: '22', estado: 'Publicado', cuantasEdiciones: 2 })]);
    expect(boton('Editar Festival')).toBeTruthy();
    expect(boton('Ver ediciones (2)')).toBeTruthy();
    expect(raiz().textContent).not.toContain('Eliminar borrador');
  });

  it('no ofrece una propuesta adicional cuando ya existe una viva', () => {
    pintar([festivalCon({ estado: 'Publicado', estadoPropuesta: 'borrador' })]);
    expect(boton('Continuar edición')).toBeTruthy();
    expect(boton('Enviar propuesta a revisión')).toBeTruthy();
    expect(Array.from(raiz().querySelectorAll('button')).map(item => item.textContent?.trim()))
      .not.toContain('Editar Festival');
  });

  it('Ver ficha está disponible para consulta en todos los estados', () => {
    pintar([festivalCon({ id: '30', estado: 'Borrador' }), festivalCon({ id: '31', estado: 'Publicado' })]);
    const emitidos: string[] = [];
    componente.abrirFicha.subscribe(item => emitidos.push(item.id));
    raiz().querySelector<HTMLButtonElement>('[data-testid="abrir-ficha-31"]')!.click();
    expect(emitidos).toEqual(['31']);
  });

  it('preserva la descripción completa en el DOM y no deja huecos cuando falta', () => {
    const descripcion = 'Encuentro de tamboras, alegres y llamadores de los municipios ribereños.';
    pintar([festivalCon({ id: '7', descripcion }), festivalCon({ id: '8', descripcion: null })]);
    expect(raiz().querySelector<HTMLElement>('[data-testid="descripcion-festival-7"]')!.textContent?.trim()).toBe(descripcion);
    expect(raiz().querySelector('[data-testid="descripcion-festival-8"]')).toBeNull();
  });
});
