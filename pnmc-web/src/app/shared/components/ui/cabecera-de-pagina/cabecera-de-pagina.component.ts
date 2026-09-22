import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * La cabecera de una página de la consola.
 *
 * <b>ES EL PRIMER PELDAÑO DE LA JERARQUIA QUE FIJA EL LENGUAJE VISUAL DE LA CONSOLA</b> —contexto,
 * título y descripción, acciones principales— y por eso es un componente y no un bloque escrito en
 * cada sección: la auditoría contó 17 firmas distintas de título en
 * 17 secciones. Que todas pasen por aquí es lo que hace que una persona reconozca la consola esté
 * donde esté.
 *
 * <b>EL CONTEXTO ES EL GRUPO, NO UN ROTULO FIJO.</b> Antes decía «Gestión administrativa» en todas
 * las páginas, que es lo mismo que no decir nada: ya se sabe dónde se está. Ahora dice la familia a
 * la que pertenece la sección —Bandeja de trabajo, Ecosistema musical…—, que es la miga que
 * orienta.
 *
 * Las acciones de la página se proyectan: son de cada sección y esta pieza solo les da el sitio.
 */
@Component({
  selector: 'app-cabecera-de-pagina',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: [`
    :host { display: block; margin-bottom: 2rem; }
    .cabecera { display: flex; flex-wrap: wrap; align-items: flex-start; justify-content: space-between; gap: 1rem 2rem; }
    .cabecera__texto { min-width: 0; max-width: 46rem; }
    .cabecera__contexto { margin: 0 0 .55rem; color: var(--color-seccion); font-family: var(--font-alternate); font-size: var(--text-dato); font-weight: 700; letter-spacing: .14em; text-transform: uppercase; }
    .cabecera__titulo { margin: 0; color: var(--color-valor); font-family: var(--font-alternate); font-size: 2.05rem; font-weight: 700; line-height: 1.1; letter-spacing: -.01em; text-wrap: balance; }
    .cabecera__descripcion { margin: .6rem 0 0; color: var(--color-prosa); font-size: var(--text-lectura); line-height: 1.55; }
    .cabecera__acciones { display: flex; flex-wrap: wrap; align-items: center; gap: .5rem; margin-top: .25rem; }
    .cabecera__acciones:empty { display: none; }
    @media (max-width: 800px) { .cabecera__titulo { font-size: 1.7rem; } }
  `],
  template: `
    <header class="cabecera">
      <div class="cabecera__texto">
        @if (contexto()) { <p class="cabecera__contexto">{{ contexto() }}</p> }
        <h1 class="cabecera__titulo">{{ titulo() }}</h1>
        @if (descripcion()) { <p class="cabecera__descripcion">{{ descripcion() }}</p> }
      </div>
      <div class="cabecera__acciones"><ng-content /></div>
    </header>
  `,
})
export class CabeceraDePaginaComponent {
  readonly titulo = input.required<string>();
  /** La familia a la que pertenece la página: es la miga. */
  readonly contexto = input<string | null | undefined>(null);
  readonly descripcion = input<string | null | undefined>(null);
}
