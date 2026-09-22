import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { AdminService } from '../../core/services/admin.service';
import { TextosWebService } from '../../core/services/textos-web.service';
import { ExternalSessionService } from '../../core/services/external-session.service';
import { IndicadorDePasosComponent } from '../../shared/components/ui/indicador-de-pasos/indicador-de-pasos.component';
import { PoliticaParaRegistro } from '../../core/services/politicas-de-datos';
import { NombrePropioPipe } from '../../shared/texto/nombre-propio.pipe';

type VistaDeAcceso = 'registro' | 'ingreso';
/**
 * Frontera pública de las organizaciones.
 *
 * Aquí solo viven el registro de una organización y el ingreso de una cuenta existente. Festival,
 * ediciones, selección de organización y propuestas pertenecen a Gestión, donde tienen sesión,
 * permisos y rutas propias. Mantenerlos aquí duplicaba un circuito ya reemplazado.
 */
@Component({
  selector: 'app-external-access-page',
  standalone: true,
  imports: [NombrePropioPipe, CommonModule, FormsModule, IndicadorDePasosComponent],
  templateUrl: './external-access-page.component.html',
})
export class ExternalAccessPageComponent implements OnInit {
  private readonly adminService = inject(AdminService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly webTexts = inject(TextosWebService);
  private readonly sesionExterna = inject(ExternalSessionService);

  readonly vista = signal<VistaDeAcceso>('registro');
  /** Alias de compatibilidad para las pruebas de la frontera pública. */
  readonly view = computed(() => this.vista() === 'registro' ? 'register' : 'login');
  readonly loading = signal(false);
  readonly message = signal('');
  readonly error = signal('');
  readonly erroresAlta = signal<Record<string, string>>({});
  readonly erroresIngreso = signal<Record<string, string>>({});
  readonly locations = signal<any[]>([]);
  /**
   * Los textos que hay que aceptar, servidos por el servidor.
   *
   * <b>NUNCA SE ESCRIBEN AQUI.</b> La plantilla los muestra tal como llegan porque son exactamente
   * los que el servidor copiará dentro de cada autorización como prueba (Ley 1581 art. 17 lit. f).
   * Antes esta pantalla componía su propia frase —«Acepto los {título} {versión}»— y lo único que
   * viajaba era un enlace a un PDF del Ministerio: la evidencia de lo aceptado vivía en un servidor
   * ajeno que puede cambiar el documento sin dejar rastro aquí.
   */
  readonly politicasDelRegistro = signal<readonly PoliticaParaRegistro[]>([]);

  /** Qué ha marcado la persona, por clave de finalidad. */
  readonly politicasAceptadas = signal<Record<string, boolean>>({});

  /** Cuál está desplegada para leerse entera. Solo una a la vez: son textos largos. */
  readonly politicaDesplegada = signal<string>('');
  readonly registroDisponible = signal(true);
  readonly pasoDeRegistro = signal(1);
  readonly mostrarContrasena = signal(false);

  readonly pasosDeRegistro = [
    { id: 1, titulo: 'Datos personales' },
    { id: 2, titulo: 'Organización y territorio' },
    { id: 3, titulo: 'Consentimientos' },
  ];

  readonly departments = computed(() => Array.from(new Map(this.locations().map(item => [item.departmentCode, item.departmentName])).entries())
    .map(([code, name]) => ({ code, name })));

  alta = {
    organizationName: '', organizationIdentificationNumber: '',
    headquartersDepartmentCode: '', headquartersMunicipalityCode: '',
    firstName: '', secondName: '', firstSurname: '', secondSurname: '',
    documentType: '', documentNumber: '', phone: '', email: '', password: '', confirmPassword: '',
  };
  login = { email: '', password: '' };

  texto(clave: string): string { return this.webTexts.getWebText(clave); }

  ngOnInit(): void {
    const modo = this.route.snapshot.data['modoAcceso'] as string | undefined;
    this.vista.set(modo === 'ingresar' ? 'ingreso' : 'registro');
    this.cargarPreparacionDelRegistro();
    this.sesionExterna.refrescar().subscribe(sesion => {
      if (sesion) this.router.navigate(['/gestion']);
    });
  }

  municipiosDelAlta(): any[] {
    return this.locations().filter(item => item.departmentCode === this.alta.headquartersDepartmentCode);
  }

  /** Si esa finalidad está marcada. */
  aceptada(clave: string): boolean {
    return this.politicasAceptadas()[clave] === true;
  }

  alternarPolitica(clave: string): void {
    this.politicasAceptadas.update(puestas => ({ ...puestas, [clave]: !puestas[clave] }));
  }

  /** Despliega o pliega el texto completo de una política. */
  alternarTextoDePolitica(clave: string): void {
    this.politicaDesplegada.update(puesta => (puesta === clave ? '' : clave));
  }

  irAlPasoDeRegistro(paso: number): void { this.pasoDeRegistro.set(Math.max(1, Math.min(3, paso))); }
  pasoSiguienteRegistro(): void { this.pasoDeRegistro.update(paso => Math.min(3, paso + 1)); }
  pasoAnteriorRegistro(): void { this.pasoDeRegistro.update(paso => Math.max(1, paso - 1)); }
  alternarMostrarContrasena(): void { this.mostrarContrasena.update(valor => !valor); }

  openExternalLogin(): void {
    this.limpiarMensajes();
    this.vista.set('ingreso');
    this.router.navigate(['/ingresar']);
  }

  openRegistrationForm(): void {
    this.limpiarMensajes();
    this.vista.set('registro');
    this.pasoDeRegistro.set(1);
    this.router.navigate(['/registro']);
  }

  actualizarCampoAlta(campo: string): void {
    this.erroresAlta.update(errores => {
      const siguiente = { ...errores };
      delete siguiente[campo];
      return siguiente;
    });
  }

  registrar(): void {
    this.limpiarMensajes();
    const documento = this.alta.documentNumber.replace(/\D/g, '');
    const errores: Record<string, string> = {};
    if (!this.alta.organizationName.trim()) errores['organizationName'] = 'Escribe el nombre de la organización.';
    if (!this.alta.headquartersDepartmentCode) errores['headquartersDepartmentCode'] = 'Elige el departamento de la sede.';
    if (!this.alta.headquartersMunicipalityCode) errores['headquartersMunicipalityCode'] = 'Elige el municipio de la sede.';
    if (!this.alta.firstName.trim()) errores['firstName'] = 'Escribe el primer nombre.';
    if (!this.alta.firstSurname.trim()) errores['firstSurname'] = 'Escribe el primer apellido.';
    if (!this.alta.documentType) errores['documentType'] = 'Elige el tipo de documento.';
    if (documento.length < 6 || documento.length > 15) errores['documentNumber'] = 'Ingresa un número de documento válido, entre 6 y 15 dígitos.';
    if (!this.esCorreoValido(this.alta.email)) errores['email'] = 'Ingresa un correo electrónico válido.';
    if (this.alta.phone.trim() && !/^[+()\-\s\d]{7,80}$/.test(this.alta.phone)) errores['phone'] = 'Revisa el formato del teléfono.';
    if (this.alta.password.length < 12) errores['password'] = 'La contraseña debe tener al menos 12 caracteres.';
    if (this.alta.confirmPassword !== this.alta.password) errores['confirmPassword'] = 'Las contraseñas no coinciden.';
    // LAS OBLIGATORIAS LAS DICE EL SERVIDOR. Antes había aquí dos comprobaciones fijas que
    // nombraban dos documentos concretos: el día que apareciera un tercero seguirían diciendo que
    // con esos dos bastaba, y el formulario dejaría pasar un alta incompleta.
    for (const politica of this.politicasDelRegistro()) {
      if (politica.obligatoria && !this.aceptada(politica.clave)) {
        errores['politicasAceptadas'] = 'Debes aceptar lo que el registro exige para continuar.';
        break;
      }
    }
    if (!this.registroDisponible()) errores['registration'] = 'El registro no está disponible mientras se completa su configuración.';
    this.erroresAlta.set(errores);
    const primerCampo = Object.keys(errores)[0];
    if (primerCampo) {
      this.pasoDeRegistro.set(['organizationName', 'headquartersDepartmentCode', 'headquartersMunicipalityCode'].includes(primerCampo) ? 2 : ['politicasAceptadas', 'registration'].includes(primerCampo) ? 3 : 1);
      return;
    }

    this.loading.set(true);
    this.adminService.registrarCuentaExterna({
      organizationName: this.alta.organizationName,
      organizationIdentificationNumber: this.alta.organizationIdentificationNumber || null,
      headquartersDepartmentCode: this.alta.headquartersDepartmentCode,
      headquartersMunicipalityCode: this.alta.headquartersMunicipalityCode,
      firstName: this.alta.firstName, secondName: this.alta.secondName || null,
      firstSurname: this.alta.firstSurname, secondSurname: this.alta.secondSurname || null,
      documentType: this.alta.documentType, documentNumber: this.alta.documentNumber,
      phone: this.alta.phone, email: this.alta.email, password: this.alta.password,
      politicasAceptadas: this.politicasDelRegistro()
        .filter(politica => this.aceptada(politica.clave))
        .map(politica => politica.clave),
    }).subscribe({
      next: () => { this.login = { email: this.alta.email, password: this.alta.password }; this.startLogin(); },
      error: fallo => this.fallar(fallo),
    });
  }

  startLogin(): void {
    this.limpiarMensajes();
    const errores: Record<string, string> = {};
    if (!this.esCorreoValido(this.login.email)) errores['loginEmail'] = 'Ingresa un correo electrónico válido.';
    if (!this.login.password) errores['loginPassword'] = 'Escribe tu contraseña.';
    this.erroresIngreso.set(errores);
    if (Object.keys(errores).length) return;
    this.loading.set(true);
    this.adminService.iniciarSesionExterna(this.login).subscribe({
      next: sesion => { this.loading.set(false); this.sesionExterna.establecer(sesion); this.router.navigate(['/gestion']); },
      error: fallo => this.fallarInicioSesion(fallo),
    });
  }

  private cargarPreparacionDelRegistro(): void {
    this.adminService.cargarDivipolaPublica().subscribe({ next: filas => this.locations.set(filas), error: fallo => this.fallar(fallo) });
    this.adminService.cargarPreparacionDeRegistroExterno().subscribe({
      next: preparacion => {
        this.politicasDelRegistro.set(preparacion?.politicas ?? []);
        this.registroDisponible.set(preparacion?.registroDisponible !== false);
        if (preparacion?.registroDisponible === false) this.error.set((preparacion.impedimentos ?? []).join(' '));
      }, error: fallo => this.fallar(fallo),
    });
  }

  private limpiarMensajes(): void { this.message.set(''); this.error.set(''); }
  private esCorreoValido(valor: string): boolean { return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(valor.trim()); }
  /**
   * No diferencia una cuenta inexistente de una contraseña incorrecta. Además de orientar a la
   * persona, evita convertir el formulario en un mecanismo de enumeración de cuentas.
   */
  private fallarInicioSesion(fallo: any): void {
    this.loading.set(false);
    const estado = Number(fallo?.status ?? 0);
    this.error.set([400, 401, 403].includes(estado)
      ? 'El correo electrónico o la contraseña no son correctos.'
      : 'No fue posible iniciar sesión. Inténtalo de nuevo.');
  }
  private fallar(fallo: any): void { this.loading.set(false); this.error.set(fallo?.message || 'No fue posible completar la solicitud. Inténtalo de nuevo.'); }
}
