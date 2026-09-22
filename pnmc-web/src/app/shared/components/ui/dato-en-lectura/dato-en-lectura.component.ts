import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { IconoDeEnlaceComponent, TipoDeEnlace } from '../icono-de-enlace/icono-de-enlace.component';

/**
 * Un dato ya registrado, tal como se lee.
 *
 * <b>POR QUE ES UN COMPONENTE Y NO UNAS CLASES REPETIDAS.</b> Las dos fichas del espacio de la
 * organización —la de la organización y la de la persona responsable— enseñan lo mismo: rótulo,
 * valor y, a veces, una nota que explica por qué ese valor no se toca desde aquí. Escritas a mano
 * en los dos ficheros ya diferían: la misma pareja rótulo/valor
 * llevaba dos pesos y dos grises según de qué plantilla viniera. Una sola pieza es también una
 * sola decisión cuando haya que cambiarla.
 *
 * <b>EL ANFITRION NO OCUPA SITIO EN EL ARBOL.</b> `display: contents` hace que el `<dt>` y el
 * `<dd>` cuenten como hijos directos del `<div>` que envuelve cada fila, que es lo que exige la
 * especificación de `<dl>` —donde un elemento propio no sería un hijo válido— y lo que espera
 * quien recorre la ficha por `dl > div`. Es la misma solución que usa `app-boton`.
 *
 * Un anfitrión en línea, que es lo que se obtiene sin declarar nada, haría además lo que el 14 de
 * septiembre de 2026 dejó el aviso del Resumen pegado al bloque siguiente: envolver bloques en una
 * caja en línea y perder por el camino los márgenes verticales.
 *
 * <b>LO QUE FALTA NO SE PINTA COMO LO QUE HAY.</b> «Sin registrar» es una ausencia, no un valor:
 * va en el gris de los rótulos y con el peso del texto corriente. Antes se leía con el mismo negro
 * y el mismo peso que un dato real, así que una ficha vacía parecía una ficha llena.
 */
@Component({
  selector: 'app-dato-en-lectura',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [IconoDeEnlaceComponent],
  styles: [':host { display: contents; }'],
  /*
    EL DATO VA PRIMERO Y EL ROTULO DEBAJO. Lo pidió así la dirección de producto: en una ficha que se consulta, lo que se busca es el valor —«Fundación de Prueba»—, y
    el rótulo solo hace falta para confirmar de qué campo es.

    EN EL ARBOL, EL `dt` SIGUE ANTES QUE EL `dd`, porque es lo único que admite la especificación de
    `<dl>` y es el orden en que lo lee un lector de pantalla: «Nombre de la organización, Fundación
    de Prueba». Lo que se invierte es la presentación, con `order`, y para eso la fila que envuelve
    a cada dato es una columna flexible.
  */
  template: `
    <dt class="order-2 mt-1 text-dato text-rotulo">{{ rotulo() }}</dt>
    <dd class="order-1 break-words text-lectura"
        [class.font-semibold]="!vacio()"
        [class.text-valor]="!vacio()"
        [class.text-rotulo]="vacio()"
        [class.whitespace-pre-line]="prosa()"
        [class.flex]="enlace()"
        [class.items-baseline]="enlace()"
        [class.gap-2]="enlace()">@if (enlace(); as tipo) {
        <!--
          EL ICONO VA CON EL VALOR Y NO CON EL ROTULO. Un enlace se reconoce por lo que es —un
          sitio, una cuenta de Instagram— antes de leer de qué campo viene, y el rótulo ya lo dice
          debajo. Cuando no hay valor el icono se queda, pero en el gris de los rótulos: sigue
          diciendo de qué fila se trata sin aparentar que hay un enlace que abrir.
        -->
        <span class="shrink-0 self-center" [class.text-rotulo]="vacio()" [class.text-morado-claro]="!vacio()">
          <app-icono-de-enlace [tipo]="tipo" [tamano]="16" />
        </span>
      }<span class="min-w-0 break-words" [class.whitespace-pre-line]="prosa()">{{ vacio() ? 'Sin registrar' : valor()
      }}@if (!vacio() && sufijo()) {<span class="font-normal text-rotulo"> ({{ sufijo() }})</span>}</span></dd>
    @if (nota()) {
      <dd class="order-3 mt-1 text-dato leading-relaxed text-rotulo">{{ nota() }}</dd>
    }
  `,
})
export class DatoEnLecturaComponent {
  readonly rotulo = input.required<string>();

  /** El valor guardado. Vacío o nulo se lee como «Sin registrar». */
  readonly valor = input<string | null | undefined>('');

  /** Matiz entre paréntesis dentro del mismo valor: el tipo de un documento, la unidad de una cifra. */
  readonly sufijo = input<string | null | undefined>('');

  /** Por qué este dato no se corrige desde aquí. Se lee debajo, en el gris de los rótulos. */
  readonly nota = input<string | null | undefined>('');

  /** El valor es un texto redactado y conserva sus saltos de línea. */
  readonly prosa = input(false);

  /** Si el valor es un enlace, cuál: así se elige el icono que lo acompaña. */
  readonly enlace = input<TipoDeEnlace | null>(null);

  readonly vacio = computed(() => (this.valor() ?? '').toString().trim().length === 0);
}
