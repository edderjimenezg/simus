import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { CommonModule } from '@angular/common';

/** Un dato de contexto de la pieza: cuándo, dónde, quién organiza. */
export interface DatoDeLaPieza {
  readonly rotulo: string;
  readonly valor: string;
}

/**
 * La pieza tal y como se lee, dentro del panel de gestión.
 *
 * <b>QUE PROBLEMA RESUELVE.</b> «Previsualizar» enseñaba la TARJETA DEL LISTADO público: cómo se ve
 * el registro dentro de la parrilla de noticias o de la agenda. Eso sirve para comprobar que el
 * recorte del título cabe y que la portada no queda vacía, pero no es lo que se pregunta antes de
 * publicar algo, que es cómo se lee. El criterio es este: * «el apartado de previsualizar debería mostrar cómo se ve la noticia o el evento como tal, no la
 * sección de noticias general», y al preguntarle precisó: dentro del panel de gestión.
 *
 * <b>ES UNA LECTURA, NO UN FORMULARIO NI UNA FICHA DE DATOS.</b> La ficha —`app-dato-en-lectura`
 * en un cajón— enseña campo a campo lo que el registro guarda, que es lo que hace falta para
 * revisarlo. Esto enseña el resultado: antetítulo, título, entradilla y cuerpo, con la medida de
 * línea y la escala de lectura del proyecto. Son dos preguntas distintas y por eso son dos vistas.
 *
 * <b>NO CARGA NADA.</b> Recibe lo que ya tiene el panel, así que funciona igual con un borrador que
 * con algo publicado. La previsualización anterior sí pedía al servidor la forma pública del
 * registro, y eso la ataba a que existiera una pantalla pública donde enseñarlo: la Agenda no tiene
 * página de detalle en el portal, así que para un evento no había nada que previsualizar «como
 * tal». Aquí sí lo hay, porque la pieza se compone con lo que el registro guarda.
 */
@Component({
  selector: 'app-previsualizacion-de-pieza',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule],
  styles: [`
    :host { display: block; }

    /* LA MEDIDA DE LINEA ES EL DATO. Un texto a todo lo ancho de un dialogo de 64 rem se lee mal,
       y previsualizar sirve justamente para juzgar como se lee. 65 caracteres es la medida que el
       lenguaje visual del proyecto fija para la prosa. */
    .pieza { max-width: 44rem; margin: 0 auto; }
    .pieza__portada { width: 100%; border-radius: .75rem; border: 1px solid var(--color-filete); object-fit: cover; aspect-ratio: 16 / 9; background: #f6f7f9; }
    /* EL HUECO DICE QUE FALTA, pero no se lleva media pantalla: a 704 px de ancho, una caja 16:9
       son 396 px de nada antes de llegar a lo que hay que leer, que es de lo que trata esto. */
    .pieza__sin-portada { display: flex; align-items: center; justify-content: center; gap: .4rem; width: 100%; min-height: 3.5rem; border-radius: .75rem; border: 1px dashed var(--color-filete); background: #f6f7f9; color: var(--color-rotulo); font-size: var(--text-dato); }
    .pieza__antetitulo { margin: 1.5rem 0 0; color: var(--color-seccion); font-family: var(--font-alternate); font-size: var(--text-dato); font-weight: 700; letter-spacing: .14em; text-transform: uppercase; }
    .pieza__titulo { margin: .5rem 0 0; color: var(--color-valor); font-family: var(--font-alternate); font-size: 1.9rem; font-weight: 700; line-height: 1.15; letter-spacing: -.01em; text-wrap: balance; }
    .pieza__firma { margin: .7rem 0 0; color: var(--color-rotulo); font-size: var(--text-cuerpo); }
    .pieza__entradilla { margin: 1.2rem 0 0; color: var(--color-valor); font-size: var(--text-destacado); line-height: 1.6; }
    .pieza__cuerpo { margin: 1.2rem 0 0; color: var(--color-prosa); font-size: var(--text-lectura); line-height: 1.75; white-space: pre-line; }
    .pieza__datos { display: grid; gap: .75rem 2rem; margin: 1.6rem 0 0; padding-top: 1.2rem; border-top: 1px solid var(--color-filete); grid-template-columns: repeat(auto-fit, minmax(11rem, 1fr)); }
    .pieza__datos dt { color: var(--color-rotulo); font-size: var(--text-dato); }
    .pieza__datos dd { margin: .15rem 0 0; color: var(--color-valor); font-size: var(--text-cuerpo); font-weight: 600; }
    @media (max-width: 800px) { .pieza__titulo { font-size: 1.5rem; } }
  `],
  template: `
    <article class="pieza">
      @if (portada()) {
        <img class="pieza__portada" [src]="portada()" [alt]="textoAlternativo() || ''"
          width="704" height="396" loading="lazy" decoding="async" />
      } @else {
        <!-- SE DICE QUE FALTA. Un hueco haría dudar de si la imagen no cargó. -->
        <p class="pieza__sin-portada">Sin imagen</p>
      }

      @if (antetitulo()) { <p class="pieza__antetitulo">{{ antetitulo() }}</p> }
      <h3 class="pieza__titulo">{{ titulo() }}</h3>
      @if (firma()) { <p class="pieza__firma">{{ firma() }}</p> }

      @if (entradilla()) { <p class="pieza__entradilla">{{ entradilla() }}</p> }
      @if (cuerpo()) { <p class="pieza__cuerpo">{{ cuerpo() }}</p> }

      @if (datos().length) {
        <dl class="pieza__datos">
          @for (dato of datos(); track dato.rotulo) {
            <div>
              <dt>{{ dato.rotulo }}</dt>
              <dd>{{ dato.valor }}</dd>
            </div>
          }
        </dl>
      }
    </article>
  `,
})
export class PrevisualizacionDePiezaComponent {
  readonly titulo = input.required<string>();
  /** La categoría, la sección o lo que sitúe la pieza. */
  readonly antetitulo = input<string | null | undefined>(null);
  /** Autoría y fecha, en una línea. */
  readonly firma = input<string | null | undefined>(null);
  readonly entradilla = input<string | null | undefined>(null);
  readonly cuerpo = input<string | null | undefined>(null);
  readonly portada = input<string | null | undefined>(null);
  readonly textoAlternativo = input<string | null | undefined>(null);
  /** Lo que acompaña a la pieza sin ser su texto: cuándo, dónde, quién organiza. */
  readonly datos = input<readonly DatoDeLaPieza[]>([]);
}
