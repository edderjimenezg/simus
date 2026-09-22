import { ChangeDetectionStrategy, Component, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AdminService, TipoDeDocumento } from '../../../core/services/admin.service';
import { BotonComponent } from '../../../shared/components/ui/boton/boton.component';

/**
 * El primer ingreso de una cuenta administrativa: cambiar la clave y decir quién eres.
 *
 * <b>POR QUE EXISTE.</b> Una cuenta administrativa no se registra: se ENTREGA. La dirección de producto
 * lo fijó: «a ellos se les registra simplemente un correo y una
 * contraseña por defecto; en su primer inicio de sesión les debe pedir cambiar la contraseña, una
 * vez cambien la contraseña se les pide completar esos datos básicos… y una vez eso pase ya se le
 * activa el usuario». Los módulos se deciden ANTES de entregarla, así que aquí no se eligen.
 *
 * <b>DOS PASOS EN ORDEN, Y SE VE CUAL FALTA.</b> Mientras la contraseña siga siendo la que eligió
 * otra persona, esa credencial la conocen dos. Después la persona dice quién es, porque una cuenta
 * sin nombre real no permite responder quién hizo qué.
 *
 * <b>NO ES UN DIALOGO NI SE PUEDE SALTAR.</b> Ocupa la pantalla porque no hay nada más que hacer
 * hasta terminarlo, y el servidor lo aplica igual: sin terminar, cada módulo responde 403. Un
 * recorrido que solo existiera aquí se saltaría escribiendo la ruta.
 *
 * <b>EL NOMBRE, PARTIDO EN CUATRO</b>, igual que en el alta externa: el proyecto pide la misma
 * estructura de nombre en todos sus formularios.
 */
@Component({
  selector: 'app-admin-primer-ingreso',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, BotonComponent],
  templateUrl: './admin-primer-ingreso.component.html',
})
export class AdminPrimerIngresoComponent {
  private readonly adminService = inject(AdminService);

  /** Si todavía hay que cambiar la contraseña. Decide en qué paso arranca. */
  readonly debeCambiarContrasena = input.required<boolean>();

  /** El correo con el que entró, para que sepa de qué cuenta se le habla. */
  readonly correo = input<string>('');

  /** Se emite cuando el recorrido termina: la consola ya puede abrirse. */
  readonly terminado = output<void>();

  readonly paso = signal<'contrasena' | 'perfil'>('contrasena');
  readonly trabajando = signal(false);
  readonly error = signal<string | null>(null);
  readonly tiposDeDocumento = signal<TipoDeDocumento[]>([]);

  readonly actual = signal('');
  readonly nueva = signal('');
  readonly repetida = signal('');

  readonly perfil = signal({
    primerNombre: '', segundoNombre: '', primerApellido: '', segundoApellido: '',
    tipoDocumento: '', identificacion: '', telefono: '',
  });

  constructor() {
    // EL PASO DE ARRANQUE LO DICE EL SERVIDOR: quien ya cambió la clave y cerró la ventana antes de
    // llenar sus datos no tiene que volver a cambiarla.
    queueMicrotask(() => {
      if (!this.debeCambiarContrasena()) { this.irAlPerfil(); }
    });
  }

  campoDelPerfil(clave: keyof ReturnType<typeof this.perfil>, valor: string): void {
    this.perfil.update(actual => ({ ...actual, [clave]: valor }));
    this.error.set(null);
  }

  cambiarLaContrasena(): void {
    if (this.trabajando()) { return; }
    this.error.set(null);

    if (this.nueva().length < 10) {
      this.error.set('La contraseña nueva tiene que tener al menos diez caracteres.');
      return;
    }
    // LAS DOS SE ESCRIBEN Y SE COMPARAN AQUI. El servidor no puede detectar un dedo torcido: quien
    // se equivoca al teclearla se queda fuera en el siguiente ingreso.
    if (this.nueva() !== this.repetida()) {
      this.error.set('Las dos contraseñas nuevas no coinciden.');
      return;
    }

    this.trabajando.set(true);
    this.adminService.cambiarMiContrasena(this.actual(), this.nueva()).subscribe({
      next: () => {
        this.trabajando.set(false);
        this.irAlPerfil();
      },
      error: (fallo: { message?: string }) => {
        this.trabajando.set(false);
        this.error.set(fallo?.message || 'No fue posible cambiar la contraseña.');
      },
    });
  }

  guardarElPerfil(): void {
    if (this.trabajando()) { return; }
    this.error.set(null);

    const p = this.perfil();
    const falta = !p.primerNombre.trim() || !p.primerApellido.trim()
      || !p.tipoDocumento.trim() || !p.identificacion.trim();
    if (falta) {
      // LOS OBLIGATORIOS SON LOS QUE IDENTIFICAN. El segundo nombre y el segundo apellido no los
      // tiene todo el mundo; el primero de cada uno, sí.
      this.error.set('Faltan tu primer nombre, tu primer apellido y tu documento.');
      return;
    }

    this.trabajando.set(true);
    this.adminService.completarMiPerfil(p).subscribe({
      next: () => {
        this.trabajando.set(false);
        this.terminado.emit();
      },
      error: (fallo: { message?: string }) => {
        this.trabajando.set(false);
        this.error.set(fallo?.message || 'No fue posible guardar tu perfil.');
      },
    });
  }

  private irAlPerfil(): void {
    this.paso.set('perfil');
    if (this.tiposDeDocumento().length === 0) {
      this.adminService.cargarTiposDeDocumento().subscribe({
        next: tipos => this.tiposDeDocumento.set(tipos),
        // SI NO LLEGAN, LA LISTA SE QUEDA VACIA Y SE DICE. Un desplegable vacío sin explicación
        // haría pensar que la persona no tiene tipo de documento válido.
        error: () => this.error.set('No fue posible cargar los tipos de documento. Vuelve a intentarlo.'),
      });
    }
  }
}
