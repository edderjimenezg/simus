import { TestBed, ComponentFixture } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { InvitacionACompletarComponent } from './invitacion-a-completar.component';
import { PerfilOrganizacion } from './panel-organizacion.api';

/**
 * La invitación a completar la organización: §15.2 y §15.3 con la misma pieza.
 *
 * <b>LO QUE ESTAS PRUEBAS IMPIDEN.</b> Que la bienvenida reaparezca en cada recarga —entonces deja
 * de ser una bienvenida—, que se enseñe sobre un perfil que aún no se ha leído, y que se pierda el
 * «Completar después» que el §15.2 exige porque ninguno de esos campos es obligatorio.
 */
describe('la invitación a completar la organización', () => {
  let fixture: ComponentFixture<InvitacionACompletarComponent>;

  const perfil = (parcial: Partial<PerfilOrganizacion> = {}): PerfilOrganizacion => ({
    id: '1', nombre: 'Fundación X', nombreLegal: null, numeroIdentificacion: null,
    tipoIdentificacion: null, descripcion: null, correoContacto: 'x@y.co',
    telefonoContacto: null, sitioWeb: null, facebook: null, instagram: null,
    otroEnlace: null, direccion: null, estadoRegistro: 'activa',
    estadoRegistroEtiqueta: 'Activa',
    fechaActualizacion: null, ...parcial,
  });

  const COMPLETO = perfil({
    telefonoContacto: '3000000000', direccion: 'Calle 1', descripcion: 'Somos una fundación.',
    sitioWeb: 'https://x.co', instagram: '@x', facebook: 'fb/x',
  });

  function montar(entradas: { perfil?: PerfilOrganizacion | null; primerIngreso?: boolean; nombre?: string; correoConfirmado?: boolean } = {}) {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [InvitacionACompletarComponent],
      providers: [provideRouter([])],
    });
    fixture = TestBed.createComponent(InvitacionACompletarComponent);
    fixture.componentRef.setInput('perfil', entradas.perfil ?? null);
    fixture.componentRef.setInput('primerIngreso', entradas.primerIngreso ?? false);
    fixture.componentRef.setInput('nombre', entradas.nombre ?? '');
    fixture.componentRef.setInput('correoConfirmado', entradas.correoConfirmado ?? false);
    fixture.detectChanges();
    return { fixture, componente: fixture.componentInstance, texto: () => fixture.nativeElement.textContent as string };
  }

  it('no se pinta mientras el perfil no se haya leído', () => {
    const { texto } = montar({ perfil: null, primerIngreso: true });

    // MUTANTE QUE MATA: tratar `null` como un perfil vacío. Un cartel levantado por un fallo de red
    // diría que falta información que quizá ya está puesta.
    expect(texto().trim()).toBe('');
  });

  it('no se pinta si no falta nada, ni siquiera en el primer ingreso', () => {
    const { texto } = montar({ perfil: COMPLETO, primerIngreso: true });

    expect(texto().trim()).toBe('');
  });

  it('en el primer ingreso da la bienvenida, nombra lo que falta y ofrece dejarlo para después', () => {
    const { texto, fixture } = montar({ perfil: perfil(), primerIngreso: true, nombre: 'Ana Gestora', correoConfirmado: true });

    expect(texto()).toContain('Te damos la bienvenida, Ana Gestora');
    expect(texto()).toContain('Completa el teléfono y la dirección');
    // §15.2: «cuando los campos no sean obligatorios, permitir Completar después».
    expect(texto()).toContain('Completar después');
    expect(texto()).toContain('Completar organización');
    // Aparece sola, sin que nadie la pida: quien usa lector de pantalla tiene que enterarse.
    expect(fixture.nativeElement.querySelector('[role="status"]')).toBeTruthy();
  });

  it('sin nombre, el saludo se acorta en vez de quedar colgando', () => {
    const { texto } = montar({ perfil: perfil(), primerIngreso: true });

    expect(texto()).toContain('Te damos la bienvenida');
    expect(texto()).not.toContain('bienvenida,');
  });

  it('«Completar después» deja el indicador permanente, no lo apaga todo', () => {
    const { componente, texto, fixture } = montar({ perfil: perfil(), primerIngreso: true });

    componente.cerrar();
    fixture.detectChanges();

    // LA INFORMACION SIGUE FALTANDO: aplazar la bienvenida no la completa. Lo que se apaga es la
    // insistencia, no el hecho.
    expect(texto()).not.toContain('Te damos la bienvenida');
    expect(texto()).toContain('Tu organización tiene información pendiente por completar.');
    expect(texto()).toContain('Completa el teléfono y la dirección');
  });

  it('fuera del primer ingreso es el indicador discreto, y no se anuncia', () => {
    const { texto, fixture } = montar({ perfil: perfil(), primerIngreso: false });

    expect(texto()).toContain('Tu organización tiene información pendiente por completar.');
    expect(texto()).not.toContain('Te damos la bienvenida');
    // MUTANTE QUE MATA: poner `role="status"` también aquí. No es una novedad —lleva ahí desde que
    // se entró— y anunciarlo en cada navegación sería ruido.
    expect(fixture.nativeElement.querySelector('[role="status"]')).toBeNull();
  });

  it('no enseña porcentajes ni barras de progreso', () => {
    // §15.3 lo prohíbe expresamente salvo que aporten valor real, y aquí no lo aportan.
    const { texto, fixture } = montar({ perfil: perfil(), primerIngreso: true });

    expect(texto()).not.toMatch(/\d+\s*%/);
    expect(fixture.nativeElement.querySelector('progress, [role="progressbar"]')).toBeNull();
  });

  it('sin el correo confirmado, la bienvenida no dice que lo está', () => {
    // SE DESCUBRIO EN NAVEGADOR. La bienvenida abría con «Tu correo quedó confirmado» y decía «ya
    // puedes registrar procesos» a una organización recién registrada cuyo correo NO lo estaba
    // —el caso normal mientras no haya proveedor de correo—, mientras el aviso de la misma
    // pantalla, justo encima, decía lo contrario.
    const { texto } = montar({ perfil: perfil(), primerIngreso: true, correoConfirmado: false });

    expect(texto()).toContain('Te damos la bienvenida');
    expect(texto()).toContain('Tu organización quedó registrada');
    expect(texto()).not.toContain('Tu correo quedó confirmado');
    expect(texto()).not.toContain('Ya puedes registrar procesos');
    expect(texto()).toContain('Mientras confirmas tu correo');
  });

  it('con el correo confirmado sí lo dice, y dice qué se habilita', () => {
    const { texto } = montar({ perfil: perfil(), primerIngreso: true, correoConfirmado: true });

    expect(texto()).toContain('Tu correo quedó confirmado');
    expect(texto()).toContain('Ya puedes registrar procesos');
    expect(texto()).not.toContain('Mientras confirmas tu correo');
  });
});
