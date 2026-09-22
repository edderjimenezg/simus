import { formatDate } from '@angular/common';
import { Component, EventEmitter, Input, OnDestroy, OnInit, computed, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Output } from '@angular/core';
import { RevisionDeCamposStore } from '../../../core/revision-de-campos/revision-de-campos.store';
import { RevisionDeCampos } from '../../../core/revision-de-campos/revision-de-campos';
import { FichaFestivalComponent } from '../../panel-organizacion/ficha-festival/ficha-festival.component';
import { FestivalDeLaOrganizacion, PanelOrganizacionApi } from '../../panel-organizacion/panel-organizacion.api';
import { BotonComponent } from '../../../shared/components/ui/boton/boton.component';
import { SelectorSegmentadoComponent } from '../../../shared/components/ui/selector-segmentado/selector-segmentado.component';
import { SECCIONES_DE_LA_FICHA } from '../../panel-organizacion/ficha-festival/secciones-de-la-ficha';
import { FichaEnRevisionApi } from './ficha-en-revision.api';
import { FECHA_Y_HORA_ADMINISTRATIVA } from '../domain/formatos-de-fecha';
import { ConfirmacionComponent } from '../../../shared/components/ui/confirmacion/confirmacion.component';

/**
 * La revisión institucional del registro y perfil permanente del Festival.
 *
 * <b>ES LA MISMA FICHA, CON LA MISMA FRONTERA.</b> Monta el bloque canónico que llena la
 * organización. No abre el recorrido heredado de versiones ni presenta Ediciones como parte del
 * registro: una Edición es una realización autónoma, de publicación directa y supervisión posterior.
 *
 * <b>LOS DOS PROVEEDORES SON LO QUE HACE QUE FUNCIONE.</b>
 * <ul>
 *   <li>{@link PanelOrganizacionApi} apunta a {@link FichaEnRevisionApi}, que lee por
 *       `/institucional/*` en vez de por `/externo/*`: son dos cookies distintas y la ficha no sabe
 *       —ni tiene por qué saber— cuál de las dos tiene quien la mira.</li>
 *   <li>{@link RevisionDeCamposStore} en modo `revision`, que es lo que hace aparecer «Pedir
 *       cambio» en los cuarenta y siete campos.</li>
 * </ul>
 *
 * <b>SE PROVEEN AQUÍ Y NO EN LA RUTA</b> para que cerrar la ficha los destruya: un almacén de
 * ámbito mayor se quedaría con las notas del Festival anterior, y el siguiente abriría con ellas.
 *
 * <b>GUARDAR NO ENVÍA.</b> Son dos botones y dos rutas: el borrador solo lo ve el funcionario, y la
 * organización no se entera de nada hasta que se pulsa «Enviar». Es lo que permite revisar una
 * ficha larga en dos ratos sin devolverla a medias.
 */
@Component({
  selector: 'app-ficha-en-revision',
  standalone: true,
  imports: [BotonComponent, FormsModule, FichaFestivalComponent, SelectorSegmentadoComponent, ConfirmacionComponent],
  providers: [
    RevisionDeCamposStore,
    { provide: PanelOrganizacionApi, useClass: FichaEnRevisionApi },
  ],
  template: `
    @if (festivalCompleto(); as festival) {
    <!--
      LAS SECCIONES DE LA FICHA, COMO PESTAÑAS Y EN EL SITIO DONDE ANTES ESTABAN LAS DE VISTA.

      La ficha traía su propia navegación: una columna flotada de 12 rem a la izquierda, pegada al
      borde y con los rótulos a 12,5 px, que dentro del panel de Solicitudes dejaba el contenido
      arrinconado y competía con el índice de trámites que ya hay a la izquierda de todo. El dueño
      quedó definido: «que las secciones aparezcan como ahora
      están apareciendo lo de resumen de trámite y ficha completa».

      Cada pestaña lleva cuántos ajustes hay señalados en esa sección: con tres secciones y una
      sola a la vista, sin el recuento no habría forma de saber que queda trabajo en otra.
    -->
    @if (comoPanel) {
      <div class="secciones-de-la-ficha">
        <app-selector-segmentado
          [opciones]="pestanasDeLaFicha()"
          [elegida]="seccionElegida()"
          etiquetaDelGrupo="Secciones de la ficha"
          (elegir)="seccionElegida.set($event)"
        ></app-selector-segmentado>
      </div>
    }
    <app-ficha-festival
      [seccionVisible]="comoPanel ? seccionElegida() : undefined"
      [modoIncrustado]="comoPanel"
      [ocultarEncabezado]="sinEncabezado"
      [festival]="festival"
      [organizacionId]="festival.organizacionPrincipalId || organizacionId"
      [nombreOrganizacion]="nombreOrganizacion"
      tituloPersonalizado="Revisión del registro del Festival"
      descripcionContexto="Revise los datos permanentes del Festival y señale ajustes concretos cuando sean necesarios. Las Ediciones no hacen parte de esta aprobación inicial: se publican directamente por la organización cuando el Festival ya está publicado y quedan sujetas a supervisión posterior."
      [estadoAutoguardado]="estadoAutoguardado()"
      [pulsoAutoguardado]="pulsoAutoguardado()"
      [horaAutoguardado]="horaAutoguardado()"
      [mostrarResumenDeRevision]="!!confirmacionPendiente()"
      (cerrar)="intentarCerrar()">

      <!-- Mientras se confirma una decisión, el contexto del envío es ruido: lo que hay que leer
           es qué se va a mandar, no de qué ciclo venía el registro. -->
      @if (!confirmacionPendiente() && almacen.comparacionEnvios(); as comparacion) {
        <!-- Filete al costado y nada de relleno: dentro del panel, un recuadro de color
             es una tarjeta dentro de otra. -->
        <section contexto-envio data-testid="contexto-del-envio" class="contexto">
          <div class="flex flex-col gap-2 sm:flex-row sm:items-start sm:justify-between">
            <div>
              <p class="rotulo-de-seccion">{{ ordinalEnvio(comparacion.numeroEnvio) }} envío a revisión</p>
              @if (comparacion.esPrimerEnvio) {
                <p class="contexto__texto">Es la entrega inicial y será la referencia para los siguientes ciclos de revisión.</p>
              } @else {
                <p class="contexto__texto">
                  <strong>{{ comparacion.camposModificados }} {{ comparacion.camposModificados === 1 ? 'campo cambió' : 'campos cambiaron' }}</strong>
                  desde el envío anterior.
                  @if (comparacion.ajustesSugeridosAtendidos > 0) {
                    {{ comparacion.ajustesSugeridosAtendidos }} {{ comparacion.ajustesSugeridosAtendidos === 1 ? 'corresponde a un ajuste sugerido' : 'corresponden a ajustes sugeridos' }}.
                  }
                  @if (comparacion.otrosCambios > 0) {
                    {{ comparacion.otrosCambios }} {{ comparacion.otrosCambios === 1 ? 'es un cambio adicional' : 'son cambios adicionales' }}.
                  }
                </p>
              }
            </div>
            <time class="contexto__fecha">Recibido {{ fecha(comparacion.fechaEnvio) }}</time>
          </div>
          @if (comparacion.comparacionParcial) {
            <p class="contexto__matiz">La comparación estructurada comenzó en este ciclo. Se muestran los valores anteriores que quedaron registrados en las sugerencias de ajuste; los demás campos no se reconstruyen.</p>
          } @else if (!comparacion.esPrimerEnvio && comparacion.camposModificados === 0) {
            <p class="contexto__matiz">No se detectaron diferencias de contenido frente al envío anterior.</p>
          }
        </section>
      }

      <!--
        LA CONFIRMACION, EN EL MISMO LENGUAJE QUE EL RESTO.

        Era el único paso del flujo que se quedó sin revisar: un antetítulo a 12 px en tres colores
        según la acción, un título a 24 px con tracking-wide, una lista con filete arriba y abajo,
        y para el caso de las sugerencias un recuadro violeta con línea morada al costado. Es decir,
        casi todo lo que se fue retirando del resto del apartado, en la pantalla donde se toma la
        decisión.

        Ahora usa el rótulo de sección de la casa, el título al tamaño del trámite, el texto corrido
        a 16 px y filetes solo entre las entradas de la lista.
      -->
      @if (confirmacionPendiente(); as accion) {
        <section resumen-decision data-testid="revision-confirmacion" class="confirmacion">
          <p class="rotulo-de-seccion" [class.es-rechazo]="accion === 'rechazar'" [class.es-publicacion]="accion === 'publicar'">
            @if (accion === 'sugerencias') { Confirmar envío de sugerencias }
            @else if (accion === 'publicar') { Confirmar publicación }
            @else { Confirmar rechazo }
          </p>
          <h2 class="confirmacion__titulo">
            @if (accion === 'sugerencias') { Ajustes sugeridos para {{ nombreFestival }} }
            @else if (accion === 'publicar') { Publicar {{ nombreFestival }} }
            @else { Rechazar {{ nombreFestival }} }
          </h2>
          @if (accion === 'sugerencias') {
            <p class="confirmacion__texto">Se enviarán <strong>{{ almacen.cuantasNotas() === 1 ? '1 ajuste sugerido' : almacen.cuantasNotas() + ' ajustes sugeridos' }}</strong> a la organización <strong>{{ nombreOrganizacion || 'sin identificar' }}</strong>.</p>
            <ol class="confirmacion__lista">
              @for (ajuste of almacen.paraGuardar(observacionGeneral).observaciones; track ajuste.campoId) {
                <li>
                  <p class="confirmacion__campo">{{ ajuste.campoEtiqueta }} <span>{{ etiquetaDeSeccion(ajuste.seccionId) }}</span></p>
                  <p class="confirmacion__nota">{{ ajuste.nota }}</p>
                </li>
              }
            </ol>
            @if (observacionGeneral.trim()) {
              <p class="rotulo-de-seccion confirmacion__aparte">Observación general</p>
              <p class="confirmacion__nota">{{ observacionGeneral }}</p>
            }
          } @else if (accion === 'publicar') {
            <p class="confirmacion__texto">El Festival quedará visible en el portal público. Esta decisión se registrará en su trazabilidad institucional.</p>
          } @else {
            <p class="rotulo-de-seccion confirmacion__aparte es-rechazo">Motivo del rechazo</p>
            <p class="confirmacion__nota">{{ motivoRechazo.trim() }}</p>
          }
        </section>
      }

      <div pie-de-revision class="flex w-full flex-col gap-3">
        @if (almacen.error()) {
          <p data-testid="revision-error" role="alert" class="rounded-lg bg-rose-50 px-3 py-2 text-sm text-rose-800">
            {{ almacen.error() }}
          </p>
        }
        @if (almacen.mensaje() && !esMensajeDeAutoguardado()) {
          <p data-testid="revision-mensaje" role="status" class="rounded-lg bg-verde-texto/10 px-3 py-2 text-sm text-verde-texto">
            {{ almacen.mensaje() }}
          </p>
        }

        @if (almacen.yaEnviada()) {
          <!--
            YA SALIO: la lista esta en manos de la organizacion y no se toca. Reescribir una nota que
            la otra parte ya esta atendiendo cambiaria el encargo a mitad de camino.
          -->
          <p data-testid="revision-ya-enviada" class="rounded-lg bg-amber-50 px-3 py-2 text-sm text-amber-900">
            Las sugerencias de ajuste se enviaron el {{ fecha(almacen.revision()?.fechaEnvio) }}
            @if (almacen.revision()?.destinatarioNombre) {
              a {{ almacen.revision()?.destinatarioNombre }}
            }
            · {{ almacen.cuantasPendientes() }} de {{ almacen.cuantasNotas() }} sin atender.
          </p>
        } @else if (!confirmacionPendiente()) {
          <!--
            LO QUE SE DEVUELVE, EN UN SOLO SITIO.

            Antes esto eran tres cosas sueltas al pie: una etiqueta con su área de texto siempre
            abierta, una frase larga de instrucciones metida a la izquierda de los botones, y los
            botones. Las sugerencias por campo —lo que de verdad se devuelve— no aparecían por
            ningún lado: vivían dispersas entre los cuarenta y siete campos y solo se veían juntas
            al confirmar el envío.

            Ahora es un bloque con nombre: qué se ha señalado, en qué campos —cada uno lleva a su
            campo—, y la observación general debajo. Mejorado a petición
            criterio: «revisa todo el tema de la observación general, de las
            observaciones específicas».
          -->
          <section class="devolucion" aria-label="Lo que se devuelve a la organización">
            <p class="devolucion__titulo">Lo que se le devuelve a la organización</p>

            <p data-testid="revision-recuento" class="devolucion__recuento">
              @if (almacen.cuantasNotas() === 0) {
                <span class="devolucion__vacio">Ningún campo señalado todavía.</span>
                Señala únicamente los datos permanentes que requieran ajuste; sin ninguno señalado,
                el registro se publica tal como llegó.
              } @else {
                <b>{{ almacen.cuantasNotas() }}</b>
                {{ almacen.cuantasNotas() === 1 ? 'campo señalado' : 'campos señalados' }}
                @if (almacen.sinGuardar()) { <span class="devolucion__pendiente">· sin guardar</span> }
              }
            </p>

            @if (almacen.cuantasNotas() > 0) {
              <!-- Cada nombre lleva a su campo: es la forma de releer lo escrito sin recorrer la
                   ficha entera buscando cuál de los cuarenta y siete se anotó. -->
              <ul class="devolucion__campos" data-testid="revision-campos-senalados">
                @for (ajuste of almacen.paraGuardar(observacionGeneral).observaciones; track ajuste.campoId) {
                  <li>
                    <button type="button" (click)="irAlCampo(ajuste.campoId)"
                            [attr.aria-label]="'Ir al campo ' + ajuste.campoEtiqueta">{{ ajuste.campoEtiqueta }}</button>
                  </li>
                }
              </ul>
            }

            <label for="revision-observacion-general" class="campo__rotulo devolucion__rotulo">Observación general</label>
            <small class="campo__ayuda devolucion__ayuda">Lo que no cabe en ningún campo.</small>
            <textarea id="revision-observacion-general" data-testid="revision-observacion-general" rows="2"
                      [ngModel]="observacionGeneral" (ngModelChange)="cambiarObservacionGeneral($event)" [disabled]="almacen.guardando()"
                      class="devolucion__texto"></textarea>

            @if (mostrandoRechazo()) {
              <div class="devolucion__rechazo">
                <label for="revision-motivo-rechazo" class="campo__rotulo devolucion__rotulo es-rechazo">Motivo del rechazo</label>
                <textarea id="revision-motivo-rechazo" data-testid="revision-motivo-rechazo" rows="2"
                          [(ngModel)]="motivoRechazo" [disabled]="almacen.guardando()"
                          class="devolucion__texto es-rechazo"></textarea>
                <p class="devolucion__aviso">La organización recibirá este motivo y el registro no será público.</p>
              </div>
            }
          </section>
        }

        <!--
          LAS DECISIONES, CON UN SOLO RELLENO A LA VEZ.

          Eran tres botones macizos seguidos —morado, verde y rojo— en versalitas de 12 px. Dos de
          los tres están siempre inhabilitados por construcción: solo se puede ENVIAR si hay campos
          señalados, y solo se puede PUBLICAR si no hay ninguno. Así que el relleno lo lleva la que
          de verdad aplica y las demás acompañan.
        -->
        <div class="flex flex-wrap items-center justify-end gap-2">
          @if (!almacen.yaEnviada() && confirmacionPendiente(); as accion) {
            <app-boton identificador="revision-volver" importancia="secundaria" (accion)="cancelarConfirmacion()">Volver a la revisión</app-boton>
            <app-boton identificador="revision-confirmar-accion"
                       [importancia]="accion === 'rechazar' ? 'destructiva' : accion === 'publicar' ? 'confirmar' : 'principal'"
                       [deshabilitado]="almacen.guardando()" (accion)="confirmarAccion()">
              @if (accion === 'sugerencias') { Enviar sugerencias }
              @else if (accion === 'publicar') { Publicar Festival }
              @else { Rechazar registro }
            </app-boton>
          } @else if (!almacen.yaEnviada()) {
            <app-boton identificador="revision-enviar"
                       [importancia]="almacen.cuantasNotas() > 0 ? 'principal' : 'secundaria'"
                       [deshabilitado]="almacen.guardando() || almacen.cuantasNotas() === 0"
                       (accion)="prepararEnvio()">Enviar sugerencias de ajuste</app-boton>
            @if (!mostrandoRechazo()) {
              <app-boton identificador="revision-publicar"
                         [importancia]="almacen.cuantasNotas() === 0 ? 'confirmar' : 'secundaria'"
                         [deshabilitado]="almacen.guardando() || almacen.cuantasNotas() > 0"
                         (accion)="prepararPublicacion()">Publicar Festival</app-boton>
              <app-boton identificador="revision-rechazar" importancia="destructiva"
                         [deshabilitado]="almacen.guardando()" (accion)="mostrarRechazo()">Rechazar registro</app-boton>
            } @else {
              <app-boton importancia="secundaria" [deshabilitado]="almacen.guardando()" (accion)="cancelarRechazo()">Cancelar rechazo</app-boton>
              <app-boton identificador="revision-confirmar-rechazo" importancia="destructiva"
                         [deshabilitado]="almacen.guardando() || !motivoRechazo.trim()" (accion)="prepararRechazo()">Continuar</app-boton>
            }
          }
        </div>
      </div>
    </app-ficha-festival>
    } @else {
      <section role="status" class="mx-auto max-w-xl border-l-4 border-morado bg-white px-5 py-6 shadow-lg">
        @if (errorFestival()) {
          <h2 class="font-alternate text-lg font-bold text-slate-900">No fue posible abrir la ficha completa.</h2>
          <p role="alert" class="mt-2 text-sm leading-relaxed text-rose-800">{{ errorFestival() }}</p>
          <button type="button" (click)="cargarFestival()" class="mt-4 rounded-lg bg-morado px-4 py-2 text-sm font-bold text-white">Intentar nuevamente</button>
        } @else {
          <p class="text-sm text-slate-600">Cargando la información recibida…</p>
        }
      </section>
    }

    <!--
      CERRAR PERDIENDO LO SEÑALADO. Va «encima» porque la ficha ya es una capa dentro del panel de
      Solicitudes, y Esc no cierra la confirmación: quien acaba de pulsar Esc para cerrar la ficha
      volvería a pulsarlo por inercia y perdería justo lo que se le está preguntando.
    -->
    @if (cierreDudoso()) {
      <app-confirmacion
        encima
        titulo="Cerrar sin guardar lo señalado"
        detalle="Los campos que señalaste todavía no se han guardado. Si cierras, se pierden y la revisión queda como estaba."
        accion="Cerrar y perderlo"
        cancelacion="Seguir revisando"
        identificador="confirmar-cierre-de-revision"
        (confirmar)="cerrarPerdiendoLoSenalado()"
        (cancelar)="cierreDudoso.set(false)" />
    }
  `,
  styles: [`
    /* ── Lo que se le devuelve a la organización ──────────────────────────
       EL BLOQUE ESTABA TODO A 12 PX DEBAJO DE UN CONTENIDO A 16. Los nombres de
       los campos señalados y el rótulo del área de texto son lo que se lee y se
       pulsa aquí, no metadatos: suben al escalón intermedio. Lo pidió así el
       dirección de producto —«siguen muy desbalanceados muchos tamaños de texto,
       como lo que dice abajo»— señalando justamente este bloque.
       No es una tarjeta: es el último bloque de la ficha y se separa con un
       filete y aire, como el resto de las secciones. Un recuadro aquí sería
       una tarjeta dentro del panel, que es justo lo que se pidió evitar. */
    .devolucion { width: 100%; }

    /* ── La pantalla de confirmación ─────────────────────────────────────── */
    .confirmacion { max-width: 46rem; padding: 1.5rem 0 2rem; }
    .confirmacion__titulo {
      margin: .3rem 0 0;
      color: var(--color-valor);
      font-family: var(--font-alternate);
      font-size: 1.4rem;
      font-weight: 800;
      letter-spacing: -.01em;
      line-height: 1.15;
    }
    .confirmacion__texto {
      margin: .9rem 0 0;
      max-width: 58ch;
      color: var(--color-prosa);
      font-size: var(--text-cuerpo);
      line-height: 1.5;
    }
    .confirmacion__texto strong { color: var(--color-valor); font-weight: 700; }

    .confirmacion__lista { margin: 1.4rem 0 0; padding: 0; list-style: none; }
    .confirmacion__lista li { border-top: 1px solid var(--color-filete); padding: .85rem 0; }
    .confirmacion__campo {
      display: flex;
      flex-wrap: wrap;
      align-items: baseline;
      gap: .2rem .6rem;
      margin: 0;
      color: var(--color-valor);
      font-size: var(--text-lectura);
      font-weight: 600;
    }
    /* De qué sección es, junto al campo y no alineado a la derecha: ahí se leía
       como una columna suelta que no tenía encabezado. */
    .confirmacion__campo span { color: var(--color-rotulo); font-size: var(--text-dato); font-weight: 400; }
    .confirmacion__nota { margin: .25rem 0 0; max-width: 58ch; color: var(--color-prosa); font-size: var(--text-lectura); line-height: 1.5; }
    .confirmacion__aparte { margin-top: 1.4rem; }

    .rotulo-de-seccion.es-rechazo { color: #9c4630; }
    .rotulo-de-seccion.es-publicacion { color: var(--color-verde-texto); }

    /* Las pestañas de sección van pegadas al encabezado del panel, igual que
       las del resto de la consola. */
    .secciones-de-la-ficha { margin: 0 0 1.25rem; }

    /* UN SOLO ROTULO DE SECCION EN TODA LA CONSOLA. El mismo peso, color y
       espaciado que «DATOS DEL TRÁMITE» en el resumen y que «ATENCIÓN
       PRIORITARIA» en el Resumen operativo. */
    .rotulo-de-seccion {
      margin: 0 0 .35rem;
      color: var(--color-seccion);
      font-size: var(--text-dato);
      font-weight: 900;
      letter-spacing: .08em;
      text-transform: uppercase;
    }

    /* EL CONTEXTO DEL ENVIO, SIN FILETE DE COLOR AL COSTADO. Era una línea
       morada de dos píxeles que solo servía de adorno: lo que separa este
       bloque de los campos es el aire y un filete horizontal, igual que
       separan las secciones del resumen. */
    .contexto {
      margin-bottom: 1.25rem;
      border-bottom: 1px solid var(--color-filete);
      padding-bottom: 1rem;
    }
    .contexto__texto { margin: 0; color: var(--color-prosa); font-size: var(--text-cuerpo); line-height: 1.55; }
    .contexto__texto strong { color: var(--color-valor); font-weight: 700; }
    .contexto__fecha { flex: none; color: var(--color-rotulo); font-size: var(--text-dato); }
    .contexto__matiz { margin: .45rem 0 0; max-width: 58ch; color: var(--color-rotulo); font-size: var(--text-cuerpo); line-height: 1.5; }

    .devolucion__titulo {
      margin: 0 0 .5rem;
      color: var(--color-seccion);
      font-size: var(--text-dato);
      font-weight: 900;
      letter-spacing: .08em;
      text-transform: uppercase;
    }

    .devolucion__recuento {
      margin: 0 0 .6rem;
      max-width: 58ch;
      color: var(--color-prosa);
      font-size: var(--text-lectura);
      line-height: 1.5;
    }
    .devolucion__recuento b { color: var(--color-morado); font-size: var(--text-destacado); font-weight: 800; }
    .devolucion__vacio { color: var(--color-valor); font-weight: 700; }
    .devolucion__pendiente { color: #8a5a12; font-weight: 700; }

    /* Los campos señalados, cada uno como puerta a su campo. No son píldoras:
       son enlaces separados por un filete vertical. */
    .devolucion__campos {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 0 .1rem;
      margin: 0 0 .85rem;
      padding: 0;
      list-style: none;
    }
    .devolucion__campos li + li::before {
      content: '';
      display: inline-block;
      width: 1px; height: .8rem;
      margin: 0 .15rem;
      background: #dfe3ea;
      vertical-align: middle;
    }
    .devolucion__campos button {
      min-height: var(--spacing-control-minimo);
      border: 0;
      background: none;
      padding: 0 .35rem;
      color: var(--color-morado);
      font-size: var(--text-cuerpo);
      font-weight: 600;
      cursor: pointer;
    }
    .devolucion__campos button:hover { text-decoration: underline; }

    /* Solo el aire y, en el rechazo, el color que lo distingue: la tipografía es la del proyecto. */
    .devolucion__rotulo { margin-bottom: .35rem; }
    .devolucion__ayuda { margin: -.2rem 0 .45rem; }
    .devolucion__rotulo.es-rechazo { color: #9c4630; }

    .devolucion__texto {
      width: 100%;
      border: 1px solid #dde1e8;
      background: #fff;
      border-radius: .6rem;
      padding: .55rem .7rem;
      color: var(--color-valor);
      font-size: var(--text-lectura);
      line-height: 1.5;
      resize: vertical;
    }
    .devolucion__texto:focus { border-color: var(--color-morado); outline: none; }
    .devolucion__texto.es-rechazo { border-color: #e0bcb1; }
    .devolucion__texto.es-rechazo:focus { border-color: #9c4630; }

    .devolucion__rechazo { margin-top: 1rem; }
    .devolucion__aviso { margin: .35rem 0 0; color: #9c4630; font-size: var(--text-dato); }
  `],
})
export class FichaEnRevisionComponent implements OnInit, OnDestroy {
  /** El Festival de la bandeja: identificador, nombre y organización. */
  @Input({ required: true }) festivalId!: string;
  @Input() nombreFestival = '';
  @Input() organizacionId = '';
  @Input() nombreOrganizacion = '';

  /**
   * Vivir DENTRO de una columna en vez de flotar como diálogo.
   *
   * Añadido para el panel de Solicitudes: el criterio pide
   * «un mejor y más ampliado uso del panel derecho que aparece al cliquear en cualquier solicitud,
   * se puede trasladar a allí la revisión completa directamente, es más óptimo». Un diálogo encima
   * del panel tapaba la lista y obligaba a cerrarlo para pasar al siguiente trámite.
   *
   * Reutiliza `modoIncrustado` de {@link FichaFestivalComponent}, que ya existía para la ficha
   * interna del panel de la organización: la misma ficha sin fondo oscuro ni trampa de foco.
   */
  @Input() comoPanel = false;

  /** El panel que la monta ya dice de qué registro se trata; no se repite el encabezado. */
  @Input() sinEncabezado = false;

  /** Qué sección de la ficha se está mirando. Solo aplica montada como panel. */
  readonly seccionElegida = signal<string>(SECCIONES_DE_LA_FICHA[0].id);

  /**
   * Las pestañas, con cuántos ajustes señalados hay detrás de cada una.
   *
   * Se cuenta lo señalado en esta revisión —no lo ya atendido en ciclos anteriores—, que es lo que
   * se va a devolver y lo único que puede seguir creciendo mientras se revisa.
   */
  readonly pestanasDeLaFicha = computed(() => {
    const porSeccion = new Map<string, number>();
    for (const nota of this.almacen.paraGuardar('').observaciones) {
      porSeccion.set(nota.seccionId, (porSeccion.get(nota.seccionId) ?? 0) + 1);
    }
    return SECCIONES_DE_LA_FICHA.map(seccion => ({
      id: seccion.id,
      etiqueta: seccion.titulo,
      // Sin ninguno señalado no se pinta un cero: un cero al lado de cada pestaña es ruido.
      conteo: porSeccion.get(seccion.id) || undefined,
    }));
  });

  /** Cerrar la ficha. */
  @Output() cerrar = new EventEmitter<void>();

  /** La solicitud salió: la bandeja tiene que recargarse, el Festival ya no está en revisión. */
  @Output() enviada = new EventEmitter<void>();
  @Output() decidida = new EventEmitter<'Publicar' | 'Rechazar'>();

  readonly almacen = inject(RevisionDeCamposStore);
  private readonly api = inject(PanelOrganizacionApi) as FichaEnRevisionApi;

  observacionGeneral = '';
  motivoRechazo = '';
  readonly mostrandoRechazo = signal(false);
  readonly confirmacionPendiente = signal<'sugerencias' | 'publicar' | 'rechazar' | null>(null);
  readonly estadoAutoguardado = signal<'pendiente' | 'guardando' | 'guardado'>('guardado');
  readonly pulsoAutoguardado = signal(false);
  readonly horaAutoguardado = signal('');
  readonly festivalCompleto = signal<FestivalDeLaOrganizacion | null>(null);
  readonly errorFestival = signal('');

  private readonly cargada = signal(false);
  private temporizadorDeAutoguardado: ReturnType<typeof setTimeout> | null = null;
  private temporizadorDeConfirmacion: ReturnType<typeof setTimeout> | null = null;

  constructor() {
    effect(() => {
      this.almacen.versionDelBorrador();
      if (this.cargada() && this.almacen.sinGuardar() && !this.almacen.yaEnviada()) {
        this.programarAutoguardado();
      }
    });
  }

  ngOnInit(): void {
    // ESTE `set` ES LO QUE PONE LA FICHA EN MODO REVISION, y es el unico interruptor: la ficha lee
    // el modo del almacen. Se hace ANTES de pedir nada para que el primer pintado ya salga bien.
    this.almacen.modo.set('revision');
    this.cargarFestival();
    this.api.obtenerRevision(this.festivalId).subscribe({
      next: revision => this.sembrar(revision),
      error: (fallo: { message?: string }) => {
        // SE SIEMBRA VACIA IGUALMENTE. Que no se pueda leer la revision no puede impedir LEER la
        // ficha: el funcionario tiene que poder ver lo que la organizacion diligencio aunque el
        // circuito de notas este caido.
        this.almacen.sembrar(null, this.festivalId, this.nombreFestival);
        this.almacen.error.set(fallo?.message ?? 'No fue posible abrir la revisión del Festival');
      },
    });
    this.api.obtenerComparacionEnvios(this.festivalId).subscribe({
      next: comparacion => this.almacen.comparacionEnvios.set(comparacion),
      error: () => this.almacen.comparacionEnvios.set(null),
    });
  }

  cargarFestival(): void {
    this.errorFestival.set('');
    this.api.obtenerFestivalEnRevision(this.festivalId).subscribe({
      next: festival => this.festivalCompleto.set(festival),
      error: (fallo: { message?: string }) => {
        this.festivalCompleto.set(null);
        this.errorFestival.set(fallo?.message ?? 'No fue posible cargar la información completa del Festival');
      },
    });
  }

  cambiarObservacionGeneral(valor: string): void {
    this.observacionGeneral = valor;
    this.estadoAutoguardado.set('pendiente');
    this.programarAutoguardado();
  }

  guardarBorrador(automatico = false): void {
    if (this.almacen.guardando() || this.almacen.yaEnviada()) return;
    this.cancelarAutoguardado();
    this.estadoAutoguardado.set('guardando');
    this.almacen.guardando.set(true);
    this.almacen.error.set('');
    this.almacen.mensaje.set('');
    this.api.guardarBorrador(this.festivalId, this.almacen.paraGuardar(this.observacionGeneral)).subscribe({
      next: revision => {
        this.sembrar(revision);
        this.almacen.guardando.set(false);
        this.estadoAutoguardado.set('guardado');
        this.horaAutoguardado.set(new Date().toLocaleTimeString('es-CO', { hour: '2-digit', minute: '2-digit' }));
        if (automatico) this.animarConfirmacionDeGuardado();
        this.almacen.mensaje.set(automatico
          ? 'Borrador de revisión guardado automáticamente.'
          : 'El borrador de la revisión quedó guardado. La organización todavía no lo ve.');
      },
      error: (fallo: { message?: string }) => {
        this.almacen.guardando.set(false);
        this.estadoAutoguardado.set('pendiente');
        this.almacen.error.set(fallo?.message ?? 'No fue posible guardar el borrador de la revisión');
      },
    });
  }

  prepararEnvio(): void {
    if (this.almacen.cuantasNotas() === 0) return;
    this.confirmacionPendiente.set('sugerencias');
  }

  prepararPublicacion(): void {
    if (this.almacen.cuantasNotas() > 0) return;
    this.confirmacionPendiente.set('publicar');
  }

  prepararRechazo(): void {
    if (!this.motivoRechazo.trim()) return;
    this.confirmacionPendiente.set('rechazar');
  }

  cancelarConfirmacion(): void {
    const accion = this.confirmacionPendiente();
    this.confirmacionPendiente.set(null);
    if (accion === 'rechazar') this.mostrandoRechazo.set(true);
  }

  confirmarAccion(): void {
    const accion = this.confirmacionPendiente();
    if (accion === 'sugerencias') this.enviar();
    if (accion === 'publicar') this.publicar();
    if (accion === 'rechazar') this.rechazar();
  }

  private enviar(): void {
    if (this.almacen.cuantasNotas() === 0) return;

    this.almacen.guardando.set(true);
    this.almacen.error.set('');
    this.api.enviarSolicitud(this.festivalId, this.almacen.paraGuardar(this.observacionGeneral)).subscribe({
      next: revision => {
        this.sembrar(revision);
        this.almacen.guardando.set(false);
        this.almacen.mensaje.set('Las sugerencias de ajuste se enviaron a la organización.');
        this.enviada.emit();
      },
      error: (fallo: { message?: string }) => {
        this.almacen.guardando.set(false);
        this.almacen.error.set(fallo?.message ?? 'No fue posible enviar las sugerencias de ajuste');
      },
    });
  }

  private publicar(): void {
    if (this.almacen.cuantasNotas() > 0) return;
    this.resolverDecision('Publicar');
  }

  mostrarRechazo(): void {
    this.mostrandoRechazo.set(true);
    this.confirmacionPendiente.set(null);
    this.almacen.error.set('');
  }

  cancelarRechazo(): void {
    this.mostrandoRechazo.set(false);
    this.confirmacionPendiente.set(null);
    this.motivoRechazo = '';
  }

  private rechazar(): void {
    const motivo = this.motivoRechazo.trim();
    if (!motivo) return;
    this.resolverDecision('Rechazar', motivo);
  }

  /**
   * Cerrar sin perder lo escrito.
   *
   * CON NOTAS SIN GUARDAR SE PREGUNTA, y no es una confirmación de cortesía: el borrador vive en el
   * navegador hasta que se pulsa «Guardar», así que cerrar lo pierde entero.
   */
  /**
   * Llevar a un campo señalado desde la lista del pie.
   *
   * Se busca la propia nota —`nota-<campo>`, que pinta `dato-de-la-ficha`— y no el control de
   * «pedir cambio»: cuando el campo ya tiene nota, ese control no se pinta, así que apuntar a él
   * dejaría la lista sin efecto justo en el único caso en que la lista existe.
   */
  irAlCampo(campoId: string): void {
    const destino = document.querySelector(`[data-testid="nota-${campoId}"]`);
    destino?.scrollIntoView({ behavior: 'smooth', block: 'center' });
  }

  /**
   * Lo que se perdería al cerrar, esperando confirmación.
   *
   * SE PREGUNTA EN EL DIALOGO DEL PROYECTO. Era un `window.confirm`, y este es el peor sitio para
   * uno: la ficha vive dentro del panel de Solicitudes, así que el diálogo del sistema operativo se
   * abría sobre una capa que ya estaba abierta, sin decir cuántos campos se pierden.
   */
  readonly cierreDudoso = signal(false);

  intentarCerrar(): void {
    if (this.almacen.sinGuardar()) { this.cierreDudoso.set(true); return; }
    this.cerrar.emit();
  }

  cerrarPerdiendoLoSenalado(): void {
    this.cierreDudoso.set(false);
    this.cerrar.emit();
  }

  ngOnDestroy(): void {
    this.cancelarAutoguardado();
    if (this.temporizadorDeConfirmacion) clearTimeout(this.temporizadorDeConfirmacion);
  }

  fecha(valor: string | null | undefined): string {
    if (!valor) return 'una fecha que no quedó registrada';
    const cuando = new Date(valor);
    return Number.isNaN(cuando.getTime()) ? valor : formatDate(cuando, FECHA_Y_HORA_ADMINISTRATIVA, 'es-CO');
  }

  ordinalEnvio(numero: number): string {
    const ordinales: Record<number, string> = { 1: 'Primer', 2: 'Segundo', 3: 'Tercer' };
    return ordinales[numero] ?? `${numero}.º`;
  }

  esMensajeDeAutoguardado(): boolean {
    return this.almacen.mensaje() === 'Borrador de revisión guardado automáticamente.';
  }

  etiquetaDeSeccion(seccionId: string): string {
    const etiquetas: Record<string, string> = {
      generales: 'Datos generales',
      'contacto-festival': 'Contacto del Festival',
      'musica-festival': 'Prácticas y territorios sonoros',
    };
    return etiquetas[seccionId] ?? 'Otra sección de la ficha';
  }

  private resolverDecision(accion: 'Publicar' | 'Rechazar', motivoRechazo?: string): void {
    this.almacen.guardando.set(true);
    this.almacen.error.set('');
    this.api.decidirFestival(this.festivalId, accion, motivoRechazo).subscribe({
      next: () => {
        this.almacen.guardando.set(false);
        this.decidida.emit(accion);
      },
      error: (fallo: { message?: string }) => {
        this.almacen.guardando.set(false);
        this.almacen.error.set(fallo?.message ?? 'No fue posible registrar la decisión institucional');
      },
    });
  }

  private sembrar(revision: RevisionDeCampos): void {
    this.almacen.sembrar(revision, this.festivalId, this.nombreFestival);
    this.observacionGeneral = revision.observacionGeneral ?? '';
    this.cargada.set(true);
  }

  private programarAutoguardado(): void {
    this.cancelarAutoguardado();
    this.estadoAutoguardado.set('pendiente');
    this.temporizadorDeAutoguardado = setTimeout(() => this.guardarBorrador(true), 800);
  }

  private cancelarAutoguardado(): void {
    if (this.temporizadorDeAutoguardado) clearTimeout(this.temporizadorDeAutoguardado);
    this.temporizadorDeAutoguardado = null;
  }

  private animarConfirmacionDeGuardado(): void {
    if (this.temporizadorDeConfirmacion) clearTimeout(this.temporizadorDeConfirmacion);
    this.pulsoAutoguardado.set(true);
    this.temporizadorDeConfirmacion = setTimeout(() => this.pulsoAutoguardado.set(false), 1200);
  }
}
