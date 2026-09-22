import { OpcionSegmentada, SelectorSegmentadoComponent } from '../../../shared/components/ui/selector-segmentado/selector-segmentado.component';
import { BarraDeListaComponent } from '../../../shared/components/ui/barra-de-lista/barra-de-lista.component';
import { FiltroDesplegableComponent, OpcionDeFiltro } from '../../../shared/components/ui/filtro-desplegable/filtro-desplegable.component';
import { CabeceraDeTablaComponent } from '../../../shared/components/ui/tabla/cabecera-de-tabla.component';
import { ColumnaDeTabla, OrdenDeTabla } from '../../../shared/components/ui/tabla/orden-de-tabla';
import { Component, computed, inject, signal, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { CatalogoDeModulos, FormularioDeCuenta, ModulosDeUnaCuenta } from '../../../core/services/admin.service';
import { GRUPOS_DE_GESTION_ADMINISTRATIVA } from '../domain/navegacion-administrativa';
import { IndicadorDeEstadoComponent } from '../../../shared/components/ui/indicador-de-estado/indicador-de-estado.component';
import { matizDelEstado, tonoDelEstado } from '../../../shared/components/ui/indicador-de-estado/tono-del-estado';
import { FormsModule } from '@angular/forms';
import { BuscadorDeListaComponent } from '../../../shared/components/ui/buscador/buscador-de-lista.component';
import { LucideUserCog, LucideX } from '@lucide/angular';
import { BotonComponent } from '../../../shared/components/ui/boton/boton.component';
import { EstadoDeListaComponent } from '../../../shared/components/ui/estado-de-lista/estado-de-lista.component';
import { ListaSinFilasComponent } from '../../../shared/components/ui/lista-sin-filas/lista-sin-filas.component';
import { AdminService, UsuarioDelSistema } from '../../../core/services/admin.service';
import { ADMIN_ROLES, ROLES_INTERNOS, rolesDeSesion } from '../domain/admin-config';
import { HistorialDeRegistroComponent } from '../../../shared/components/ui/historial-de-registro/historial-de-registro.component';
import { TABLA_DE_AUDITORIA } from '../../../core/services/historial-de-registro.service';

import { DialogoDirective } from '../../../shared/directives/dialogo.directive';
import { ConfirmacionComponent } from '../../../shared/components/ui/confirmacion/confirmacion.component';
import { DialogoDeFormularioComponent } from '../../../shared/components/ui/dialogo-de-formulario/dialogo-de-formulario.component';
import { PanelLateralComponent } from '../../../shared/components/ui/panel-lateral/panel-lateral.component';
import { DatoEnLecturaComponent } from '../../../shared/components/ui/dato-en-lectura/dato-en-lectura.component';
import { AccionDeRegistro, MenuDeAccionesComponent } from '../../../shared/components/ui/menu-de-acciones/menu-de-acciones.component';
@Component({
  selector: 'app-admin-users-panel',
  standalone: true,
  imports: [
    BarraDeListaComponent,SelectorSegmentadoComponent, FiltroDesplegableComponent, CabeceraDeTablaComponent, DialogoDirective, MenuDeAccionesComponent,
    BotonComponent, EstadoDeListaComponent, ListaSinFilasComponent,
    CommonModule,
    FormsModule,
    LucideUserCog,
    BuscadorDeListaComponent,
    LucideX,
    IndicadorDeEstadoComponent,
    HistorialDeRegistroComponent,
    PanelLateralComponent,
    DatoEnLecturaComponent,
    ConfirmacionComponent,
    DialogoDeFormularioComponent,
  ],
  templateUrl: './admin-users-panel.component.html',
  styleUrls: ['./admin-users-panel.component.css']
})
export class AdminUsersPanelComponent {
  /** El tono y el matiz del indicador, desde el criterio único del proyecto. */
  readonly tonoDelEstado = tonoDelEstado;
  readonly matizDelEstado = matizDelEstado;

  private adminService = inject(AdminService);

  @Input() set enabled(val: boolean) {
    this._enabled.set(val);
    if (val) {
      this.cargarLasCuentas();
    }
  }
  _enabled = signal<boolean>(false);

  // Users state
  users = signal<UsuarioDelSistema[]>([]);
  /**
   * `roles` es un CONJUNTO desde entonces de la migración de SIMUS.
   *
   * `role` ya no está: el API acepta los dos campos —`roles` manda cuando viene— pero enviar los
   * dos desde aquí abriría la puerta a que se contradijeran, y quien leyera el código tendría que
   * averiguar cuál gana. Se envía uno solo.
   */
  valoresDelFormulario = signal<FormularioDeCuenta>(
    { fullName: '', email: '', roles: ['gestor_interno'], password: '', isActive: true,
      identificacion: '', tipoDocumento: '', telefono: '' }
  );

  /**
   * Los tipos de documento del catálogo del país.
   *
   * SALEN DE LA TABLA Y NO DE UNA LISTA ESCRITA AQUI. Es vocabulario controlado: una lista propia
   * sería la tercera copia del mismo catálogo, y ya hubo que corregir una que había divergido.
   */
  readonly tiposDeDocumento = signal<{ codigo: string; etiqueta: string }[]>([]);
  readonly errorDelPerfil = signal<string | null>(null);

  /** Escribe un campo del perfil sin pisar el resto del formulario. */
  campoDelPerfil(clave: 'identificacion' | 'tipoDocumento' | 'telefono', valor: string): void {
    this.valoresDelFormulario.update(actual => ({ ...actual, [clave]: valor }));
    this.errorDelPerfil.set(null);
  }

  /**
   * Qué le falta al perfil para poder guardarse, si algo.
   *
   * EL NUMERO Y SU TIPO VAN JUNTOS. Un número sin tipo no dice qué es —¿cédula, pasaporte?— y un
   * tipo sin número no identifica a nadie. El servidor lo rechaza igual; aquí se dice antes de
   * enviar, al lado del campo.
   */
  private problemaDelPerfil(): string | null {
    const f = this.valoresDelFormulario();
    const conNumero = (f.identificacion ?? '').trim().length > 0;
    const conTipo = (f.tipoDocumento ?? '').trim().length > 0;
    if (conNumero === conTipo) { return null; }
    return conNumero
      ? 'Falta elegir el tipo de documento.'
      : 'Falta el número del documento.';
  }
  message = signal<string>('');
  cuentaEnEdicion = signal<string | null>(null);
  editorAbierto = signal<boolean>(false);
  // «En vuelo» del guardado. El componente no tenia ninguna senal de carga que
  // reutilizar, asi que se anade esta: sin ella, el segundo clic en «Crear
  // usuario» vuelve a enviar el formulario y crea la cuenta por duplicado.
  guardando = signal<boolean>(false);
  readonly busqueda = signal('');
  readonly filtroRol = signal('todos');
  readonly filtroEstado = signal<'todos' | 'activos' | 'inactivos'>('todos');
  readonly usuarioSeleccionado = signal<UsuarioDelSistema | null>(null);
  /**
   * La cuenta cuyo historial se está mirando, o `null`.
   *
   * <b>SE PIDE AL ABRIR, NO AL ABRIR LA FICHA.</b> Antes la ficha traía consigo diez líneas de
   * bitácora que casi nadie leía, con una consulta por cada apertura. Es la misma decisión que ya
   * tomaron Noticias, Agenda, Catálogo, Organizaciones y Festivales.
   */
  readonly historialAbierto = signal<UsuarioDelSistema | null>(null);
  readonly tablaDeAuditoria = TABLA_DE_AUDITORIA.usuarios;


  /** Los roles del sistema, en la forma que pide el filtro compartido. */
  /** El filtro compartido emite texto; la señal solo admite sus tres valores. */
  filtrarPorEstado(valor: string): void {
    if (valor === 'todos' || valor === 'activos' || valor === 'inactivos') { this.filtroEstado.set(valor); }
  }

  /**
   * Los roles del ámbito que se está viendo, y no los tres siempre.
   *
   * Ofrecer «Externo» mientras se miran las cuentas administrativas lleva a una lista vacía por
   * construcción, que es el defecto que ya se corrigió en el filtro territorial de Festivales.
   */
  readonly filtroDeRol = computed<readonly OpcionDeFiltro[]>(() => {
    const delAmbito = this.ambito() === 'administrativas'
      ? this.adminRoles.filter(rol => ROLES_INTERNOS.includes(rol.id))
      : this.adminRoles.filter(rol => !ROLES_INTERNOS.includes(rol.id));
    return [
      { id: 'todos', etiqueta: 'Todos los roles' },
      ...delAmbito.map(rol => ({ id: rol.id, etiqueta: rol.shortLabel })),
    ];
  });

  readonly FILTRO_DE_ESTADO: readonly OpcionDeFiltro[] = [
    { id: 'todos', etiqueta: 'Todos los estados' },
    { id: 'activos', etiqueta: 'Activos' },
    { id: 'inactivos', etiqueta: 'Inactivos' },
  ];

  readonly COLUMNAS: readonly ColumnaDeTabla[] = [
    { id: 'usuario', etiqueta: 'Usuario' },
    { id: 'rol', etiqueta: 'Rol' },
    // QUE PUEDE ABRIR, EN LA LISTA. Es la pregunta que se le hace a esta pantalla, y hasta el 17
    // de septiembre de 2026 había que abrir la ficha de cada cuenta y contar casillas para
    // responderla. Solo aplica a las administrativas: una cuenta externa no entra a la consola.
    { id: 'apartados', etiqueta: 'Apartados' },
    { id: 'estado', etiqueta: 'Estado' },
    { id: 'acciones', etiqueta: 'Acciones', ordenable: false, clases: 'text-right' },
  ];

  /**
   * Cómo se lee lo que una cuenta puede abrir.
   *
   * <b>«TODOS» NO ES UN NUMERO</b>: un webmaster los tiene por lo que es, no por concesión, y
   * escribir «16 de 16» haría creer que alguien se los concedió uno a uno y que se le pueden
   * quitar. Una cuenta externa no entra a la consola, así que no tiene apartados que contar: se
   * dice, en vez de dejar el hueco.
   */
  apartadosDe(usuario: UsuarioDelSistema): string {
    if (!this.esAdministrativa(usuario)) { return 'No entra a la consola'; }
    if (this.rolesDe(usuario).includes('webmaster')) { return 'Todos'; }
    const activos = usuario.apartadosActivos ?? 0;
    const totales = usuario.apartadosTotales ?? 0;
    return totales ? `${activos} de ${totales}` : '—';
  }

  /** Cierto cuando la cuenta solo tiene los dos que vienen siempre: no se le concedió ninguno. */
  soloLoMinimo(usuario: UsuarioDelSistema): boolean {
    return this.esAdministrativa(usuario)
      && !this.rolesDe(usuario).includes('webmaster')
      && (usuario.apartadosActivos ?? 0) <= 2;
  }

  readonly orden = new OrdenDeTabla('usuario', 'asc');

  /**
   * De qué lado del sistema es cada cuenta: la consola o el ecosistema.
   *
   * <b>SON DOS POBLACIONES QUE NO SE GESTIONAN IGUAL</b>, y verlas mezcladas en una lista de
   * veintiuna cuentas hacía que las tres internas se perdieran entre dieciocho externas. Lo pidió
   * criterio: «deberían haber pestañas para externo y
   * para administrativos».
   *
   * El corte es el mismo que ya decide qué pantalla se abre al iniciar sesión —`ROLES_INTERNOS`,
   * escrita como lista blanca— y no una segunda lista escrita aquí: dos listas que digan quién es
   * del Ministerio se separan sin que nadie lo note.
   */
  readonly ambito = signal<'administrativas' | 'externas'>('administrativas');

  readonly AMBITOS: readonly OpcionSegmentada[] = [
    { id: 'administrativas', etiqueta: 'Administrativas' },
    { id: 'externas', etiqueta: 'Externas' },
  ];

  cambiarAmbito(valor: string): void {
    if (valor !== 'administrativas' && valor !== 'externas') { return; }
    this.ambito.set(valor);
    // El rol elegido puede no existir del otro lado: arrastrarlo dejaría la lista vacía sin decir
    // por qué. Es el mismo criterio que aplica Festivales al cambiar de módulo.
    this.filtroRol.set('todos');
  }

  /** Cuántas cuentas hay de cada lado, para que la pestaña lo diga antes de pulsarla. */
  readonly pestanasDeAmbito = computed<readonly OpcionSegmentada[]>(() => {
    const internas = this.users().filter(u => this.esAdministrativa(u)).length;
    return [
      { id: 'administrativas', etiqueta: 'Administrativas', conteo: internas },
      { id: 'externas', etiqueta: 'Externas', conteo: this.users().length - internas },
    ];
  });

  /** Una cuenta es administrativa si tiene algún rol interno del Ministerio. */
  esAdministrativa(usuario: UsuarioDelSistema): boolean {
    return this.rolesDe(usuario).some(rol => ROLES_INTERNOS.includes(rol));
  }

  /**
   * Si hay algún filtro puesto que pueda estar escondiendo cuentas que sí existen.
   *
   * La pestaña de ámbito no cuenta: administrativas y externas son dos listas, no un filtro sobre
   * una. La nombra `ambitoDeLaLista`, para no decir que no hay cuentas cuando las hay del otro lado.
   */
  readonly hayFiltrosPuestos = computed(
    () => this.busqueda().trim().length > 0 || this.filtroRol() !== 'todos' || this.filtroEstado() !== 'todos',
  );

  readonly ambitoDeLaLista = computed(() => (this.ambito() === 'externas' ? 'externas' : 'administrativas'));

  /** Quita la búsqueda, el rol y el estado; deja la pestaña de ámbito donde está. */
  limpiarFiltros(): void {
    this.busqueda.set('');
    this.filtroRol.set('todos');
    this.filtroEstado.set('todos');
  }

  readonly usuariosFiltrados = computed(() => {
    const consulta = this.busqueda().trim().toLocaleLowerCase('es');
    const administrativas = this.ambito() === 'administrativas';
    return this.users()
      .filter(usuario => this.esAdministrativa(usuario) === administrativas)
      .filter(usuario => !consulta
        || usuario.fullName.toLocaleLowerCase('es').includes(consulta)
        || usuario.email.toLocaleLowerCase('es').includes(consulta))
      .filter(usuario => this.filtroRol() === 'todos' || this.rolesDe(usuario).includes(this.filtroRol()))
      .filter(usuario => this.filtroEstado() === 'todos'
        || (this.filtroEstado() === 'activos' ? usuario.isActive : !usuario.isActive))
      .sort((a, b) => a.fullName.localeCompare(b.fullName, 'es'));
  });

  /** Lo que se pinta: lo filtrado, ordenado por la columna elegida. */
  readonly usuariosVisibles = computed(() => this.orden.ordenarFilas(this.usuariosFiltrados(), user => {
    switch (this.orden.columna()) {
      case 'usuario': return user.fullName || user.email;
      // Por el PRIMER rol, que es lo que la celda enseña primero. Sin rol va al final, como
      // cualquier fila a la que le falta el dato por el que se ordena.
      case 'rol': return this.rolesDe(user).map(rol => this.nombreDelRol(rol)).sort().join(', ');
      case 'estado': return user.isActive ? 'Activo' : 'Inactivo';
      default: return null;
    }
  }));

  /**
   * Cuántas cuentas hay en el ÁMBITO que se está viendo, y cuántas de ellas activas.
   *
   * Eran del sistema entero, así que el resumen decía «4 cuentas de 22 · 22 activas» con la pestaña
   * de administrativas puesta: una cifra filtrada y otra global en la misma línea. Es el mismo
   * defecto que se corrigió el mismo día en las pestañas de categoría de la Agenda.
   */
  readonly totalDelAmbito = computed(() => {
    const administrativas = this.ambito() === 'administrativas';
    return this.users().filter(u => this.esAdministrativa(u) === administrativas).length;
  });

  readonly totalActivos = computed(() => {
    const administrativas = this.ambito() === 'administrativas';
    return this.users().filter(u => this.esAdministrativa(u) === administrativas && u.isActive).length;
  });

  // Icon references
  LucideUserCog = LucideUserCog;
  LucideX = LucideX;

  adminRoles = Object.values(ADMIN_ROLES);

  ROLE_STYLES: Record<string, string> = {
    webmaster: 'text-violet-700 bg-violet-50 border border-violet-200',
    gestor_interno: 'text-slate-600 bg-slate-100 border border-slate-200',
  };

  /**
   * En qué punto va la lectura del registro de cuentas.
   *
   * <b>SIN ESTO, LA LISTA MENTIA DURANTE LA ESPERA.</b> Mientras el servidor respondía, la tabla
   * estaba vacía y la pantalla afirmaba que no hay cuentas; y si la lectura fallaba, se quedaba
   * afirmándolo para siempre. Una lista vacía y una lista que todavía no se ha podido leer no son
   * el mismo hecho, y es lo que distinguen los tres estados de `app-estado-de-lista`.
   */
  readonly cargando = signal(false);
  readonly errorDeLaLista = signal('');

  cargarLasCuentas() {
    this.cargando.set(true);
    this.errorDeLaLista.set('');
    this.adminService.cargarCuentasAdministrativas().subscribe({
      next: (payload) => {
        this.users.set(payload || []);
        this.cargando.set(false);
      },
      error: (err) => {
        this.errorDeLaLista.set(err.message || 'No fue posible leer el registro de cuentas.');
        this.cargando.set(false);
      }
    });
  }

  /**
   * Las acciones que esta fila admite hoy.
   *
   * <b>ANTES ERAN DOS BOTONES SUELTOS Y UNO ESCONDIDO.</b> «Ver ficha» en texto, «Editar» como un
   * icono sin rótulo —que obliga a adivinar o a pasar el ratón— y «Desactivar» solo dentro del
   * diálogo de la ficha, de modo que para retirarle el acceso a alguien había que abrirlo primero.
   * Los demás paneles de la consola —Festivales, organizaciones, noticias, agenda y catálogo— usan
   * todos el mismo menú, y este no.
   *
   * <b>«DESACTIVAR» SOLO SI ESTÁ ACTIVO.</b> Es la regla de la consola: una acción se muestra si de
   * verdad está disponible para el estado actual del registro. Ofrecerla sobre una cuenta ya
   * inactiva lleva a una confirmación que no cambia nada.
   *
   * VER LA FICHA ES LA ACCIÓN PRINCIPAL —lo que se espera de una fila de esta tabla— y por eso sale
   * fuera del desplegable.
   */
  accionesDe(usuario: UsuarioDelSistema): AccionDeRegistro[] {
    const acciones: AccionDeRegistro[] = [
      { id: 'ficha', etiqueta: 'Abrir ficha', tono: 'principal' },
      { id: 'editar', etiqueta: 'Editar' },
    ];
    if (usuario.isActive) {
      acciones.push({ id: 'desactivar', etiqueta: 'Desactivar', tono: 'peligro' });
    } else {
      // UNA CUENTA INACTIVA SE REACTIVA O SE ELIMINA. Reactivar no tenía acción: había que abrir el
      // editor y marcar una casilla. Y eliminar no existía: lo pidió la dirección de producto el 15 de
      // septiembre de 2026. Solo sobre una cuenta ya desactivada, para que borrar no sea un clic
      // desde una cuenta viva; el servidor además se niega si la cuenta actuó alguna vez.
      acciones.push({ id: 'reactivar', etiqueta: 'Reactivar' });
      acciones.push({ id: 'eliminar', etiqueta: 'Eliminar la cuenta', tono: 'peligro' });
    }
    return acciones;
  }

  ejecutarAccion(usuario: UsuarioDelSistema, accion: string): void {
    switch (accion) {
      case 'ficha': this.verFicha(usuario); break;
      case 'editar': this.startEdit(usuario); break;
      case 'desactivar': this.desactivar(usuario); break;
      case 'reactivar': this.reactivar(usuario); break;
      case 'eliminar': this.eliminar(usuario); break;
    }
  }

  /**
   * Los módulos de la consola que tiene activados la cuenta abierta.
   *
   * <b>SE PIDEN AL ABRIR LA FICHA</b> y no con la lista: son una consulta por cuenta, y la lista
   * tiene veintidós. Es la misma decisión que ya tomó el historial de esta misma ficha.
   */
  readonly modulosDeLaCuenta = signal<ModulosDeUnaCuenta | null>(null);

  /** El catálogo: qué módulos existen y cuáles no se pueden quitar. */
  readonly catalogoDeModulos = signal<CatalogoDeModulos | null>(null);
  readonly guardandoModulos = signal(false);
  readonly errorDeModulos = signal<string | null>(null);

  /**
   * Las casillas que se dibujan, con su rótulo y si se puede tocar.
   *
   * EL ROTULO SALE DE LA NAVEGACION, que es donde ya vive el nombre que la persona lee en la barra.
   * Escribirlo aquí otra vez sería una segunda lista de nombres esperando a divergir.
   */
  readonly casillasDeModulo = computed(() => {
    const catalogo = this.catalogoDeModulos();
    const suyos = this.modulosDeLaCuenta();
    if (!catalogo || !suyos) { return []; }

    const activos = new Set(suyos.modulos);
    const siempre = new Set(catalogo.siempreActivados);
    // SOLO LO QUE ABRE ALGO. El catálogo del servidor puede conocer un módulo que la barra no
    // ofrece —«galeria», retirado de la navegación mientras no exista—;
    // dar una casilla para él sería entregar una llave de una puerta que no está.
    return catalogo.modulos.filter(codigo => this.tieneSeccion(codigo)).map(codigo => ({
      codigo,
      etiqueta: this.nombreDelModulo(codigo),
      activo: activos.has(codigo),
      // NI LOS DE SIEMPRE NI LOS DE UN WEBMASTER SE PUEDEN TOCAR, y la casilla lo dice estando
      // puesta y deshabilitada en vez de desaparecer: si desapareciera parecería que no existe.
      fijo: siempre.has(codigo) || suyos.todosPorSerWebmaster,
    }));
  });

  /**
   * Las casillas, agrupadas como la barra izquierda: por familia, con el nombre y la descripción
   * de cada apartado.
   *
   * <b>ERA UNA LISTA PLANA DE DIECISEIS CASILLAS</b> al pie de una tarjeta, sin decir qué abre cada
   * una ni a qué familia pertenece. El criterio es este: * «el módulo de activar o desactivar apartados… debe ser más visual y estructurado desde un
   * apartado de gestión claro». La estructura ya existe —es la de la navegación— y se reutiliza;
   * escribirla aquí otra vez sería una segunda lista esperando a divergir.
   */
  readonly gruposDeModulos = computed(() => {
    const casillas = new Map(this.casillasDeModulo().map(casilla => [casilla.codigo, casilla]));
    return GRUPOS_DE_GESTION_ADMINISTRATIVA
      .map(grupo => ({
        id: grupo.id,
        titulo: grupo.titulo,
        modulos: grupo.secciones
          .filter(seccion => casillas.has(seccion.id))
          .map(seccion => ({ ...casillas.get(seccion.id)!, descripcion: seccion.descripcion })),
      }))
      .filter(grupo => grupo.modulos.length > 0);
  });

  /** Cuántos apartados puede abrir la cuenta, de cuántos hay: la línea que resume la gestión. */
  readonly resumenDeModulos = computed(() => {
    const casillas = this.casillasDeModulo();
    return { activos: casillas.filter(casilla => casilla.activo).length, total: casillas.length };
  });

  /** El nombre con el que ese módulo aparece en la barra izquierda. */
  nombreDelModulo(codigo: string): string {
    return this.seccionDe(codigo)?.titulo ?? codigo;
  }

  private tieneSeccion(codigo: string): boolean {
    return this.seccionDe(codigo) !== undefined;
  }

  private seccionDe(codigo: string) {
    for (const grupo of GRUPOS_DE_GESTION_ADMINISTRATIVA) {
      const seccion = grupo.secciones.find(s => s.id === codigo);
      if (seccion) { return seccion; }
    }
    return undefined;
  }

  private cargarModulosDe(usuario: UsuarioDelSistema): void {
    this.errorDeModulos.set(null);
    this.modulosDeLaCuenta.set(null);

    if (!this.catalogoDeModulos()) {
      this.adminService.cargarModulosDisponibles().subscribe({
        next: catalogo => this.catalogoDeModulos.set(catalogo),
        error: () => this.errorDeModulos.set('No fue posible consultar los módulos de la consola.'),
      });
    }

    this.adminService.cargarModulosDeCuenta(usuario.id).subscribe({
      next: suyos => this.modulosDeLaCuenta.set(suyos),
      error: () => this.errorDeModulos.set('No fue posible consultar los módulos de esta cuenta.'),
    });
  }

  /**
   * Activa o desactiva un módulo y lo guarda.
   *
   * SE GUARDA EL CONJUNTO ENTERO, no la diferencia: lo que la persona decide es qué casillas quedan
   * puestas. Ver `guardarModulosDeCuenta`.
   */
  alternarModulo(codigo: string): void {
    // UN APARTADO FIJO NO SE ALTERNA. Su casilla se ve puesta y deshabilitada —no desaparece,
    // porque desaparecer haría creer que no existe—, y pulsarla no puede mandar nada.
    if ((this.catalogoDeModulos()?.siempreActivados ?? []).includes(codigo)) { return; }
    this.cambiarApartados(activos => {
      if (activos.has(codigo)) { activos.delete(codigo); } else { activos.add(codigo); }
    });
  }

  /**
   * Marcar o desmarcar un grupo entero de la barra izquierda.
   *
   * <b>PORQUE ASI SE CONCEDE DE VERDAD.</b> Quien entrega una cuenta nueva no elige apartado por
   * apartado: decide «esta persona lleva Publicaciones» o «esta lleva el Ecosistema». Hacerlo con
   * catorce casillas eran catorce gestos y catorce peticiones; y como cada una guardaba por su
   * cuenta, a media faena la cuenta quedaba con un conjunto que nadie había decidido.
   */
  alternarGrupo(id: string, encender: boolean): void {
    const grupo = this.gruposDeModulos().find(g => g.id === id);
    if (!grupo) { return; }
    const suyos = grupo.modulos.filter(modulo => !modulo.fijo).map(modulo => modulo.codigo);
    this.cambiarApartados(activos => {
      for (const codigo of suyos) {
        if (encender) { activos.add(codigo); } else { activos.delete(codigo); }
      }
    });
  }

  /**
   * Cierto cuando en ese grupo hay algo que conceder o que quitar.
   *
   * <b>«BANDEJA DE TRABAJO» NO LO TIENE:</b> sus dos apartados van siempre, así que un «Todo el
   * grupo» ahí sería un control que promete una acción que no puede hacer. Una acción solo se
   * muestra si de verdad está disponible.
   */
  grupoSeDecide(id: string): boolean {
    const grupo = this.gruposDeModulos().find(g => g.id === id);
    return !!grupo && grupo.modulos.some(modulo => !modulo.fijo);
  }

  /** Cierto cuando todo lo que se puede conceder de ese grupo ya está concedido. */
  grupoCompleto(id: string): boolean {
    const grupo = this.gruposDeModulos().find(g => g.id === id);
    if (!grupo) { return false; }
    const elegibles = grupo.modulos.filter(modulo => !modulo.fijo);
    return elegibles.length > 0 && elegibles.every(modulo => modulo.activo);
  }

  /** Todos los que se pueden conceder, de una vez. */
  concederTodo(): void {
    const todos = this.casillasDeModulo().map(casilla => casilla.codigo);
    this.cambiarApartados(activos => { for (const codigo of todos) { activos.add(codigo); } });
  }

  /**
   * Dejar solo los dos que vienen siempre.
   *
   * NO ES «QUITARLE TODO»: Resumen operativo y Solicitudes y revisiones no se pueden quitar, así
   * que el verbo dice lo que de verdad pasa. Prometer que se queda sin nada sería mentir.
   */
  dejarSoloLoMinimo(): void {
    const siempre = this.catalogoDeModulos()?.siempreActivados ?? [];
    this.cambiarApartados(activos => {
      activos.clear();
      for (const codigo of siempre) { activos.add(codigo); }
    });
  }

  /**
   * El único sitio donde se guarda el conjunto de apartados.
   *
   * <b>SE MANDA EL CONJUNTO ENTERO Y NO LA DIFERENCIA</b>, igual que lo espera el servidor: lo que
   * la persona decide es qué puede abrir la cuenta, no qué se añade o se quita. Con dos operaciones,
   * dos pestañas abiertas podrían dejar un estado que nadie eligió.
   */
  private cambiarApartados(ajustar: (activos: Set<string>) => void): void {
    const usuario = this.usuarioSeleccionado();
    const suyos = this.modulosDeLaCuenta();
    if (!usuario || !suyos || this.guardandoModulos()) { return; }
    // A UN WEBMASTER NO SE LE TOCAN: los tiene por lo que es, no por concesión.
    if (suyos.todosPorSerWebmaster) { return; }

    // SOLO SE REENVIA LO QUE EL CATALOGO CONOCE. Una cuenta puede conservar en la base un módulo
    // que ya no existe —«galeria» salió de la barra y del catálogo— y
    // el servidor rechaza con razón lo que no conoce; reenviarlo dejaría la casilla sin efecto.
    const catalogo = this.catalogoDeModulos();
    const conocidos = new Set(catalogo?.modulos ?? []);
    const siempre = new Set(catalogo?.siempreActivados ?? []);
    const activos = new Set(suyos.modulos.filter(modulo => conocidos.has(modulo)));
    ajustar(activos);
    // LOS FIJOS VIAJAN CON EL RESTO. Lo que se manda es el conjunto que la persona ve marcado, y
    // ellos están marcados: quitarlos del envío convertiría el «conjunto entero» en una
    // diferencia, que es justo lo que el servidor no espera.
    for (const codigo of siempre) { activos.add(codigo); }

    this.guardandoModulos.set(true);
    this.errorDeModulos.set(null);
    this.apartadosGuardados.set(false);
    this.adminService.guardarModulosDeCuenta(usuario.id, [...activos]).subscribe({
      next: actualizados => {
        this.modulosDeLaCuenta.set(actualizados);
        this.guardandoModulos.set(false);
        this.apartadosGuardados.set(true);
        // Y LA LISTA DE ATRAS SE ENTERA: su cifra decía lo de antes, y esa contradicción entre dos
        // sitios de la misma pantalla es justo lo que el proyecto viene corrigiendo.
        this.refrescarLaCifraDeApartados(usuario.id, actualizados.modulos.length);
      },
      error: () => {
        this.errorDeModulos.set('No fue posible guardar. Los apartados siguen como estaban.');
        this.guardandoModulos.set(false);
      },
    });
  }

  /** Lo que la fila de la lista enseña, puesto al día sin volver a pedir las cuentas. */
  private refrescarLaCifraDeApartados(id: string, cuantos: number): void {
    this.users.update(usuarios => usuarios.map(usuario =>
      usuario.id === id ? { ...usuario, apartadosActivos: cuantos } : usuario));
  }

  /** Se acaba de guardar: lo dice la pantalla, y se calla en cuanto se cambia otra cosa. */
  readonly apartadosGuardados = signal(false);

  private pedirTiposDeDocumento(): void {
    if (this.tiposDeDocumento().length > 0) { return; }
    this.adminService.cargarTiposDeDocumento().subscribe({
      next: tipos => this.tiposDeDocumento.set(tipos),
      // SI NO LLEGAN, LA LISTA SE QUEDA VACIA Y EL CAMPO OFRECE «Sin documento registrado»: el
      // formulario se sigue pudiendo guardar, que es lo que importa. Un catálogo que no carga no
      // puede bloquear el alta de una cuenta.
      error: () => this.tiposDeDocumento.set([]),
    });
  }

  verFicha(usuario: UsuarioDelSistema): void {
    this.usuarioSeleccionado.set(usuario);
    // SOLO PARA CUENTAS ADMINISTRATIVAS: una cuenta externa no entra a la consola, así que
    // ofrecerle módulos sería ofrecerle llaves de una puerta por la que no pasa.
    if (this.esAdministrativa(usuario)) { this.cargarModulosDe(usuario); }
    else { this.modulosDeLaCuenta.set(null); }
  }

  cerrarFicha(): void {
    this.usuarioSeleccionado.set(null);
    this.historialAbierto.set(null);
    this.modulosDeLaCuenta.set(null);
    this.errorDeModulos.set(null);
  }

  desactivar(usuario: UsuarioDelSistema): void {
    if (!usuario.isActive) return;
    this.pedirConfirmacion('desactivar', usuario);
  }

  private desactivarDeVerdad(usuario: UsuarioDelSistema): void {
    this.message.set('Desactivando usuario…');
    this.adminService.desactivarCuenta(usuario.id).subscribe({
      next: () => {
        const actualizado = { ...usuario, isActive: false };
        this.users.update(usuarios => usuarios.map(item => item.id === usuario.id ? actualizado : item));
        this.usuarioSeleccionado.set(actualizado);
        this.message.set('Usuario desactivado correctamente.');
        this.verFicha(actualizado);
        this.cerrarLaConfirmacion();
      },
      error: error => this.fallaLaConfirmacion(error, 'No fue posible desactivar el usuario.'),
    });
  }

  /** Devolverle el acceso a una cuenta desactivada, sin abrir el editor para marcar una casilla. */
  reactivar(usuario: UsuarioDelSistema): void {
    if (usuario.isActive || this.guardando()) return;
    this.guardando.set(true);
    this.message.set('Reactivando usuario…');
    this.adminService.guardarCuentaAdministrativa({
      id: usuario.id,
      fullName: usuario.fullName,
      email: usuario.email,
      roles: this.rolesDe(usuario),
      password: '',
      isActive: true,
      identificacion: usuario.identificacion ?? '',
      tipoDocumento: usuario.tipoDocumento ?? '',
      telefono: usuario.telefono ?? '',
    }).subscribe({
      next: respuesta => {
        this.guardando.set(false);
        const actualizado = respuesta.user;
        this.users.update(usuarios => usuarios.map(item => item.id === usuario.id ? actualizado : item));
        if (this.usuarioSeleccionado()?.id === usuario.id) { this.verFicha(actualizado); }
        this.message.set('Usuario reactivado: vuelve a poder entrar.');
      },
      error: error => {
        this.guardando.set(false);
        this.message.set(this.mensajeDelServidor(error, 'No fue posible reactivar el usuario.'));
      },
    });
  }

  /**
   * Eliminar la cuenta de verdad.
   *
   * SOLO SOBRE UNA CUENTA DESACTIVADA, y el servidor decide si puede: si la cuenta actuó alguna vez
   * —entró, publicó, revisó— la bitácora la nombra y eliminarla sería perder la trazabilidad; el
   * servidor se niega y dice cuántas veces actuó. Lo que sí se elimina es lo que nunca hizo nada:
   * la cuenta creada por error, la de prueba.
   */
  eliminar(usuario: UsuarioDelSistema): void {
    if (usuario.isActive) return;
    this.pedirConfirmacion('eliminar', usuario);
  }

  private eliminarDeVerdad(usuario: UsuarioDelSistema): void {
    this.message.set('Eliminando la cuenta…');
    this.adminService.eliminarCuentaDefinitivamente(usuario.id).subscribe({
      next: () => {
        this.users.update(usuarios => usuarios.filter(item => item.id !== usuario.id));
        if (this.usuarioSeleccionado()?.id === usuario.id) { this.cerrarFicha(); }
        this.message.set(`La cuenta de ${usuario.fullName || usuario.email} quedó eliminada.`);
        this.cerrarLaConfirmacion();
      },
      // EL DIALOGO NO SE CIERRA CUANDO EL SERVIDOR SE NIEGA. Su negativa es informativa —«tiene 2
      // actuaciones en la bitácora»— y cerrarlo dejaría el motivo en una franja lejos de la acción.
      error: error => this.fallaLaConfirmacion(error, 'No fue posible eliminar la cuenta.'),
    });
  }

  /** El motivo del servidor cuando lo da —«tiene 2 actuaciones en la bitácora»—, y si no, el nuestro. */
  private mensajeDelServidor(error: unknown, respaldo: string): string {
    const cuerpo = (error as { payload?: { message?: string } } | null)?.payload;
    if (typeof cuerpo?.message === 'string' && cuerpo.message.trim()) { return cuerpo.message; }
    const mensaje = (error as { message?: string } | null)?.message;
    return typeof mensaje === 'string' && mensaje.trim() ? mensaje : respaldo;
  }

  /**
   * Lo que está esperando confirmación.
   *
   * <b>SE CONFIRMA EN UN DIALOGO DEL PROYECTO, NO EN UNO DEL SISTEMA OPERATIVO.</b> Hasta el 17 de
   * septiembre de 2026 estas tres decisiones —desactivar desde la ficha, desactivar guardando el
   * formulario y eliminar la cuenta— se preguntaban con `window.confirm`: tipografía ajena, botones
   * «Aceptar/Cancelar» que nadie eligió, sin foco atrapado, imposibles de comprobar en una prueba y
   * silenciosamente bloqueables por el navegador, que es la peor de todas: la acción no ocurría y
   * nadie se enteraba de por qué.
   */
  readonly confirmacion = signal<{ que: 'desactivar' | 'desactivar-al-guardar' | 'eliminar'; usuario: UsuarioDelSistema } | null>(null);

  /** Lo que el servidor contestó al negarse, dentro del propio diálogo. */
  readonly errorDeLaConfirmacion = signal('');

  private pedirConfirmacion(que: 'desactivar' | 'desactivar-al-guardar' | 'eliminar', usuario: UsuarioDelSistema): void {
    this.errorDeLaConfirmacion.set('');
    this.confirmacion.set({ que, usuario });
  }

  /** El verbo de lo que va a pasar. */
  readonly tituloDeLaConfirmacion = computed(() =>
    this.confirmacion()?.que === 'eliminar' ? 'Eliminar la cuenta' : 'Desactivar la cuenta');

  /** Qué se pierde, qué queda y si puede revertirse. */
  readonly detalleDeLaConfirmacion = computed(() => {
    const pendiente = this.confirmacion();
    if (!pendiente) return '';
    const quien = pendiente.usuario.fullName || pendiente.usuario.email || 'esta cuenta';
    if (pendiente.que === 'eliminar') {
      return `La cuenta de ${quien} desaparece con sus roles y sus módulos, y no se puede deshacer. ` +
        'Si actuó alguna vez, el sistema se negará para no perder la trazabilidad de lo que hizo.';
    }
    return `${quien} perderá el acceso al Espacio de Gestión Administrativa en cuanto confirmes. ` +
      'La cuenta se conserva con su historial y puede reactivarse después.';
  });

  readonly accionDeLaConfirmacion = computed(() =>
    this.confirmacion()?.que === 'eliminar' ? 'Eliminar la cuenta' : 'Desactivar');

  confirmarLaDecision(): void {
    const pendiente = this.confirmacion();
    if (!pendiente) return;
    this.errorDeLaConfirmacion.set('');
    if (pendiente.que === 'eliminar') { this.eliminarDeVerdad(pendiente.usuario); return; }
    if (pendiente.que === 'desactivar') { this.desactivarDeVerdad(pendiente.usuario); return; }
    this.guardarLaCuenta();
  }

  cerrarLaConfirmacion(): void {
    this.confirmacion.set(null);
    this.errorDeLaConfirmacion.set('');
  }

  /** El servidor se negó: el diálogo sigue abierto con su explicación. */
  private fallaLaConfirmacion(error: unknown, respaldo: string): void {
    const explicacion = this.mensajeDelServidor(error, respaldo);
    this.message.set(explicacion);
    if (this.confirmacion()) { this.errorDeLaConfirmacion.set(explicacion); }
  }

  enviarElFormulario() {
    // Con el boton deshabilitado todavia queda una via de doble envio: pulsar
    // Enter dentro de un campo del formulario. Este guardado corta esa via.
    if (this.guardando()) return;

    // Unica accion destructiva del panel: desmarcar «Usuario Activo» y guardar
    // deja a una persona sin acceso al panel, y desde aqui no hay deshacer. Se
    // pregunta solo en ese paso —de activo a inactivo— nombrando a quien
    // afecta; guardar el nombre, el correo, el rol o la contrasena no pregunta
    // nada, porque una confirmacion de mas ensena a aceptar sin leer.
    const idEnEdicion = this.cuentaEnEdicion();
    if (idEnEdicion && !this.valoresDelFormulario().isActive) {
      const original = this.users().find((u) => u.id === idEnEdicion);
      if (original?.isActive) {
        this.pedirConfirmacion('desactivar-al-guardar', original);
        return;
      }
    }

    this.guardarLaCuenta();
  }

  /** El guardado de verdad, ya confirmado lo que hubiera que confirmar. */
  private guardarLaCuenta(): void {
    // Dos formas de enviar algo que el API va a rechazar. Pararlas aquí ahorra el viaje y, sobre
    // todo, deja el mensaje pegado al campo en vez de en la franja de error general.
    if (this.valoresDelFormulario().roles.length === 0) {
      this.message.set('Hay que marcar al menos un rol.');
      return;
    }

    if (this.mezclaProhibida()) {
      this.message.set('«Usuario externo» no puede combinarse con un rol interno del Ministerio.');
      return;
    }

    const problema = this.problemaDelPerfil();
    if (problema) {
      this.errorDelPerfil.set(problema);
      return;
    }

    this.guardando.set(true);
    this.message.set('Guardando usuario...');

    // Construct payload
    const payload = {
      ...this.valoresDelFormulario(),
      id: this.cuentaEnEdicion() || undefined
    };

    this.adminService.guardarCuentaAdministrativa(payload).subscribe({
      next: (response) => {
        this.guardando.set(false);
        const savedUser = response.user;
        const current = this.users();
        
        // Update user in local array
        const index = current.findIndex(u => u.id === savedUser.id);
        if (index > -1) {
          const updated = [...current];
          updated[index] = savedUser;
          this.users.set(updated);
        } else {
          this.users.set([savedUser, ...current]);
        }
        
        this.limpiarElFormulario();
        this.editorAbierto.set(false);
        this.message.set('Usuario guardado correctamente.');
        this.cerrarLaConfirmacion();
      },
      error: (err) => {
        this.guardando.set(false);
        this.fallaLaConfirmacion(err, 'Error al guardar el usuario');
      }
    });
  }

  startEdit(user: UsuarioDelSistema) {
    this.cuentaEnEdicion.set(user.id);
    this.valoresDelFormulario.set({
      fullName: user.fullName || '',
      email: user.email || '',
      // SIN RESPALDO A 'gestor_interno'. Una cuenta que llegue sin roles es una cuenta rota, y
      // rellenarle uno por omisión haría que abrir su ficha y guardarla le CONCEDIERA un rol que
      // nadie decidió darle. Se abre vacía, la pantalla lo dice y quien administra elige.
      roles: this.rolesDe(user),
      password: '',
      isActive: user.isActive ?? true,
      // LO QUE YA TIENE, PARA PODER CORREGIRLO. Abrir la ficha con los campos en blanco haría que
      // guardar un cambio de rol borrara su identificación sin que nadie lo pidiera.
      identificacion: user.identificacion ?? '',
      tipoDocumento: user.tipoDocumento ?? '',
      telefono: user.telefono ?? '',
    });
    this.errorDelPerfil.set(null);
    this.pedirTiposDeDocumento();
    this.message.set('');
    this.editorAbierto.set(true);
  }

  startCreate() {
    this.cuentaEnEdicion.set(null);
    this.valoresDelFormulario.set({
      fullName: '',
      email: '',
      roles: ['gestor_interno'],
      password: '',
      isActive: true,
      identificacion: '',
      tipoDocumento: '',
      telefono: '',
    });
    this.errorDelPerfil.set(null);
    this.message.set('');
    this.editorAbierto.set(true);
    this.pedirTiposDeDocumento();
  }

  cerrarElEditor() {
    this.editorAbierto.set(false);
    this.cuentaEnEdicion.set(null);
    this.message.set('');
  }

  limpiarElFormulario() {
    this.valoresDelFormulario.set({
      fullName: '',
      email: '',
      roles: ['gestor_interno'],
      password: '',
      isActive: true,
      identificacion: '',
      tipoDocumento: '',
      telefono: '',
    });
    this.errorDelPerfil.set(null);
    this.cuentaEnEdicion.set(null);
  }

  // ---------------------------------------------------------------- roles, en conjunto

  /**
   * Los roles de una fila del listado, entendiendo también respuestas anteriores a la transicion.
   *
   * El parámetro va tipado y no como `any` aunque `users()` sí lo sea: esto es lo único que este
   * componente necesita saber de una fila, y escribirlo aquí es lo que impide que el trinquete de
   * tipado suba un peldaño por una función nueva.
   */
  rolesDe(user: { role?: string | null; roles?: readonly string[] | null } | null | undefined): string[] {
    return rolesDeSesion(user);
  }

  tieneRol(roleId: string): boolean {
    return this.valoresDelFormulario().roles.includes(roleId);
  }

  /**
   * Marca o desmarca un rol.
   *
   * NO IMPIDE LA COMBINACIÓN PROHIBIDA, la avisa. Bloquear la casilla dejaría a quien administra
   * sin entender por qué no puede marcarla; la plantilla explica el motivo y el API la rechaza.
   * La pantalla no es la guarda: `Permisos.MezclaExternoConInterno` lo es.
   */
  alternarRol(roleId: string, marcado: boolean): void {
    const actuales = this.valoresDelFormulario().roles;
    const siguientes = marcado
      ? Array.from(new Set([...actuales, roleId]))
      : actuales.filter((rol) => rol !== roleId);

    this.valoresDelFormulario.set({ ...this.valoresDelFormulario(), roles: siguientes });
  }

  /** ¿Están marcados a la vez «externo» y un rol interno? */
  mezclaProhibida(): boolean {
    const roles = this.valoresDelFormulario().roles;
    return roles.includes('externo') && roles.some((rol) => ROLES_INTERNOS.includes(rol));
  }

  /**
   * Cómo se lee un rol.
   *
   * <b>UN CODIGO NO ES UN ROL PARA QUIEN LEE.</b> Si el catálogo no lo conoce se devuelve tal cual
   * y no una cadena vacía: un dato raro es más fácil de diagnosticar que un dato ausente, que es el
   * mismo criterio que sigue la bitácora con un verbo que no sabe nombrar.
   */
  nombreDelRol(roleId: string): string {
    return ADMIN_ROLES[roleId]?.shortLabel || roleId;
  }

  /**
   * Los roles de una cuenta, con su nombre.
   *
   * LA FICHA ENSEÑABA EL CODIGO —«gestor_interno»— mientras la lista, dos centímetros a la
   * izquierda, enseñaba «Gestor». La misma cuenta dicha de dos maneras en la misma pantalla.
   */
  etiquetasDeRol(usuario: UsuarioDelSistema): string {
    const roles = this.rolesDe(usuario);
    return roles.length ? roles.map(rol => this.nombreDelRol(rol)).join(', ') : 'Sin rol';
  }

  descripcionDelRol(roleId: string): string {
    return ADMIN_ROLES[roleId]?.description || '';
  }

  COLORES_DE_AVATAR = [
    'bg-violet-600', 'bg-indigo-600', 'bg-blue-600',
    'bg-teal-600', 'bg-emerald-600', 'bg-amber-600', 'bg-rose-600',
  ];

  colorDeAvatar(name = ''): string {
    const hash = String(name).split('').reduce((acc, ch) => acc + ch.charCodeAt(0), 0);
    return this.COLORES_DE_AVATAR[hash % this.COLORES_DE_AVATAR.length];
  }

  inicialesDe(name = ''): string {
    const parts = String(name).trim().split(/\s+/);
    if (parts.length === 1 && parts[0]) return parts[0].slice(0, 2).toUpperCase();
    if (parts.length > 1) return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
    return '';
  }

  fuerzaDeLaClaveEscrita(pwd = ''): number {
    let score = 0;
    if (pwd.length >= 8) score++;
    if (/[A-Z]/.test(pwd)) score++;
    if (/[0-9]/.test(pwd)) score++;
    if (/[^A-Za-z0-9]/.test(pwd)) score++;
    return score;
  }

  fuerzaDeLaClave(pwd = ''): string {
    const labels = ['Muy débil', 'Débil', 'Regular', 'Buena', 'Fuerte'];
    return labels[this.fuerzaDeLaClaveEscrita(pwd)] || '';
  }

  colorDeLaFuerza(pwd = ''): string {
    const colors = ['bg-red-400', 'bg-orange-400', 'bg-amber-400', 'bg-lime-400', 'bg-emerald-400'];
    return colors[this.fuerzaDeLaClaveEscrita(pwd)] || 'bg-slate-200';
  }
}
